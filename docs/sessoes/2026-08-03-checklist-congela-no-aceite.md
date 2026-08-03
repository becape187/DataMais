# 2026-08-03 — Fluxo do laudo: checklist congela na assinatura, e volta a aparecer

Segunda frente do dia (a primeira foi PDF/assinaturas). Aqui o problema é de
**fluxo**: o laudo assinado não se comportava como documento assinado.

## O que estava acontecendo

`SalvarRespostasCampos` (`RelatorioController.cs`) tinha um trecho que, ao gravar
qualquer resposta num laudo `Concluido`, **reabria o laudo sozinho**: `Situacao`
voltava para `Rascunho` e entrava um `Reaberto` no histórico. Como o checklist
grava a cada clique (não existe botão Salvar), bastava o operador **tocar** num
radio de um laudo já assinado para:

- perder a situação de assinado,
- **perder o botão de PDF** — ele é `disabled={!concluido}`,
- ganhar uma linha "Reaberto" no histórico sem ter pedido nada.

Ou seja: o laudo assinado seguia com os campos editáveis, e a edição acidental
era silenciosa e destrutiva.

## O que mudou

### 1. Rascunho edita, Concluído congela

- `POST /relatorio/{id}/respostas-campos` agora **recusa com 409** quando
  `Situacao == "Concluido"`. O reabrir implícito saiu.
- Novo `POST /relatorio/{id}/reabrir` (`Admin,Operador`): volta para `Rascunho`,
  registra `Reaberto` com o usuário logado, idempotente se já for rascunho.
- Frontend: botão **`✎ Editar (gera vN+1)`** aparece no lugar do
  "Concluir / Assinar" quando o laudo está assinado, com confirmação que diz o
  que vai acontecer (volta a rascunho, PDF bloqueia, próxima assinatura é vN+1).

O PDF, então, fica disponível para sempre depois de assinado — nada mais o revoga
sem o operador mandar.

### 2. Checklist de laudo fechado aparecendo

Duas causas possíveis para "abro um laudo fechado e os checkboxes estão vazios",
e as duas foram corrigidas porque não dá para distinguir sem o banco do cliente:

- **O frontend descartava campo excluído.** `filter(c => !c.excluido)` jogava fora
  exatamente o que o backend manda de propósito: campo soft-deleted **que tem
  resposta neste laudo** (`RelatorioController.cs:185`). Quem respondeu um
  checklist e depois viu a pergunta sair do cadastro perdia a resposta da tela.
  Agora o filtro é `!c.excluido || tem valor`.
- **Radio marcado não rende no PDF.** O html2canvas não desenha o ponto de um
  `<input type="radio" checked>`. Em laudo assinado (ou usuário sem permissão de
  operar) o checklist virou **texto estático** — `✔ Sim` / `✕ Não` / o texto
  digitado —, o que resolve o PDF e, de quebra, torna impossível editar por engano.

### 3. Falha de gravação deixou de ser silenciosa

`salvarRespostas` só fazia `console.error`. O operador via o clique marcado na
tela, ia embora, e o laudo abria vazio depois. Agora, em erro: o valor volta ao
último que o backend confirmou (`respostasSalvasRef`) e uma faixa vermelha
aparece acima do documento (fora do `.relatorio-container`, para não entrar no PDF).

### 4. "Estado das conexões e flanges" removida

Tirada de `CamposRev02` no `DbSeeder`. Só isso não bastava — o registro já está
no banco de produção. Novo `DbSeeder.RemoverCamposDescontinuados` (chamado no
`Program.cs` logo depois do seed) faz soft-delete do campo **e apaga as respostas
dele**. As respostas vão junto de propósito: com a correção do item 2, um campo
excluído com resposta volta a aparecer — apagar a resposta é o que faz a pergunta
sumir de verdade dos laudos antigos. O histórico não se perde: a versão assinada
guarda tudo em `RelatorioVersao.RespostasJson`.

## Arquivos

- `DataMais/Controllers/RelatorioController.cs` — 409 em concluído, endpoint `reabrir`
- `DataMais/Data/DbSeeder.cs` — campo fora do seed + `RemoverCamposDescontinuados`
- `DataMais/Program.cs` — chamada do novo passo de seed
- `DataMaisWeb/src/pages/VisualizarRelatorio.tsx` — leitura estática, botão Editar,
  filtro de excluídos, erro visível
- `DataMaisWeb/src/pages/VisualizarRelatorio.css` — `.campo-relatorio-valor`, `.checklist-erro`

- `deploy-local.sh` — nota da release + conferência da pergunta retirada e dos
  laudos por situação

Sem migration: nada mudou no schema. O que mexe no banco é o seed do boot —
por isso o dump que o `deploy-local.sh` faz antes do restart importa nesta release.

## Como subir no cliente

```bash
cd <pasta-do-repo-na-VM>
git pull
./deploy-local.sh          # dump do banco → build → restart → conferências
```

Se der erro de `\r`: `sed -i 's/\r$//' deploy-local.sh`.
No fim ele confere se `Estado das conexões e flanges` saiu do banco (campo e
respostas) e lista os laudos por situação.

## Pendente de validar na bancada

- Abrir um laudo **já assinado** e conferir se o checklist mostra as respostas
  (é o sintoma original). Se ainda vier vazio, o problema é dado — as respostas
  não estão na tabela —, e aí é olhar `RespostasCampoRelatorio` do laudo direto no
  banco.
- Gerar o PDF de um laudo assinado e conferir se `✔ Sim` / `✕ Não` saem impressos.
- Assinar → Editar → mudar uma resposta → Concluir: conferir v2 no histórico e o
  PDF liberado de novo.
- Conferir no primeiro boot do serviço a linha
  `✓ 1 pergunta(s) descontinuada(s) do checklist removida(s)`.
