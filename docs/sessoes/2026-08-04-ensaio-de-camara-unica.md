# 2026-08-04 — Ensaio de câmara única, e o Histórico de Versões fora do PDF

Duas frentes pequenas e independentes, pedidas na mesma conversa.

## 1. Histórico de Versões sai do PDF (fica na tela)

O bloco é um `.relatorio-section` filho direto de `.relatorio-container` — que é
justamente o elemento capturado pelo `html2canvas`. Ele existe para o pessoal do
laboratório ver quem assinou e quem reabriu; no documento entregue ao cliente é
ruído.

- `VisualizarRelatorio.tsx` — a seção ganhou a classe **`fora-do-pdf`**.
- `relatorioPdf.ts` — antes da captura, `clone.querySelectorAll('.fora-do-pdf').forEach(el => el.remove())`.

**Removido do DOM, não escondido.** `display:none` também sairia da captura (o
`calcularPontosDeCorte` filtra por `offsetParent`), mas o `gap` do container
continuaria reservando o espaçamento do bloco entre as seções vizinhas.

Qualquer outro bloco que deva viver só na tela é só marcar com a mesma classe.

## 2. Checkbox por câmara — ensaio (e laudo) com uma câmara só

Até aqui "o ensaio tem sempre as duas câmaras" estava **cravado em quatro
lugares** como `new[] { "A", "B" }`: `EnsaioController.AtualizarStatusEnsaio`,
o `podeAceitar` do `MontarDto`, o gate de `AceitarEnsaio` e
`RegistroConclusaoMonitor.ConcluirEtapaAsync`. Um cilindro que só precisava de
uma câmara não tinha saída: o ensaio nunca chegava a `AguardandoAceite`.

### Modelo

`Ensaio.CamaraAHabilitada` / `CamaraBHabilitada` (bool NOT NULL) e a propriedade
`[NotMapped] CamarasHabilitadas`, que é quem os quatro pontos passam a consultar.

Migration **`AddCamarasHabilitadasEnsaio`**. O scaffold do EF gerou
`defaultValue: false` nas duas colunas e isso foi **corrigido à mão para
`true`** — com `false`, todo ensaio já existente acordaria sem nenhuma câmara
habilitada e não poderia mais ser aceito. (Detalhe traiçoeiro: `All` sobre lista
vazia é `true`, então esses ensaios iriam para `AguardandoAceite` e só falhariam
no aceite.) O default ficou só na migration, de propósito: com
`HasDefaultValue(true)` no modelo, o EF trataria `false` como "não informado" no
INSERT e o banco gravaria `true` por cima.

### Endpoint

`PUT /ensaio/{id}/camaras` → `{ camaraAHabilitada, camaraBHabilitada }`

- Recusa ensaio fora de `EmAndamento`/`AguardandoAceite`.
- Recusa as duas desmarcadas.
- Recusa (409) desmarcar câmara **em execução** — encerrar é ato próprio, com o
  registro desligado na sequência certa.
- **Desmarcar câmara que já rodou descarta as corridas dela**: `Concluida`/
  `Repetida` viram `Descartada` e as leituras saem do Influx. Foi a decisão
  explícita do usuário — "pode não querer levar esse ensaio adiante e descartar".
- `IniciarEtapa` recusa câmara desmarcada.

A remoção no Influx passa **`TimeSpan.Zero`** de folga (`RemoverLeiturasInfluxAsync`
ganhou o parâmetro). O padrão de ±1 min existe para não deixar pontas ao
descartar, mas aqui a corrida da outra câmara costuma começar segundos depois —
a folga levaria junto o começo dela.

### Tela

- Checkbox "Incluir a câmara X neste ensaio" no card de cada câmara, marcado por
  padrão. Desabilitado enquanto a câmara roda e na última câmara marcada.
- Câmara desmarcada: card apagado (`.camara-fora`), sem botão de iniciar,
  com a linha "Não será ensaiada e não entra no laudo".
- Desmarcar câmara **que já rodou** abre modal dizendo quantas corridas serão
  descartadas.
- **Modal no aceite** quando só uma câmara está marcada: nomeia a câmara que
  entra, a que fica de fora, e lembra que o número REH-MPR é queimado ali.

### Laudo

Não precisou de tratamento especial — `EtapasValidas` devolve uma etapa,
`CombinarResultados` julga só ela e o card "Câmaras Testadas" já sai com "A".
Mudaram dois textos:

- A observação gravada no aceite: `(somente câmara A)` em vez de `(câmaras A e B)`.
- O critério abaixo do resultado, que afirmava "O ensaio é aprovado somente se as
  duas câmaras forem aprovadas" — em laudo de uma câmara vira "Ensaio realizado
  somente na câmara A — o veredito considera apenas ela."

## Pendente de validar na bancada

- Ensaio novo: as duas câmaras entram marcadas.
- Desmarcar a B sem ter rodado → o aceite libera com a A concluída e sobe o modal.
- Desmarcar uma câmara **já rodada** → conferir no gráfico do laudo/Influx que as
  leituras dela sumiram e que **a corrida da outra câmara continuou inteira**
  (é o ponto que a folga zero protege).
- PDF de um laudo assinado: sem a seção Histórico de Versões, e conferir se a
  paginação não ficou com sobra estranha na última página.
