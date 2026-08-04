import jsPDF from 'jspdf'
import html2canvas from 'html2canvas'

// Geração do PDF do laudo.
//
// O laudo é capturado uma única vez como imagem (html2canvas) e depois recortado
// em páginas A4. O recorte NÃO é feito por régua: ele cai sempre no espaço entre
// dois blocos de topo do relatório (cabeçalho, cada `.relatorio-section`, rodapé),
// de modo que nenhuma seção — em especial o par cards+gráfico de uma câmara —
// termine cortada ao meio. Como a altura de cada bloco é praticamente fixa, a
// quebra fica previsível de um laudo para o outro.
//
// TAMANHO DO ARQUIVO — o jsPDF **descarta** a compressão do PNG que entra em
// `addImage`: ele decodifica a imagem e, se nenhuma compressão for pedida,
// regrava os pixels em RGB cru (`processPNG` → ramo `canCompress === false`).
// Sem `compress` no documento e sem o argumento `compression`, um laudo de 4
// páginas saía com ~70 MB de bytes crus e nenhum `/Filter` no arquivo inteiro.
// Por isso os dois ajustes abaixo são obrigatórios, não cosméticos.

const A4_LARGURA_MM = 210
const A4_ALTURA_MM = 297
const MARGEM_MM = 10
const RODAPE_MM = 8 // faixa reservada para a numeração de página
const LARGURA_RENDER_PX = 1200 // largura fixa de renderização (independe da janela)
const ESCALA_CAPTURA = 1.5 // 1800px sobre 190mm ≈ 240 DPI — de sobra para um laudo impresso

// Compressão Flate aplicada pelo jsPDF à imagem de cada página.
// 'FAST' = zlib nível 1 + filtro Sub · 'MEDIUM' = nível 6 + Average · 'SLOW' = nível 9 + Paeth.
// Todas são sem perda: mudam só o tamanho do arquivo e o tempo de geração.
const COMPRESSAO_IMAGEM = 'FAST' as const

const LARGURA_UTIL_MM = A4_LARGURA_MM - MARGEM_MM * 2 // 190mm
const ALTURA_UTIL_MM = A4_ALTURA_MM - MARGEM_MM * 2 - RODAPE_MM

/** Recorte de uma página, em pixels de CSS do clone renderizado. */
interface FatiaPagina {
  inicio: number
  fim: number
}

/**
 * Pontos onde é aceitável cortar: o meio do respiro entre um bloco e o próximo.
 * Sempre inclui o topo e a base do conteúdo.
 */
function calcularPontosDeCorte(clone: HTMLElement, alturaTotal: number): number[] {
  const topoClone = clone.getBoundingClientRect().top
  const blocos = Array.from(clone.children)
    .filter(el => el instanceof HTMLElement && el.offsetParent !== null)
    .map(el => {
      const r = (el as HTMLElement).getBoundingClientRect()
      return { inicio: r.top - topoClone, fim: r.bottom - topoClone }
    })
    .filter(b => b.fim > b.inicio)
    .sort((a, b) => a.inicio - b.inicio)

  const cortes = [0]
  for (let i = 0; i < blocos.length - 1; i++) {
    cortes.push((blocos[i].fim + blocos[i + 1].inicio) / 2)
  }
  cortes.push(alturaTotal)
  return cortes
}

/** Distribui o conteúdo em páginas, quebrando sempre no último corte que couber. */
function paginar(cortes: number[], alturaTotal: number, alturaPaginaPx: number): FatiaPagina[] {
  const paginas: FatiaPagina[] = []
  let inicio = 0

  while (inicio < alturaTotal - 1) {
    const limite = inicio + alturaPaginaPx

    if (limite >= alturaTotal) {
      paginas.push({ inicio, fim: alturaTotal })
      break
    }

    // Último ponto de corte que ainda cabe nesta página.
    let corte = 0
    for (const c of cortes) {
      if (c > inicio && c <= limite) corte = c
    }

    // Bloco maior que uma página inteira: corta na régua para não travar.
    if (corte === 0) corte = limite

    paginas.push({ inicio, fim: corte })
    inicio = corte
  }

  return paginas.length > 0 ? paginas : [{ inicio: 0, fim: alturaTotal }]
}

/** Extrai um pedaço horizontal da captura como um canvas próprio. */
function recortar(origem: HTMLCanvasElement, topo: number, altura: number): HTMLCanvasElement {
  const fatia = document.createElement('canvas')
  fatia.width = origem.width
  fatia.height = Math.max(1, Math.round(altura))

  const ctx = fatia.getContext('2d')
  if (ctx) {
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, fatia.width, fatia.height)
    ctx.drawImage(
      origem,
      0, Math.round(topo), origem.width, fatia.height,
      0, 0, fatia.width, fatia.height
    )
  }

  return fatia
}

/**
 * Monta o PDF A4 do laudo a partir do elemento na tela.
 * `identificacao` vai no rodapé de cada página, ao lado do "Página X de Y".
 */
export async function gerarRelatorioPdf(origem: HTMLElement, identificacao: string): Promise<jsPDF> {
  const clone = origem.cloneNode(true) as HTMLElement

  clone.style.position = 'absolute'
  clone.style.left = '-9999px'
  clone.style.top = '0'
  clone.style.width = `${LARGURA_RENDER_PX}px`
  clone.style.maxWidth = `${LARGURA_RENDER_PX}px`
  clone.style.transform = 'none'
  clone.style.transformOrigin = 'top left'
  clone.style.boxShadow = 'none'

  // Botões de exportação não entram no documento.
  clone.querySelectorAll('.export-button, .export-buttons').forEach(btn => {
    ;(btn as HTMLElement).style.display = 'none'
  })

  // Blocos que existem só na tela (ex.: Histórico de Versões) saem do documento.
  // Removidos do DOM, não escondidos: assim não sobra o espaçamento do bloco entre
  // as seções vizinhas — o `gap` do container só some quando o filho deixa de existir.
  clone.querySelectorAll('.fora-do-pdf').forEach(el => el.remove())

  document.body.appendChild(clone)

  try {
    // Dois frames: um para o layout, outro para o reflow das larguras fixas.
    await new Promise(requestAnimationFrame)
    await new Promise(requestAnimationFrame)

    const alturaTotalPx = clone.scrollHeight
    const cortes = calcularPontosDeCorte(clone, alturaTotalPx)

    const canvas = await html2canvas(clone, {
      scale: ESCALA_CAPTURA,
      useCORS: true,
      logging: false,
      backgroundColor: '#ffffff',
      width: LARGURA_RENDER_PX,
      windowWidth: LARGURA_RENDER_PX
    })

    // Relação real entre a captura e o layout medido (não presume a escala).
    const razao = canvas.height / alturaTotalPx
    const mmPorPx = LARGURA_UTIL_MM / LARGURA_RENDER_PX
    const alturaPaginaPx = ALTURA_UTIL_MM / mmPorPx

    const paginas = paginar(cortes, alturaTotalPx, alturaPaginaPx)

    // `compress: true` cobre os streams de conteúdo; a imagem depende do
    // argumento passado no addImage abaixo.
    const pdf = new jsPDF({ orientation: 'p', unit: 'mm', format: 'a4', compress: true })

    paginas.forEach((pagina, i) => {
      if (i > 0) pdf.addPage()

      const alturaFatiaPx = pagina.fim - pagina.inicio
      const fatia = recortar(canvas, pagina.inicio * razao, alturaFatiaPx * razao)

      pdf.addImage(
        fatia.toDataURL('image/png', 1.0),
        'PNG',
        MARGEM_MM,
        MARGEM_MM,
        LARGURA_UTIL_MM,
        alturaFatiaPx * mmPorPx,
        undefined, // alias: cada fatia é uma imagem distinta, não há o que reaproveitar
        COMPRESSAO_IMAGEM
      )
    })

    // Numeração — só faz sentido quando o laudo passa de uma folha.
    if (paginas.length > 1) {
      for (let i = 1; i <= paginas.length; i++) {
        pdf.setPage(i)
        pdf.setFontSize(8)
        pdf.setTextColor(130)
        pdf.text(identificacao, MARGEM_MM, A4_ALTURA_MM - 6)
        pdf.text(
          `Página ${i} de ${paginas.length}`,
          A4_LARGURA_MM - MARGEM_MM,
          A4_ALTURA_MM - 6,
          { align: 'right' }
        )
      }
    }

    return pdf
  } finally {
    if (clone.parentNode) clone.parentNode.removeChild(clone)
  }
}
