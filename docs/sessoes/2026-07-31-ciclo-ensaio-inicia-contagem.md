# 2026-07-31 — Ciclo do ensaio: INICIA_CONTAGEM, trava de pressão, encerramento automático

Sessão longa, com testes em bancada no cliente entre os deploys.

## DESFECHO — a lição da sessão (ler primeiro)

Teste final de bancada: **encerramento automático OK**; contagem não iniciava.
Causa: a "máquina de estados" defensiva do INICIA_CONTAGEM (borda obrigatória,
anti-retenção, reabertura, retalho/vale) — proteções desenhadas para leituras
não-confiáveis, que deixaram de existir quando a serialização do Modbus entrou.
O CLP do cliente é comportado: o flag **nunca** está ligado na partida e só liga
ao atingir a pressão de teste.

Decisão do Bernardo, literal: **"ligou o flag, inicia contagem" — somente isso.**
A máquina de estados foi REMOVIDA; a regra é por NÍVEL: ligou = t0, desligou =
fim, janela medida uma vez por etapa, flag religado depois vira só log. Está no
CLAUDE.md como regra permanente: não readicionar esperteza ali.

Segunda diretriz de fluxo, também permanente: **nenhuma mudança "no escuro"** —
antes de mexer, explicar o que será mudado e por quê; diagnóstico com prova
(journalctl/sinais-clp) vem antes de correção.

## E o último 00:00:00 — relógio do PC da bancada

Após a regra por nível, o teste mostrou o badge "Contando" (t0 CARIMBADO ✓) e
MESMO ASSIM "Tempo de teste" E "Desde a partida" em 00:00:00 — com o gráfico
andando normalmente. Prova: o gráfico usa horários formatados NO SERVIDOR; os
cronômetros eram `Date.now() do PC − timestamp do servidor`. O PC da bancada tem
relógio/fuso deslocado → conta negativa → `formatarDuracao` clampa em zero.
TODOS os "00:00:00" da sessão tinham essa componente.

Correção: os endpoints de polling devolvem `agoraServidor`; a tela mede o offset
(`agoraServidor − Date.now()`) a cada resposta e calcula TODO tempo decorrido no
relógio do servidor (`agoraCorrigido = tique local + offset`). Os cronômetros
ficam imunes ao relógio do PC. Regra: nunca comparar timestamp do servidor com
`Date.now()` cru em tela nova. Vale conferir mesmo assim o fuso/hora do PC da
bancada.

## O que foi construído

### Trava de pressão na partida (`def4b28`)
- Iniciar câmara com qualquer uma das câmaras ≥ **3 bar** é recusado (409 + modal
  na tela). Leitura que falha também bloqueia — sem confirmar cilindro aliviado,
  não parte. Constante `PressaoMaximaParaIniciarBar` no `EnsaioController`.
- O alarme aparece **só no clique de iniciar, como modal** (a tarja fixa que
  ficava na tela foi removida a pedido do operador).
- `GET /ensaio/pressoes` lê as pressões com a bancada parada (tela mostra).

### Partida bloqueante (`def4b28`)
- `POST /ensaio/{id}/etapa` agora só responde sucesso depois de **confirmar por
  leitura que o REGISTRO_RODANDO subiu** (timeout 5s). Não confirmou → etapa é
  removida do banco e coils desligados. Spinner "Confirmando no CLP…" na tela.

### INICIA_CONTAGEM → t0 do laudo (`def4b28`, refinado até `8273b1e`)
- Colunas novas `EnsaioEtapas.DataInicioContagem/DataFimContagem` (migration
  `AddJanelaContagemEnsaioEtapa` — aplica sozinha no restart).
- **Papel de cada sinal** (definido pelo Bernardo): `REGISTRO_RODANDO` inicia e
  para o registro — a descida dele, sozinha, conclui a etapa. `INICIA_CONTAGEM`
  manda **só na contagem de tempo** (t0 do laudo e cronômetro da tela).
- O laudo analisa a janela `[DataInicioContagem, DataFimContagem]` exata (sem a
  margem antiga de ±1min). Ensaios sem o registro caem na regra antiga (t0 =
  primeiro ponto que atinge o setpoint).

### Exclusão de laudo (`def4b28`)
- `DELETE /relatorio/{id}` — só **Admin**, só **Rascunho** (concluído/assinado →
  409). Leva junto: respostas do checklist, versões, ensaio (vira Cancelado) e
  leituras no Influx. O número REH-MPR **não** volta — a sequência fica com
  buraco, de propósito. Botão "🗑 Excluir laudo" na tela do laudo.

### Tela do ensaio
- Código `ENSAIO-20260731-134721` não aparece mais para o operador (parecia
  numeração errada de laudo) — virou "Ensaio de 31/07/2026 13:47"; o REH-MPR
  nasce no aceite.
- Painel com **REGISTRO_RODANDO** e **INICIA_CONTAGEM** ao vivo (● Ligado /
  ○ Desligado / ⚠ falha / ⚠ leitura de Ns atrás) — alimentado pelo cache do
  monitor, zero Modbus extra. "Tempo de teste" só anda com a contagem do CLP.

## As três causas raiz que fizeram os sintomas sobreviverem a 4 deploys

### 1. Concorrência no socket Modbus (`e1fe3f1`)
O `IModbusMaster` do NModbus **não é thread-safe** e nada serializava: monitor
(1 Hz) + polling de pressões da tela (1 Hz) no MESMO socket TCP. Frames se
entrelaçavam e — como REGISTRO_RODANDO e INICIA_CONTAGEM são ambos 0x02 com
resposta idêntica — **a resposta de um era entregue ao outro** (contagem
"ligando sozinha"). O retry ainda dava Dispose na conexão debaixo da transação
alheia. Correção: `SemaphoreSlim` por conexão (`ExecutarNaConexaoAsync` é o
único caminho até o socket), remoção de conexão só dentro do semáforo,
`Transport.Retries = 0` (o retry interno do NModbus segurava o semáforo ~40s
contra CLP travado — medido), fila com teto de 30s.
**Regra permanente: nunca tocar `master.*` fora do invólucro.**

### 2. Estado de borda em memória × deploys (`e1fe3f1`)
Cada deploy reinicia o serviço; o monitor acordava vendo `rodando=false` sem
nunca ter visto `true` → borda de descida nunca existia → etapa presa em
EmExecucao. **Cada deploy de correção reproduzia o bug.** Correção: etapa
aberta >30s com sinal desligado por 5 ciclos seguidos conclui mesmo sem borda
(falha de leitura não conta como desligado).

### 3. O "bobo e simples": a tela engolia a conclusão (`8273b1e`)
Quando o monitor conclui a etapa, `/ensaio/ativo` passa a responder
`{ ativo: false }` — e o guard do polling da tela fazia `return` exatamente
nesse caso. **O backend encerrava; a tela nunca ficava sabendo** e seguia em
"Rodando" até o operador clicar Encerrar (que respondia "Etapa já encerrada"
como sucesso, mascarando tudo). Existia desde o rework das duas câmaras.
Correção: ativo sumiu com câmara na tela → `GET /ensaio/{id}` busca o estado
final e loga "CLP concluiu a câmara X".

## Máquina de estados da janela de contagem (`8273b1e`)

Sintoma de bancada: INICIA_CONTAGEM `● Ligado` com cronômetro congelado em
00:00:00 — o sinal estava **retido** na partida (t0 carimbado na hora), caiu na
rampa (janela fechada com ~0s) e a subida real era ignorada. Regras atuais:

- t0 **só na borda de subida**. Retenção na partida é ignorada com log.
  Única aceitação por nível: primeira observação de etapa >30s (restart do
  backend no meio do patamar).
- Sinal voltando após queda: janela ≤5s = retalho → descarta e recomeça t0;
  vale ≤5s no meio de patamar real → retoma mantendo t0; **re-subida tardia
  após patamar medido → ignorada** (ex.: recuo, que pressuriza a câmara oposta
  de propósito — fundir faria o laudo reprovar cilindro sem passagem).
- Glitch de leitura não apaga `_contagemAnterior` (não disfarça a borda real).
- Carimbos são `ExecuteUpdateAsync` condicionado a `Status = EmExecucao`
  (encerramento manual no meio do ciclo não deixa t0 > DataFim órfão).
- Laudo: janela invertida (dado histórico corrompido) → descarta com log e usa
  a regra do setpoint.

## Diagnóstico disponível

- **Tela do ensaio**: painel dos dois sinais, sempre visível.
- `GET /api/ensaio/sinais-clp`: cadastro, valor bruto, interpretação, erro,
  última leitura do monitor e a **janela carimbada** da etapa em execução.
  Com bancada parada, os dois sinais têm que vir `ligado=false`.
- `journalctl -u datamais.service`: "INICIA_CONTAGEM subiu/caiu/voltou a
  ligar", "REGISTRO_RODANDO caiu: concluindo", "concluindo … descida não foi
  observada".

## Pendente de validar em bancada (próximo ensaio)

1. Partida: contagem NÃO deve iniciar sozinha (mesmo com sinal retido).
2. Patamar: `INICIA_CONTAGEM ● Ligado` e "Tempo de teste" andando (≤2s de atraso).
3. Fim do ciclo: tela marca a câmara Concluída **sozinha** em ≤3s, com
   "CLP concluiu a câmara X" no log de eventos.
4. Laudo: janela de análise = patamar (sem rampa, sem recuo).

## Pontos em aberto (conhecidos, não são bug)

- **Coleta de pressão depende da tela aberta**: a única escrita no Influx é o
  polling do navegador (`/pressao-atual`). Aba fechada durante a corrida =
  janela de contagem correta porém vazia → veredito `-`. Próxima melhoria
  natural: coletor em background.
- Sequência REH-MPR fica com buracos quando um laudo rascunho é excluído (por
  design; rever se a auditoria exigir sequência contínua).
- Verificações multi-agente já apontaram (não corrigido por ser raro): recuo
  que religa INICIA_CONTAGEM gera log e é ignorado — se o CLP algum dia fizer
  dois patamares legítimos numa etapa, precisará de múltiplos segmentos de
  janela (tabela filha), não do par único atual.
