# 2026-07-31 — PDF do laudo: repetição das páginas e quebra por seção

Continuação do dia. Olhamos o PDF exportado (`docs/relatorio_REH-MPR-0000001-2026.pdf`,
do laudo REH-MPR-0000001-2026) e ele estava **imprestável**: 3 páginas idênticas, o
relatório inteiro achatado em cada uma.

## Causa

`VisualizarRelatorio.tsx` capturava o laudo num único canvas e fatiava assim:

```ts
pdf.addImage(canvas, 'PNG', margin, margin, imgWidth, pageImgHeight)  // toda página
```

`addImage` **redimensiona a imagem inteira** para a caixa informada — não recorta.
Como `position` voltava para `margin` a cada página e só a altura da caixa mudava,
cada folha recebia o relatório completo comprimido. O jeito clássico (deslocar a
imagem para cima com `position = margin - i * alturaPagina`) resolveria a repetição,
mas cortaria no meio de gráfico/cards.

## Correção — `DataMaisWeb/src/utils/relatorioPdf.ts` (novo)

Recorte de verdade, e no lugar certo:

- Antes de capturar, mede-se cada bloco de topo do laudo (cabeçalho,
  cada `.relatorio-section`, rodapé) no clone de 1200px.
- Os pontos de corte candidatos são o **meio do respiro entre dois blocos** —
  nunca dentro de um.
- Paginação gulosa: a página vai até o último corte que couber na área útil
  (A4 − margens − faixa do rodapé). Bloco maior que uma folha inteira ainda
  corta na régua, só para não travar.
- Cada página é um `<canvas>` recortado da captura (`drawImage` com retângulo de
  origem) e vai ao PDF na proporção real — sem esticar nada.
- Rodapé discreto com `REH-… · cliente` e `Página X de Y`, só quando passa de
  uma folha.
- A razão captura/layout é medida (`canvas.height / clone.scrollHeight`), não
  presumida a partir da escala.

`exportarParaPDF` e `imprimirRelatorio` eram ~95 linhas duplicadas cada uma;
agora as duas chamam `gerarRelatorioPdf()` e só divergem no destino (download x
janela de impressão).

## Paginação resultante (simulada com as alturas medidas deste laudo)

| Pág | Conteúdo | Ocupação |
|-----|----------|----------|
| 1 | Cabeçalho + Ident. Documento + Ident. Equipamento + Resultado do Ensaio | 209mm |
| 2 | Câmara A + Câmara B (cards + gráfico inteiros) | 261mm |
| 3 | Observações + Inspeção Visual + Testes Funcionais | 269mm |
| 4 | Condições Finais + Histórico de Versões + assinatura | 128mm |

Nenhuma seção partida, nenhuma página estourando a área útil. Como a altura dos
blocos é praticamente fixa de um laudo para o outro, essa divisão se repete —
muda só quando o ensaio tem uma câmara só ou o checklist cresce.

## Pendente de validar

- Exportar/imprimir um laudo real na VM e conferir as 4 folhas (principalmente
  se o gráfico de cada câmara sai inteiro).
- Laudo de uma câmara só e laudo com muitas perguntas adicionais: conferir se o
  rodapé "Página X de Y" e a última folha ficam bem.
