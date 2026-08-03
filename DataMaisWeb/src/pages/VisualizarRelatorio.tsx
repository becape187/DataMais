import { useEffect, useState, useRef } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import * as XLSX from 'xlsx'
import api from '../config/api'
import { useAuth } from '../contexts/AuthContext'
import { formatarDuracao } from '../utils/tempo'
import { gerarRelatorioPdf } from '../utils/relatorioPdf'
import './VisualizarRelatorio.css'

interface CampoRelatorio {
  id: number
  nome: string
  tipoResposta: 'SimOuNao' | 'TextoSimples' | 'MultiplasLinhas'
  secao?: string | null
  reprovaSeSim?: boolean
  ordem: number
  respostaId?: number | null
  valor?: string | null
  excluido?: boolean
}

/** Uma câmara do ensaio, já avaliada pelo backend na sua janela de tempo. */
interface EtapaRelatorio {
  etapaId: number
  camara: 'A' | 'B'
  tentativa: number
  dataInicio: string
  dataFim: string | null
  pressaoCargaConfigurada: number
  tempoCargaConfigurado: number
  pressaoMinima?: number | null
  pressaoMaxima?: number | null
  pressaoMedia?: number | null
  pressaoMaximaCamaraOposta?: number | null
  limitePassagem?: number | null
  resultado?: string | null
}

/** Série de pressão de uma câmara (A e B juntas — é o par que mostra a passagem). */
interface SerieEtapa {
  etapaId: number
  camara: 'A' | 'B'
  tentativa: number
  dados: DataPoint[]
}

interface RelatorioDetalhe {
  id: number
  numero: string
  /** Vessel/Frota — vem do cadastro (clienteNome); não existe cliente externo. */
  cliente: string
  data: string
  ensaioId?: number | null
  observacoes?: string | null
  duracao?: string | null
  etapas: EtapaRelatorio[]
  resultado?: string | null
  campos?: CampoRelatorio[]
  // Ciclo de vida / assinatura (rev02)
  situacao?: string | null
  versao?: number | null
  concluidoPorNome?: string | null
  dataConclusao?: string | null
  // Identificação do documento
  ensaioLocalTeste?: string | null
  ensaioDepartamento?: string | null
  ensaioOrdemServico?: string | null
  // Identificação do equipamento
  cilindroCodigoSap?: string | null
  cilindroTagId?: string | null
  cilindroFabricante?: string | null
  cilindroNumeroSerie?: string | null
  cilindroPartNumber?: string | null
  cilindroFluido?: string | null
  cilindroDiametroInterno?: number | null
  cilindroDiametroHaste?: number | null
  cilindroMaximaPressaoA?: number | null
  cilindroMaximaPressaoB?: number | null
}

interface VersaoHistorico {
  id: number
  numeroVersao: number
  acao: string
  usuarioNome?: string | null
  dataHora: string
  resultado?: string | null
}

interface DataPoint {
  time: string
  pressaoA?: number | null
  pressaoB?: number | null
}

// Quadro de assinaturas manuais do laudo (modelo MODEC).
// Vão dois papéis por linha — os quatro lado a lado espremiam as linhas a ponto
// de não caber nem caneta nem o carimbo de uma assinatura gov.br.
const BLOCOS_ASSINATURA = [
  {
    papel: 'Executor do Ensaio',
    campos: [{ rotulo: 'Nome' }, { rotulo: 'Assinatura', alto: true }, { rotulo: 'Data' }]
  },
  {
    papel: 'Supervisor / Coordenador',
    campos: [{ rotulo: 'Nome' }, { rotulo: 'Assinatura', alto: true }, { rotulo: 'Data' }]
  },
  {
    papel: 'PIC – Engineer',
    campos: [{ rotulo: 'Nome' }, { rotulo: 'Assinatura', alto: true }, { rotulo: 'Data' }]
  },
  {
    papel: 'Classificadora',
    observacao: 'quando aplicável',
    campos: [{ rotulo: 'Empresa' }, { rotulo: 'Surveyor' }, { rotulo: 'Assinatura', alto: true }]
  }
]

const VisualizarRelatorio = () => {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { podeOperar, isAdmin } = useAuth()
  const [relatorio, setRelatorio] = useState<RelatorioDetalhe | null>(null)
  const [loading, setLoading] = useState(true)
  const [seriesEtapas, setSeriesEtapas] = useState<SerieEtapa[]>([])
  const [loadingGrafico, setLoadingGrafico] = useState(true)
  const [respostasCampos, setRespostasCampos] = useState<Record<number, string>>({})
  const [versoes, setVersoes] = useState<VersaoHistorico[]>([])
  const [concluindo, setConcluindo] = useState(false)
  const [reabrindo, setReabrindo] = useState(false)
  const [erroChecklist, setErroChecklist] = useState<string | null>(null)
  const [modalExcluir, setModalExcluir] = useState(false)
  const [excluindo, setExcluindo] = useState(false)
  const relatorioContainerRef = useRef<HTMLDivElement>(null)
  /** Último valor que o backend confirmou para cada campo — é para ele que a tela volta se a gravação falhar. */
  const respostasSalvasRef = useRef<Record<number, string>>({})

  const concluido = relatorio?.situacao === 'Concluido'

  // O veredito de pressão de cada câmara vem pronto do backend (ele lê o InfluxDB na
  // janela da etapa). Aqui só aplicamos por cima o override do checklist, que o operador
  // pode mudar sem recarregar a página.
  //
  // Regra do laudo: em cada câmara, a partir do instante em que ela atinge o setpoint,
  // mede-se o PICO da câmara OPOSTA; se passar do limite do cilindro, é passagem →
  // Reprovado. Uma câmara reprovada reprova o ensaio inteiro. Qualquer checklist
  // "reprova se Sim" respondido "Sim" sobrepõe tudo.
  const reprovadoPorChecklist = (relatorio?.campos || []).some(
    c => c.reprovaSeSim && respostasCampos[c.id]?.trim().toLowerCase() === 'sim'
  )

  const resultadoCalculado: string | null = reprovadoPorChecklist
    ? 'Reprovado'
    : relatorio?.resultado ?? null

  const serieDaEtapa = (etapaId: number): DataPoint[] =>
    seriesEtapas.find(s => s.etapaId === etapaId)?.dados ?? []

  const totalPontos = seriesEtapas.reduce((soma, s) => soma + s.dados.length, 0)

  useEffect(() => {
    const carregarRelatorio = async () => {
      if (!id) return

      try {
        const response = await api.get(`/Relatorio/${id}`)
        const r = response.data

        const dataStr = new Date(r.data).toLocaleString('pt-BR')

        let duracao: string | null = null
        if (r.ensaioDataInicio && r.ensaioDataFim) {
          const inicio = new Date(r.ensaioDataInicio)
          const fim = new Date(r.ensaioDataFim)
          const diffMs = fim.getTime() - inicio.getTime()
          if (diffMs > 0) {
            duracao = formatarDuracao(diffMs / 1000)
          }
        }

        // Inicializa respostas dos campos
        const respostas: Record<number, string> = {}
        if (r.campos && Array.isArray(r.campos)) {
          r.campos.forEach((campo: CampoRelatorio) => {
            if (campo.valor != null) {
              respostas[campo.id] = campo.valor
            }
          })
        }
        setRespostasCampos(respostas)
        respostasSalvasRef.current = { ...respostas }

        setRelatorio({
          id: r.id,
          numero: r.numero,
          cliente: r.clienteNome || 'N/A',
          data: dataStr,
          ensaioId: r.ensaioId ?? null,
          observacoes: r.observacoes ?? null,
          duracao,
          etapas: Array.isArray(r.etapas) ? r.etapas : [],
          resultado: r.resultado ?? null,
          campos: r.campos || [],
          situacao: r.situacao ?? 'Rascunho',
          versao: r.versao ?? 0,
          concluidoPorNome: r.concluidoPorNome ?? null,
          dataConclusao: r.dataConclusao ?? null,
          ensaioLocalTeste: r.ensaioLocalTeste ?? null,
          ensaioDepartamento: r.ensaioDepartamento ?? null,
          ensaioOrdemServico: r.ensaioOrdemServico ?? null,
          cilindroCodigoSap: r.cilindroCodigoSap ?? null,
          cilindroTagId: r.cilindroTagId ?? null,
          cilindroFabricante: r.cilindroFabricante ?? null,
          cilindroNumeroSerie: r.cilindroNumeroSerie ?? null,
          cilindroPartNumber: r.cilindroPartNumber ?? null,
          cilindroFluido: r.cilindroFluido ?? null,
          cilindroDiametroInterno: r.cilindroDiametroInterno ?? null,
          cilindroDiametroHaste: r.cilindroDiametroHaste ?? null,
          cilindroMaximaPressaoA: r.cilindroMaximaPressaoA ?? null,
          cilindroMaximaPressaoB: r.cilindroMaximaPressaoB ?? null,
        })
      } catch (err) {
        console.error('Erro ao carregar relatório:', err)
      } finally {
        setLoading(false)
      }
    }

    carregarRelatorio()
  }, [id])

  // Carrega os dados do gráfico
  useEffect(() => {
    const carregarDadosGrafico = async () => {
      if (!id) return

      try {
        setLoadingGrafico(true)
        const response = await api.get(`/Relatorio/${id}/dados-grafico`)
        setSeriesEtapas(Array.isArray(response.data.etapas) ? response.data.etapas : [])
      } catch (err) {
        console.error('Erro ao carregar dados do gráfico:', err)
        setSeriesEtapas([])
      } finally {
        setLoadingGrafico(false)
      }
    }

    if (relatorio && relatorio.ensaioId) {
      carregarDadosGrafico()
    } else {
      setLoadingGrafico(false)
    }
  }, [id, relatorio])

  const carregarVersoes = async () => {
    if (!id) return
    try {
      const resp = await api.get(`/Relatorio/${id}/versoes`)
      setVersoes(Array.isArray(resp.data) ? resp.data : [])
    } catch (err) {
      console.error('Erro ao carregar versões:', err)
    }
  }

  useEffect(() => {
    if (id) carregarVersoes()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  // Grava uma resposta do checklist. Só vale em Rascunho — laudo assinado é recusado
  // pelo backend (409) e a edição passa a exigir reabertura explícita.
  //
  // Falha de gravação NÃO pode passar em silêncio: o operador via o clique marcado na tela
  // e o laudo abria vazio depois. Em erro, o valor anterior volta e ele é avisado.
  const salvarRespostas = async (campoId: number, valor: string, valorAnterior: string) => {
    if (!id || !relatorio) return

    try {
      await api.post(`/Relatorio/${id}/respostas-campos`, {
        respostas: [{ campoRelatorioId: campoId, valor: valor || null }]
      })
      respostasSalvasRef.current[campoId] = valor
      setErroChecklist(null)
    } catch (error: any) {
      console.error('Erro ao salvar resposta:', error)
      setRespostasCampos(prev => ({ ...prev, [campoId]: valorAnterior }))
      setErroChecklist(
        error?.response?.data?.message || 'Não foi possível salvar a resposta. Verifique a conexão e tente de novo.'
      )
    }
  }

  // Reabertura explícita de um laudo assinado: volta para Rascunho e a próxima conclusão
  // grava a versão seguinte. Antes isso acontecia sozinho no primeiro clique do checklist.
  const reabrirRelatorio = async () => {
    if (!id || !relatorio) return
    const proxima = (relatorio.versao ?? 0) + 1
    if (!window.confirm(
      `Reabrir o laudo ${relatorio.numero} para edição?\n\n` +
      `Ele volta para rascunho e o PDF fica bloqueado até ser concluído de novo — ` +
      `quando será assinado como v${proxima}.`
    )) return

    try {
      setReabrindo(true)
      const resp = await api.post(`/Relatorio/${id}/reabrir`)
      setRelatorio(prev => (prev ? { ...prev, situacao: resp.data?.situacao ?? 'Rascunho' } : prev))
      setErroChecklist(null)
      await carregarVersoes()
    } catch (err: any) {
      alert(err.response?.data?.message || 'Erro ao reabrir o laudo')
    } finally {
      setReabrindo(false)
    }
  }

  const concluirRelatorio = async () => {
    if (!id) return
    if (!window.confirm('Concluir e assinar este relatório? Isso libera a geração do PDF.')) return
    try {
      setConcluindo(true)
      const resp = await api.post(`/Relatorio/${id}/concluir`)
      setRelatorio(prev => (prev ? {
        ...prev,
        situacao: resp.data?.situacao ?? 'Concluido',
        versao: resp.data?.versao ?? prev.versao,
        concluidoPorNome: resp.data?.concluidoPorNome ?? prev.concluidoPorNome,
        dataConclusao: resp.data?.dataConclusao ?? prev.dataConclusao,
      } : prev))
      await carregarVersoes()
    } catch (err: any) {
      alert(err.response?.data?.message || 'Erro ao concluir relatório')
    } finally {
      setConcluindo(false)
    }
  }

  const excluirRelatorio = async () => {
    if (!id) return

    setExcluindo(true)

    try {
      await api.delete(`/Relatorio/${id}`)
      navigate('/relatorios', { replace: true })
    } catch (err: any) {
      alert(err.response?.data?.message || 'Erro ao excluir o laudo')
      setExcluindo(false)
      setModalExcluir(false)
    }
  }

  // Função para exportar dados para CSV
  const exportarParaCSV = () => {
    if (totalPontos === 0) {
      alert('Não há dados para exportar')
      return
    }

    // A câmara entra como coluna: as duas corridas vão no mesmo arquivo
    const cabecalho = ['Câmara', 'Tempo', 'Pressão A (bar)', 'Pressão B (bar)']

    const linhas = seriesEtapas.flatMap(serie =>
      serie.dados.map(ponto => [
        serie.camara,
        ponto.time,
        ponto.pressaoA != null ? ponto.pressaoA.toFixed(2) : '',
        ponto.pressaoB != null ? ponto.pressaoB.toFixed(2) : ''
      ])
    )

    // Combina cabeçalho e dados
    const csvContent = [
      cabecalho.join(','),
      ...linhas.map(linha => linha.join(','))
    ].join('\n')

    // Adiciona BOM para UTF-8 (suporta caracteres especiais)
    const BOM = '\uFEFF'
    const blob = new Blob([BOM + csvContent], { type: 'text/csv;charset=utf-8;' })
    
    // Cria link de download
    const link = document.createElement('a')
    const url = URL.createObjectURL(blob)
    link.setAttribute('href', url)
    link.setAttribute('download', `relatorio_${relatorio?.numero || 'dados'}_pontos_coletados.csv`)
    link.style.visibility = 'hidden'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
  }

  // Função para exportar dados para XLS
  const exportarParaXLS = () => {
    if (totalPontos === 0) {
      alert('Não há dados para exportar')
      return
    }

    // Uma aba por câmara — cada corrida é uma prova independente
    const wb = XLSX.utils.book_new()

    seriesEtapas.forEach(serie => {
      const dadosPlanilha = serie.dados.map(ponto => ({
        'Tempo': ponto.time,
        'Pressão A (bar)': ponto.pressaoA != null ? ponto.pressaoA : '',
        'Pressão B (bar)': ponto.pressaoB != null ? ponto.pressaoB : ''
      }))

      const ws = XLSX.utils.json_to_sheet(dadosPlanilha)
      ws['!cols'] = [{ wch: 15 }, { wch: 18 }, { wch: 18 }]

      XLSX.utils.book_append_sheet(wb, ws, `Câmara ${serie.camara}`)
    })

    // Gera o arquivo e faz o download
    XLSX.writeFile(wb, `relatorio_${relatorio?.numero || 'dados'}_pontos_coletados.xlsx`)
  }

  // Identificação que vai no rodapé de cada página do PDF
  const rodapePdf = () =>
    [relatorio?.numero, relatorio?.cliente].filter(Boolean).join(' · ') || 'Relatório de Ensaio Hidráulico'

  // Função para exportar relatório para PDF
  const exportarParaPDF = async () => {
    if (!relatorioContainerRef.current) {
      alert('Erro ao gerar PDF: elemento não encontrado')
      return
    }

    try {
      const pdf = await gerarRelatorioPdf(relatorioContainerRef.current, rodapePdf())
      pdf.save(`relatorio_${relatorio?.numero || 'relatorio'}.pdf`)
    } catch (error) {
      console.error('Erro ao gerar PDF:', error)
      alert('Erro ao gerar PDF. Tente novamente.')
    }
  }

  // Função para imprimir relatório (usa o mesmo processo do PDF)
  const imprimirRelatorio = async () => {
    if (!relatorioContainerRef.current) {
      alert('Erro ao preparar impressão: elemento não encontrado')
      return
    }

    try {
      const pdf = await gerarRelatorioPdf(relatorioContainerRef.current, rodapePdf())

      const pdfUrl = URL.createObjectURL(pdf.output('blob'))
      const printWindow = window.open(pdfUrl, '_blank')

      if (printWindow) {
        printWindow.onload = () => {
          printWindow.print()
        }
      } else {
        // Fallback: se não conseguir abrir nova janela, faz download
        pdf.save(`relatorio_${relatorio?.numero || 'relatorio'}.pdf`)
      }
    } catch (error) {
      console.error('Erro ao preparar impressão:', error)
      alert('Erro ao preparar impressão. Tente novamente.')
    }
  }

  const secoesRelatorio = ['Inspeção Visual', 'Testes Funcionais', 'Condições Finais']

  // Laudo assinado (ou usuário sem permissão de operar): o checklist vira LEITURA — texto,
  // não input desabilitado. Dois motivos: não há como editar por engano, e a resposta
  // aparece de fato no PDF (o html2canvas não desenha o ponto de um <input type="radio">
  // marcado, então o laudo saía com as bolinhas todas vazias).
  const renderCampoLeitura = (campo: CampoRelatorio) => {
    const valor = (respostasCampos[campo.id] || '').trim()

    if (!valor) {
      return <span className="campo-relatorio-valor vazio">Não respondido</span>
    }

    if (campo.tipoResposta === 'SimOuNao') {
      const sim = valor.toLowerCase() === 'sim'
      return (
        <span className={`campo-relatorio-valor ${sim ? 'sim' : 'nao'}`}>
          {sim ? '✔' : '✕'} {valor}
        </span>
      )
    }

    return <span className="campo-relatorio-valor texto">{valor}</span>
  }

  const renderCampoInput = (campo: CampoRelatorio) => {
    if (!podeOperar || concluido) {
      return renderCampoLeitura(campo)
    }

    if (campo.tipoResposta === 'SimOuNao') {
      return (
        <div className="campo-relatorio-radio-group">
          {['Sim', 'Não'].map(opt => (
            <label key={opt}>
              <input
                type="radio"
                name={`campo-${campo.id}`}
                value={opt}
                checked={respostasCampos[campo.id] === opt}
                onChange={(e) => {
                  const anterior = respostasSalvasRef.current[campo.id] || ''
                  setRespostasCampos({ ...respostasCampos, [campo.id]: e.target.value })
                  salvarRespostas(campo.id, e.target.value, anterior)
                }}
              />
              {opt}
            </label>
          ))}
        </div>
      )
    }
    if (campo.tipoResposta === 'MultiplasLinhas') {
      return (
        <textarea
          className="campo-relatorio-textarea-display"
          value={respostasCampos[campo.id] || ''}
          onChange={(e) => setRespostasCampos({ ...respostasCampos, [campo.id]: e.target.value })}
          onBlur={() => salvarRespostas(campo.id, respostasCampos[campo.id] || '', respostasSalvasRef.current[campo.id] || '')}
          placeholder="Digite sua resposta..."
          rows={3}
          style={{ width: '100%', minHeight: 70, resize: 'vertical' }}
        />
      )
    }
    return (
      <input
        type="text"
        className="campo-relatorio-input"
        value={respostasCampos[campo.id] || ''}
        onChange={(e) => setRespostasCampos({ ...respostasCampos, [campo.id]: e.target.value })}
        onBlur={(e) => salvarRespostas(campo.id, respostasCampos[campo.id] || '', e.target.defaultValue)}
        placeholder="Digite sua resposta..."
      />
    )
  }

  if (loading) {
    return (
      <div className="visualizar-relatorio">
        <div className="page-header">
          <div>
            <Link to="/relatorios" className="back-link">← Voltar para Relatórios</Link>
            <h1>Carregando relatório...</h1>
          </div>
        </div>
      </div>
    )
  }

  if (!relatorio) {
    return (
      <div className="visualizar-relatorio">
        <div className="page-header">
          <div>
            <Link to="/relatorios" className="back-link">← Voltar para Relatórios</Link>
            <h1>Relatório não encontrado</h1>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="visualizar-relatorio">
      <div className="page-header">
        <div>
          <Link to="/relatorios" className="back-link">← Voltar para Relatórios</Link>
          <h1>Relatório {relatorio.numero}</h1>
          <p className="page-subtitle">Vessel/Frota {relatorio.cliente}</p>
        </div>
        <div className="header-actions">
          <span className={`situacao-badge ${concluido ? 'concluido' : 'rascunho'}`}>
            {concluido ? `✔ Concluído (v${relatorio.versao})` : '✎ Rascunho'}
          </span>
          {podeOperar && !concluido && (
            <button className="btn btn-primary" onClick={concluirRelatorio} disabled={concluindo}>
              {concluindo ? 'Concluindo...' : '✔ Concluir / Assinar'}
            </button>
          )}
          {/* Assinado: o checklist está congelado. Editar é ato explícito e gera versão nova. */}
          {podeOperar && concluido && (
            <button className="btn btn-secondary" onClick={reabrirRelatorio} disabled={reabrindo}>
              {reabrindo ? 'Reabrindo...' : `✎ Editar (gera v${(relatorio.versao ?? 0) + 1})`}
            </button>
          )}
          <button
            className="btn btn-secondary"
            onClick={exportarParaPDF}
            disabled={!concluido}
            title={concluido ? '' : 'Conclua o relatório para gerar o PDF'}
          >
            📥 Download PDF
          </button>
          <button
            className="btn btn-secondary"
            onClick={imprimirRelatorio}
            disabled={!concluido}
            title={concluido ? '' : 'Conclua o relatório para imprimir'}
          >
            🖨️ Imprimir
          </button>
          {/* Exclusão: só Admin e só rascunho — laudo assinado é documento emitido */}
          {isAdmin && !concluido && (
            <button
              className="btn btn-excluir"
              onClick={() => setModalExcluir(true)}
              disabled={excluindo}
            >
              🗑 Excluir laudo
            </button>
          )}
        </div>
      </div>

      {/* Fora do .relatorio-container de propósito: é aviso de tela, não faz parte do documento. */}
      {erroChecklist && (
        <div className="checklist-erro" role="alert">
          ⚠ {erroChecklist}
        </div>
      )}

      {modalExcluir && relatorio && (
        <div className="rel-modal-overlay" role="alertdialog" aria-modal="true" aria-labelledby="titulo-excluir-laudo">
          <div className="rel-modal-card">
            <h2 id="titulo-excluir-laudo">Excluir o laudo {relatorio.numero}?</h2>
            <p>
              O laudo e as respostas do checklist são apagados, o ensaio que o gerou é
              descartado e as leituras de pressão dele saem do banco de dados. Não tem volta.
            </p>
            <p>
              O número <strong>{relatorio.numero}</strong> não volta para o contador — a
              sequência do ano fica com um buraco.
            </p>
            <div className="rel-modal-acoes">
              <button className="btn btn-excluir" onClick={excluirRelatorio} disabled={excluindo}>
                {excluindo ? 'Excluindo…' : 'Excluir laudo e descartar ensaio'}
              </button>
              <button className="btn btn-secondary" onClick={() => setModalExcluir(false)} disabled={excluindo}>
                Voltar
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="relatorio-container" ref={relatorioContainerRef}>
        <div className="relatorio-header-card">
          <div className="relatorio-logo">
            <img src="/modec-logo.png" alt="MODEC Logo" />
          </div>
          <div className="relatorio-info">
            <h2>Relatório de Ensaio Hidráulico</h2>
            <div className="relatorio-meta">
              <div className="meta-item">
                <span className="meta-label">Número:</span>
                <span className="meta-value">{relatorio.numero}</span>
              </div>
              <div className="meta-item">
                <span className="meta-label">Data:</span>
                <span className="meta-value">{relatorio.data}</span>
              </div>
              {/* Vessel/Frota é o cadastro (tabela Clientes) — não existe cliente externo aqui.
                  O código interno do ensaio não entra no documento: o número oficial é o REH-MPR. */}
              <div className="meta-item">
                <span className="meta-label">Vessel/Frota:</span>
                <span className="meta-value">{relatorio.cliente}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Identificação do Documento</h3>
          <div className="info-grid">
            <div className="info-card"><span className="info-label">Local do Teste</span><span className="info-value">{relatorio.ensaioLocalTeste || '-'}</span></div>
            <div className="info-card"><span className="info-label">Departamento</span><span className="info-value">{relatorio.ensaioDepartamento || '-'}</span></div>
            <div className="info-card"><span className="info-label">Ordem de Serviço</span><span className="info-value">{relatorio.ensaioOrdemServico || '-'}</span></div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Identificação do Equipamento</h3>
          <div className="info-grid">
            <div className="info-card"><span className="info-label">Código SAP</span><span className="info-value">{relatorio.cilindroCodigoSap || '-'}</span></div>
            <div className="info-card"><span className="info-label">Tag / ID</span><span className="info-value">{relatorio.cilindroTagId || '-'}</span></div>
            <div className="info-card"><span className="info-label">Fabricante</span><span className="info-value">{relatorio.cilindroFabricante || '-'}</span></div>
            <div className="info-card"><span className="info-label">Nº de Série</span><span className="info-value">{relatorio.cilindroNumeroSerie || '-'}</span></div>
            <div className="info-card"><span className="info-label">Part Number</span><span className="info-value">{relatorio.cilindroPartNumber || '-'}</span></div>
            <div className="info-card"><span className="info-label">Fluido Utilizado</span><span className="info-value">{relatorio.cilindroFluido || '-'}</span></div>
            <div className="info-card"><span className="info-label">Ø Êmbolo (mm)</span><span className="info-value">{relatorio.cilindroDiametroInterno ?? '-'}</span></div>
            <div className="info-card"><span className="info-label">Ø Haste (mm)</span><span className="info-value">{relatorio.cilindroDiametroHaste ?? '-'}</span></div>
            <div className="info-card"><span className="info-label">Pressão Máx. A (bar)</span><span className="info-value">{relatorio.cilindroMaximaPressaoA ?? '-'}</span></div>
            <div className="info-card"><span className="info-label">Pressão Máx. B (bar)</span><span className="info-value">{relatorio.cilindroMaximaPressaoB ?? '-'}</span></div>
          </div>
        </div>

        <div className="relatorio-section">
          <div className="secao-titulo-linha">
            <h3>Resultado do Ensaio</h3>
            {totalPontos > 0 && !loadingGrafico && (
              <div className="export-buttons">
                <button onClick={exportarParaCSV} className="btn btn-secondary export-button">
                  📊 Exportar CSV
                </button>
                <button onClick={exportarParaXLS} className="btn btn-secondary export-button">
                  📈 Exportar XLS
                </button>
              </div>
            )}
          </div>
          <div className="info-grid">
            <div className="info-card">
              <span className="info-label">Câmaras Testadas</span>
              <span className="info-value">
                {relatorio.etapas.length > 0 ? relatorio.etapas.map(e => e.camara).join(' e ') : '-'}
              </span>
            </div>
            <div className="info-card">
              <span className="info-label">Duração Total</span>
              <span className="info-value">{relatorio.duracao || '-'}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Resultado</span>
              <span className={`info-value resultado ${resultadoCalculado?.toLowerCase() === 'aprovado' ? 'aprovado' : 'reprovado'}`}>
                {resultadoCalculado || '-'}
              </span>
            </div>
          </div>
          <p className="resultado-criterio">
            {reprovadoPorChecklist
              ? 'Reprovado pelo checklist de inspeção — sobrepõe o resultado das câmaras.'
              : 'O ensaio é aprovado somente se as duas câmaras forem aprovadas.'}
          </p>
        </div>

        {relatorio.etapas.length === 0 && (
          <div className="relatorio-section">
            <h3>Câmaras</h3>
            <div className="grafico-placeholder">
              <p>Nenhuma câmara registrada neste relatório</p>
              <p className="placeholder-note">
                {relatorio.ensaioId
                  ? 'O ensaio vinculado não tem corridas concluídas.'
                  : 'Este relatório não está vinculado a um ensaio.'}
              </p>
            </div>
          </div>
        )}

        {relatorio.etapas.map(etapa => {
          const serie = serieDaEtapa(etapa.etapaId)
          const limite = etapa.limitePassagem != null && etapa.limitePassagem > 0 ? etapa.limitePassagem : 1
          const duracaoEtapa = etapa.dataFim
            ? formatarDuracao((new Date(etapa.dataFim).getTime() - new Date(etapa.dataInicio).getTime()) / 1000)
            : '-'

          return (
            <div className="relatorio-section" key={etapa.etapaId}>
              <div className="secao-titulo-linha">
                <h3>
                  Câmara {etapa.camara}
                  {etapa.tentativa > 1 && (
                    <span className="etapa-tentativa"> · tentativa {etapa.tentativa}</span>
                  )}
                </h3>
                <span
                  className={`resultado-camara ${etapa.resultado?.toLowerCase() === 'aprovado' ? 'aprovado' : 'reprovado'}`}
                >
                  {etapa.resultado || 'Sem veredito'}
                </span>
              </div>

              <div className="pressao-grid">
                <div className="pressao-card pressao-card-setpoint">
                  <span className="pressao-label">Pressão Setpoint</span>
                  <span className="pressao-value">{Math.round(etapa.pressaoCargaConfigurada)} bar</span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Tempo de Carga</span>
                  <span className="pressao-value">{etapa.tempoCargaConfigurado} min</span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Duração</span>
                  <span className="pressao-value">{duracaoEtapa}</span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Pressão Máxima</span>
                  <span className="pressao-value">
                    {etapa.pressaoMaxima != null ? `${Math.round(etapa.pressaoMaxima)} bar` : '-'}
                  </span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Pressão Mínima</span>
                  <span className="pressao-value">
                    {etapa.pressaoMinima != null ? `${Math.round(etapa.pressaoMinima)} bar` : '-'}
                  </span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Pressão Média</span>
                  <span className="pressao-value">
                    {etapa.pressaoMedia != null ? `${Math.round(etapa.pressaoMedia)} bar` : '-'}
                  </span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Pico Câmara {etapa.camara === 'A' ? 'B' : 'A'}</span>
                  <span className="pressao-value">
                    {etapa.pressaoMaximaCamaraOposta != null
                      ? `${etapa.pressaoMaximaCamaraOposta.toFixed(2)} bar`
                      : '-'}
                  </span>
                </div>
                <div className="pressao-card">
                  <span className="pressao-label">Limite de Passagem</span>
                  <span className="pressao-value">{limite.toFixed(2)} bar</span>
                </div>
              </div>

              {loadingGrafico ? (
                <div className="grafico-placeholder">
                  <p>Carregando dados do gráfico...</p>
                </div>
              ) : serie.length === 0 ? (
                <div className="grafico-placeholder">
                  <p>Nenhum dado de pressão para esta câmara</p>
                </div>
              ) : (
                <div className="grafico-container">
                  <ResponsiveContainer width="100%" height={340}>
                    <LineChart data={serie}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#E0E0E0" />
                      <XAxis
                        dataKey="time"
                        stroke="#666"
                        tick={{ fill: '#666', fontSize: 11 }}
                        label={{ value: 'Tempo', position: 'insideBottom', offset: -5, style: { fontSize: 12 } }}
                      />
                      <YAxis
                        stroke="#666"
                        tick={{ fill: '#666', fontSize: 11 }}
                        label={{ value: 'Pressão (bar)', angle: -90, position: 'insideLeft', style: { fontSize: 12 } }}
                        domain={[0, 'dataMax + 50']}
                      />
                      <Tooltip
                        contentStyle={{
                          backgroundColor: 'rgba(255, 255, 255, 0.95)',
                          border: '1px solid #E0E0E0',
                          borderRadius: '8px'
                        }}
                        formatter={(value: number | undefined, name: string | undefined) => {
                          const nameStr = name || 'Pressão'
                          if (value === undefined || value === null) return ['N/A', nameStr]
                          return [`${value.toFixed(2)} bar`, nameStr]
                        }}
                      />
                      <Legend />
                      <Line
                        type="monotone"
                        dataKey="pressaoA"
                        stroke="#dc3545"
                        strokeWidth={2}
                        dot={false}
                        name={etapa.camara === 'A' ? 'Pressão A — pressurizada (bar)' : 'Pressão A — oposta (bar)'}
                        animationDuration={300}
                      />
                      <Line
                        type="monotone"
                        dataKey="pressaoB"
                        stroke="#007bff"
                        strokeWidth={2}
                        dot={false}
                        name={etapa.camara === 'B' ? 'Pressão B — pressurizada (bar)' : 'Pressão B — oposta (bar)'}
                        animationDuration={300}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              )}
            </div>
          )
        })}

        <div className="relatorio-section">
          <h3>Observações</h3>
          <div className="observacoes-box">
            <p>{relatorio.observacoes || 'Sem observações adicionais.'}</p>
          </div>
        </div>

        {relatorio.campos && relatorio.campos.length > 0 && (() => {
          // Campo excluído do cadastro entra quando ESTE laudo já o respondeu — o backend
          // manda esses de propósito, e descartá-los aqui apagava da tela o checklist de
          // laudos antigos (a pergunta saiu do cadastro depois, a resposta continua valendo).
          const ativos = (relatorio.campos || [])
            .filter(c => !c.excluido || (c.valor || '').trim() !== '')
            .sort((a, b) => a.ordem - b.ordem)
          const grupos = secoesRelatorio
            .map(sec => ({ sec, itens: ativos.filter(c => (c.secao || '').trim() === sec) }))
            .filter(g => g.itens.length > 0)
          const semSecao = ativos.filter(c => !secoesRelatorio.includes((c.secao || '').trim()))
          const renderItens = (itens: CampoRelatorio[]) => (
            <div className="campos-relatorio-container">
              {itens.map(campo => (
                <div key={campo.id} className="campo-relatorio-item">
                  <label className="campo-relatorio-label">
                    {campo.nome}
                    {campo.reprovaSeSim && respostasCampos[campo.id] === 'Sim' && (
                      <span style={{ color: '#dc3545', marginLeft: 6, fontSize: 12, fontWeight: 600 }}>→ Reprova o laudo</span>
                    )}
                  </label>
                  {renderCampoInput(campo)}
                </div>
              ))}
            </div>
          )
          return (
            <>
              {grupos.map(g => (
                <div key={g.sec} className="relatorio-section">
                  <h3>{g.sec}</h3>
                  {renderItens(g.itens)}
                </div>
              ))}
              {semSecao.length > 0 && (
                <div className="relatorio-section">
                  <h3>Perguntas Adicionais</h3>
                  {renderItens(semSecao)}
                </div>
              )}
            </>
          )
        })()}

        {versoes.length > 0 && (
          <div className="relatorio-section">
            <h3>Histórico de Versões</h3>
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>Versão</th>
                    <th>Ação</th>
                    <th>Assinado por</th>
                    <th>Data</th>
                    <th>Resultado</th>
                  </tr>
                </thead>
                <tbody>
                  {versoes.map(v => (
                    <tr key={v.id}>
                      <td>v{v.numeroVersao}</td>
                      <td>{v.acao}</td>
                      <td>{v.usuarioNome || '—'}</td>
                      <td>{new Date(v.dataHora).toLocaleString('pt-BR')}</td>
                      <td>{v.resultado || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        <div className="relatorio-section">
          <h3>Assinaturas</h3>
          <div className="assinaturas-grid">
            {BLOCOS_ASSINATURA.map(bloco => (
              <div className="assinatura-bloco" key={bloco.papel}>
                <div className="assinatura-papel">
                  {bloco.papel}
                  {bloco.observacao && (
                    <span className="assinatura-observacao"> ({bloco.observacao})</span>
                  )}
                </div>
                {bloco.campos.map(campo => (
                  <div
                    className={`assinatura-campo${campo.alto ? ' assinatura-campo-alto' : ''}`}
                    key={campo.rotulo}
                  >
                    <span className="assinatura-rotulo">{campo.rotulo}:</span>
                    <span className="assinatura-linha"></span>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </div>

        <div className="relatorio-footer">
          <div className="footer-signature">
            <div className="signature-line"></div>
            {concluido ? (
              <p>
                <strong>{relatorio.concluidoPorNome || '—'}</strong><br />
                Concluído (v{relatorio.versao}) em{' '}
                {relatorio.dataConclusao ? new Date(relatorio.dataConclusao).toLocaleString('pt-BR') : '-'}
              </p>
            ) : (
              <p>Relatório em rascunho — não assinado</p>
            )}
          </div>
          <div className="footer-date">
            <p>Data de Emissão: {relatorio.data}</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export default VisualizarRelatorio


