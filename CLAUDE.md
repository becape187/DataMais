# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

DataMais é um sistema de **gestão de ensaios hidráulicos** para a MODEC: cadastra clientes, cilindros, sensores e ensaios, lê dados de um CLP via **Modbus TCP** em tempo real, grava as séries temporais no **InfluxDB** e gera **relatórios** de ensaio com veredito automático Aprovado/Reprovado.

Tem duas partes:
- **`DataMais/`** — API REST backend em **ASP.NET Core 8.0 (C#)**.
- **`DataMaisWeb/`** — frontend **React 19 + TypeScript + Vite**.

Todo o domínio é nomeado em **português**. Este repositório (`becape187/DataMais`) é a fonte da verdade — é o que roda em produção no servidor.

> Histórico: havia uma versão antiga "skeleton" (Clean Architecture vazia com entidades genéricas tipo `Receita`/`ReceitaCampoDef`). Ela foi **descartada** — ignore qualquer referência a `DataMais.Domain/Application/Infrastructure/Core`, `arquitetura_sistemas.md` ou plataforma genérica de bancadas. O código real é o descrito aqui.

## Comandos

```bash
# Backend
cd DataMais && dotnet restore && dotnet build
cd DataMais && dotnet run            # API em http://localhost:5000 (Swagger em /swagger)

# Migrations EF Core (PostgreSQL)
cd DataMais && dotnet ef migrations add <Nome>
cd DataMais && dotnet ef database update   # em Development as migrations também rodam no startup

# Frontend
cd DataMaisWeb && npm install
cd DataMaisWeb && npm run dev         # Vite em http://localhost:5173
cd DataMaisWeb && npm run build       # build de produção (tsc + vite build)
```

Não há projeto de testes nem linter configurado. `dotnet run` em Development aplica migrations automaticamente e expõe Swagger; em Production o Kestrel escuta em `0.0.0.0:5000` atrás de proxy nginx (SSL), com migrations aplicadas manualmente.

## Arquitetura

Backend ASP.NET Core convencional em camadas por pasta (não é Clean Architecture):

```
Program.cs        → bootstrap, DI, CORS, Swagger, connection string PostgreSQL
Configuration/    → AppConfig/DatabaseConfig/InfluxConfig (carregados do .env via ConfigService)
Controllers/      → endpoints REST (um por entidade): Cliente, Cilindro, Sensor, Ensaio,
                    Relatorio, ModbusConfig, CampoRelatorio, Config, Usuario
Models/           → entidades EF Core (POCO com Data Annotations)
Data/             → DataMaisDbContext (EF Core + Npgsql), DataMaisDbContextFactory
Migrations/       → migrations EF Core (histórico do schema PostgreSQL)
Services/         → ModbusService (conexões/leitura/escrita CLP), ConfigService (.env)
```

### Dois bancos de dados (arquitetura híbrida)

- **PostgreSQL** (via EF Core / `Npgsql`) — dados relacionais e de configuração: clientes, cilindros, sensores, ensaios, relatórios, usuários, configs Modbus, campos de relatório.
- **InfluxDB** (`InfluxDB.Client`) — séries temporais de pressão/carga durante os ensaios. **Não** é registrado no DI: os controllers (ex.: `RelatorioController`) instanciam `InfluxDBClient` sob demanda e consultam via **Flux**. Measurement principal: `ensaio_pressao`, com tag `ensaioId` e fields `pressaoA` / `pressaoB`.

### Coleta de dados via Modbus

`ModbusService` (singleton) lê o CLP via Modbus TCP (`NModbus`). Cada registro a ler/escrever é uma linha de `ModbusConfig` no PostgreSQL (IP, porta, slaveId, função, endereço, tipo de dado, byte order, fator/offset). Conexões TCP são **mantidas abertas permanentemente** por IP:porta (o CLP não tolera múltiplas conexões) e reaproveitadas; só são recriadas quando inválidas. `ConverterValor` aplica tipo (UInt16/Int16/Int32/Float), byte order, fator de conversão e offset.

**Toda transação Modbus é serializada por um `SemaphoreSlim` por conexão** (`ExecutarNaConexaoAsync`) — o `IModbusMaster` do NModbus não é thread-safe, e o monitor (1 Hz) mais o polling da tela (1 Hz) compartilham o mesmo socket. Sem isso, frames se entrelaçavam e a resposta de um discrete input era entregue ao outro (contagem "ligando sozinha", etapa que não encerrava). Regra: **nunca** chame `master.*`/`ObterOuCriarConexaoAsync` fora desse invólucro, e a remoção de conexão em falha acontece **dentro** do semáforo (nunca em cima de transação alheia).

### Calibração de sensor

`Sensor` guarda uma **calibração linear de 2 pontos** (`InputMin/OutputMin`, `InputMax/OutputMax`): converte valor AD bruto → grandeza de engenharia por interpolação linear.

## Ciclo de vida do ensaio (as duas câmaras, um laudo)

Um **ensaio** é o cabeçalho do teste (cliente, cilindro, vessel, OS) e nasce com **as duas câmaras** (`CamaraAHabilitada`/`CamaraBHabilitada`), cada uma rodada como uma `EnsaioEtapa` — em qualquer ordem, repetíveis. O operador pode **desmarcar uma delas** e fechar o laudo com a outra sozinha (ver abaixo). Estados do ensaio (`StatusEnsaio`): `EmAndamento` → `AguardandoAceite` → `Aceito`, ou `Cancelado`. Estados da etapa (`StatusEtapa`): `EmExecucao` → `Concluida` / `Descartada`, e `Repetida` quando uma tentativa posterior da mesma câmara a substitui.

O fluxo, em `EnsaioController`:

```
POST /ensaio                       cria o cabeçalho
PUT  /ensaio/{id}/camaras          marca/desmarca as câmaras que este ensaio testa
POST /ensaio/{id}/etapa            inicia câmara A ou B (Modbus)
POST /ensaio/etapa/{id}/encerrar   ?salvar=true|false
POST /ensaio/{id}/aceitar          → cria o Relatorio (queima o número REH-MPR)
POST /ensaio/{id}/cancelar
GET  /ensaio/ativo · /ensaio/pendentes
```

Pontos que não são óbvios pelo código:

- **O laudo só nasce no aceite.** `RegistroConclusaoMonitor` fecha a **etapa**, não gera relatório. Ensaio descartado não consome número.
- **Três marcos de tempo, e cada sinal do CLP manda em um só.** `DataInicio` é o clique do operador. `REGISTRO_RODANDO` inicia e para o registro: a descida dele — sozinha — fecha a etapa e define `DataFim`. `INICIA_CONTAGEM` manda só na contagem de tempo, **por nível e sem inferência**: ligou = `DataInicioContagem` (t0 do laudo), desligou = `DataFimContagem`; janela medida uma vez por etapa. Regra do operador, literal: "ligou o flag, inicia contagem" — o CLP só liga ao atingir a pressão de teste. **Não** adicionar esperteza aqui (borda obrigatória, anti-retenção, reabertura): já existiu e era o que impedia a contagem de começar. Falha de leitura do `INICIA_CONTAGEM` nunca trava o encerramento — o laudo degrada para a regra do setpoint.
- **A partida é bloqueante.** `IniciarEtapa` só devolve sucesso depois de confirmar por leitura que o `REGISTRO_RODANDO` subiu (5 s de timeout); não subindo, a etapa é removida e os coils desligados. Antes disso, ainda bloqueia se qualquer câmara estiver com pressão residual ≥ 3 bar.
- **O checklist é editável em Rascunho e congela no aceite.** `POST /relatorio/{id}/respostas-campos` grava a cada resposta (não há botão Salvar) e **recusa (409) em `Concluido`**. Reabrir é ato explícito — `POST /relatorio/{id}/reabrir` volta para Rascunho e registra `Reaberto`; a conclusão seguinte assina como v+1. Em laudo assinado o frontend renderiza o checklist como **texto**, não input desabilitado: além de não dar para editar por engano, `<input type="radio">` marcado **não aparece no PDF** (o html2canvas não desenha o ponto).
- **Campo de checklist excluído do cadastro continua no laudo que já o respondeu** — o `GET` manda soft-deleted com resposta e a tela exibe. Para aposentar uma pergunta de vez (sumir dos laudos antigos), o caminho é `DbSeeder.RemoverCamposDescontinuados`: soft-delete no campo **e** remoção das respostas. O snapshot assinado em `RelatorioVersao.RespostasJson` preserva o histórico.
- **Excluir laudo é `DELETE /relatorio/{id}`, só Admin e só em Rascunho.** Laudo `Concluido` (assinado) não pode ser excluído. A exclusão leva junto o ensaio (vai para `Cancelado`) e as leituras dele no Influx; o número REH-MPR **não** volta para o contador — a sequência do ano fica com um buraco, de propósito.
- **Todas as etapas compartilham a tag `ensaioId` no Influx** — o que separa uma câmara da outra é a janela de tempo da etapa. Toda query de análise recorta por ela.
- **Só a última tentativa concluída de cada câmara entra no laudo** (`EtapasValidas`); as anteriores ficam como histórico `Repetida`.
- **"As duas câmaras" virou "as câmaras habilitadas".** `Ensaio.CamarasHabilitadas` é a lista que manda em tudo: `AtualizarStatusEnsaio`, `RegistroConclusaoMonitor.ConcluirEtapaAsync`, `podeAceitar` e o gate de `AceitarEnsaio`. Nunca voltar a cravar `new[] { "A", "B" }` nesses pontos. Desmarcar as duas é recusado — o ensaio precisa de ao menos uma.
- **Desmarcar câmara que já rodou descarta a corrida dela** (`DefinirCamaras`): as etapas `Concluida`/`Repetida` viram `Descartada` e as leituras saem do Influx — com folga `TimeSpan.Zero` na janela, senão o buffer de 1 min levaria junto o começo da corrida da outra câmara. É o caminho do operador que não quer levar aquele resultado adiante; a tela confirma antes.
- **O laudo de câmara única não tem tratamento especial** — `EtapasValidas` devolve uma etapa, `CombinarResultados` julga só ela e a tela mostra "Câmaras Testadas: A". O que muda é o texto do critério (não pode dizer "as duas câmaras") e a observação gravada no aceite.
- `Ensaio.CamaraTestada`/`PressaoCargaConfigurada`/`TempoCargaConfigurado` estão **depreciados** — existem só para os ensaios anteriores a este modelo, cujo backfill os copiou para a primeira etapa.

## Critério de Aprovado / Reprovado (regra de negócio central)

O veredito é **calculado on-the-fly** ao abrir o relatório — **não é persistido** em coluna (só no snapshot da versão assinada).

Regra, por câmara: a análise recorta a **janela de contagem do CLP** — `[EnsaioEtapa.DataInicioContagem, DataFimContagem]`, carimbada pelas bordas do sinal `INICIA_CONTAGEM` — e nela mede-se o **pico da câmara OPOSTA**. Ensaios anteriores a esse registro (sem `DataInicioContagem`) caem na regra antiga: t0 = primeiro instante em que a câmara pressurizada **atinge o setpoint** (`EnsaioEtapa.PressaoCargaConfigurada`). Se esse pico ultrapassar `Cilindro.LimitePassagemCamaraA/B` (default 1 bar), o cilindro está dando passagem → **Reprovado**. As estatísticas da própria câmara (min/máx/média) são informativas: a pressão dela cai por vários motivos e não serve como critério.

Combinação: **uma câmara reprovada reprova o ensaio inteiro**; se nenhuma reprovou mas alguma não pôde ser avaliada, o resultado fica nulo (`-`). Qualquer campo de checklist `ReprovaSeSim` respondido "Sim" **sobrepõe tudo** e força Reprovado.

Onde fica:

- Backend: `DataMais/Controllers/RelatorioController.cs` → `AnalisarEtapaAsync` (por câmara) + `CombinarResultados` + override do checklist em `GetById`/`AvaliarResultadoAsync`.
- Frontend: `DataMaisWeb/src/pages/VisualizarRelatorio.tsx` consome o veredito por câmara **pronto do backend** e só aplica por cima o override do checklist (que o operador muda sem recarregar). A matemática de pressão **não é mais duplicada** no frontend — ao alterar a regra, mexa só no backend.

## Modelo de domínio (PostgreSQL)

`Cliente` 1—N `Cilindro` 1—N `Ensaio` 1—N `EnsaioEtapa`, e `Ensaio` 1—1 `Relatorio` (na prática: um laudo por ensaio aceito). Resumo dos centrais:

> **`Cliente` é o Vessel/Frota.** A MODEC não tem clientes externos — na interface inteira (menu, telas, laudo) a entidade se chama **Vessel/Frota**; "cliente" sobrevive só em tabela, FK, rota e campo JSON. Ao mexer em texto visível, use Vessel/Frota; ao mexer em código, `Cliente` continua sendo o nome.

- **Cilindro** — equipamento sob teste. Tem duas câmaras (A/B) com parâmetros próprios de ensaio: `MaximaPressaoA/B`, `PreCargaA/B`, `CargaNominalA/B`, tempos de rampa/duração, e percentuais de processo: `PercentualVariacaoAlarmeA/B`, `HistereseAlarmeA/B`, `PercentualVariacaoDesligaProcessoA/B` (estes regem alarme/desligamento durante o ensaio, distintos do critério de 5% do laudo).
- **Ensaio** — cabeçalho do teste num cilindro: identificação do documento (`LocalTeste`, `Departamento`, `OrdemServico`) e `Status`. Ver seção de ciclo de vida acima. `Ensaio.Vessel` está **depreciado** (texto livre que duplicava o cadastro): a coluna fica para os ensaios antigos, não é mais escrita nem exibida. `Ensaio.Numero` (`ENSAIO-20260731-163555`) é identificador interno — **não aparece em tela nem no laudo**; o número do documento é o REH-MPR.
- **EnsaioEtapa** — uma corrida numa câmara: `Camara` (A/B), `Tentativa`, `Status`, janela `DataInicio`/`DataFim`, `PressaoCargaConfigurada` (setpoint) e `TempoCargaConfigurado`. Único por `(EnsaioId, Camara, Tentativa)`.
- **Relatorio** — laudo de um ensaio, criado no aceite. Numeração `REH-MPR-0000001-2026` via `NumeroRelatorioService` (UPSERT atômico em `ContadorRelatorio`, sequencial reinicia a cada ano). Tem `RespostaCampoRelatorio` (respostas a `CampoRelatorio`, campos configuráveis tipo "SimOuNao" etc.) — o checklist é **do ensaio**, respondido uma vez e valendo para as duas câmaras.
- **Sensor** / **ModbusConfig** — ver seções acima.

## Convenções

- Entidades, propriedades e vocabulário de domínio em **português** — siga isso em código novo.
- Em `OnModelCreating`, relacionamento cuja entidade-pai tem a coleção **precisa** apontá-la: `WithMany(pai => pai.Filhos)`. Com `WithMany()` vazio o EF trata a navegação como um segundo relacionamento e cria uma **FK sombra** (`XId1`) — o código grava na FK explícita e o `Include` lê pela sombra, sempre nula. Foi o que apagou o checklist do laudo por meses (`RespostasCampoRelatorio.RelatorioId1`).
- Models são POCO com **Data Annotations** (`[Key]`, `[Required]`, `[MaxLength]`, `[Column(TypeName=...)]`). Relacionamentos e índices ficam em `DataMaisDbContext.OnModelCreating`. Soft-delete só onde existe (`CampoRelatorio.DataExclusao`); a maioria usa `DataCriacao`/`DataAtualizacao` sem soft-delete.
- JSON da API é **camelCase** (configurado em `Program.cs`); o frontend consome assim.
- Configuração sensível (PostgreSQL, InfluxDB token, Modbus) vem de um arquivo **`.env`** lido por `ConfigService` — nunca hardcode credenciais. Ver `DataMais/env.example`, `INFLUX_SETUP.md`, `CONFIGURAR_SECRETS.md`.
- `net8.0`, `Nullable` e `ImplicitUsings` habilitados.

## Deploy

Push na branch `main` dispara deploy via **GitHub Actions** por SSH (`.github/workflows/deploy.yml`). O backend roda como serviço systemd (`DataMais/datamais.service`) atrás de nginx. Ver `DEPLOY.md`, `ATUALIZAR_SERVICO.md`, `CONFIGURAR_SECRETS.md`.

## Infra MODEC

`docs/topologia-infra-modec.md` documenta a topologia de rede da instalação (CLP Weidmüller, MK1/MK2, VM, túneis WireGuard) — referência para conectividade do CLP e publicação dos serviços.

## Diário de sessões (contexto entre máquinas)

O desenvolvimento acontece numa máquina e o teste/deploy na VM do cliente — e o Claude Code roda nas duas. **`docs/sessoes/`** guarda o resumo de cada sessão de trabalho (o que foi feito, causa raiz, commits, pendências de bancada). **Ao retomar trabalho em qualquer máquina, leia o arquivo mais recente de `docs/sessoes/` antes de mexer.** Ao concluir uma frente de trabalho: commit + push + `deploy-local.sh` em dia + atualizar o diário — o usuário só testa no cliente, então trabalho não pushado é invisível.
