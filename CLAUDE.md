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

### Calibração de sensor

`Sensor` guarda uma **calibração linear de 2 pontos** (`InputMin/OutputMin`, `InputMax/OutputMax`): converte valor AD bruto → grandeza de engenharia por interpolação linear.

## Critério de Aprovado / Reprovado (regra de negócio central)

O veredito do ensaio é **calculado on-the-fly** ao abrir o relatório — **não é persistido** em coluna. A regra está duplicada em backend e frontend (mantenha as duas em sincronia ao alterar):

- Backend: `DataMais/Controllers/RelatorioController.cs` → `GetById` + `CalcularEstatisticasPressaoAsync`.
- Frontend: `DataMaisWeb/src/pages/VisualizarRelatorio.tsx` → `calcularResultado()`.

Regra: a partir do instante em que a pressão **atinge o setpoint** (`Ensaio.PressaoCargaConfigurada`) pela primeira vez, coleta-se a pressão mínima do restante do ensaio. **Aprovado se `pressaoMin >= setpoint * 0.95`** (desvio ≤ 5%), senão **Reprovado**. A câmara testada (`Ensaio.CamaraTestada` = "A"/"B") seleciona o field Influx (`pressaoA`/`pressaoB`). Se nunca atinge o setpoint ou falta config Influx, o resultado fica nulo (`-`).

## Modelo de domínio (PostgreSQL)

`Cliente` 1—N `Cilindro` 1—N `Ensaio` 1—N `Relatorio`. Resumo dos centrais:

- **Cilindro** — equipamento sob teste. Tem duas câmaras (A/B) com parâmetros próprios de ensaio: `MaximaPressaoA/B`, `PreCargaA/B`, `CargaNominalA/B`, tempos de rampa/duração, e percentuais de processo: `PercentualVariacaoAlarmeA/B`, `HistereseAlarmeA/B`, `PercentualVariacaoDesligaProcessoA/B` (estes regem alarme/desligamento durante o ensaio, distintos do critério de 5% do laudo).
- **Ensaio** — execução de um teste num cilindro. `Status` (string: Pendente/EmExecucao/Concluido/Cancelado), `CamaraTestada`, `PressaoCargaConfigurada` (setpoint), `TempoCargaConfigurado`.
- **Relatorio** — laudo de um ensaio. Tem `RespostaCampoRelatorio` (respostas a `CampoRelatorio`, campos configuráveis tipo "SimOuNao" etc.).
- **Sensor** / **ModbusConfig** — ver seções acima.

## Convenções

- Entidades, propriedades e vocabulário de domínio em **português** — siga isso em código novo.
- Models são POCO com **Data Annotations** (`[Key]`, `[Required]`, `[MaxLength]`, `[Column(TypeName=...)]`). Relacionamentos e índices ficam em `DataMaisDbContext.OnModelCreating`. Soft-delete só onde existe (`CampoRelatorio.DataExclusao`); a maioria usa `DataCriacao`/`DataAtualizacao` sem soft-delete.
- JSON da API é **camelCase** (configurado em `Program.cs`); o frontend consome assim.
- Configuração sensível (PostgreSQL, InfluxDB token, Modbus) vem de um arquivo **`.env`** lido por `ConfigService` — nunca hardcode credenciais. Ver `DataMais/env.example`, `INFLUX_SETUP.md`, `CONFIGURAR_SECRETS.md`.
- `net8.0`, `Nullable` e `ImplicitUsings` habilitados.

## Deploy

Push na branch `main` dispara deploy via **GitHub Actions** por SSH (`.github/workflows/deploy.yml`). O backend roda como serviço systemd (`DataMais/datamais.service`) atrás de nginx. Ver `DEPLOY.md`, `ATUALIZAR_SERVICO.md`, `CONFIGURAR_SECRETS.md`.

## Infra MODEC

`docs/topologia-infra-modec.md` documenta a topologia de rede da instalação (CLP Weidmüller, MK1/MK2, VM, túneis WireGuard) — referência para conectividade do CLP e publicação dos serviços.
