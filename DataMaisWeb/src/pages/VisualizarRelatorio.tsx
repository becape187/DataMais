import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import api from '../config/api'
import './VisualizarRelatorio.css'

interface RelatorioDetalhe {
  id: number
  numero: string
  cliente: string
  data: string
  ensaioId?: number | null
  ensaioNumero?: string | null
  observacoes?: string | null
  camaraTestada?: string | null
  pressaoCargaConfigurada?: number | null
  tempoCargaConfigurado?: number | null
  duracao?: string | null
  pressaoMaxima?: number | null
  pressaoMinima?: number | null
  pressaoMedia?: number | null
}

interface DataPoint {
  time: string
  pressao: number
}

const VisualizarRelatorio = () => {
  const { id } = useParams<{ id: string }>()
  const [relatorio, setRelatorio] = useState<RelatorioDetalhe | null>(null)
  const [loading, setLoading] = useState(true)
  const [dadosGrafico, setDadosGrafico] = useState<DataPoint[]>([])
  const [loadingGrafico, setLoadingGrafico] = useState(true)

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
            const totalSec = Math.round(diffMs / 1000)
            const minutos = Math.floor(totalSec / 60)
            const segundos = totalSec % 60
            duracao = `${minutos} min ${segundos.toString().padStart(2, '0')} s`
          }
        }

        setRelatorio({
          id: r.id,
          numero: r.numero,
          cliente: r.clienteNome || 'N/A',
          data: dataStr,
          ensaioId: r.ensaioId ?? null,
          ensaioNumero: r.ensaioNumero ?? null,
          observacoes: r.observacoes ?? null,
          camaraTestada: r.camaraTestada ?? null,
          pressaoCargaConfigurada: r.pressaoCargaConfigurada ?? null,
          tempoCargaConfigurado: r.tempoCargaConfigurado ?? null,
          duracao,
          pressaoMaxima: r.pressaoMaxima ?? null,
          pressaoMinima: r.pressaoMinima ?? null,
          pressaoMedia: r.pressaoMedia ?? null,
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
        const dados = response.data.dados || []
        setDadosGrafico(dados)
      } catch (err) {
        console.error('Erro ao carregar dados do gráfico:', err)
        setDadosGrafico([])
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
          <p className="page-subtitle">
            {relatorio.ensaioNumero
              ? <>Ensaio {relatorio.ensaioNumero} - {relatorio.cliente}</>
              : <>Cliente {relatorio.cliente}</>}
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary">📥 Download PDF</button>
          <button className="btn btn-secondary">🖨️ Imprimir</button>
        </div>
      </div>

      <div className="relatorio-container">
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
              <div className="meta-item">
                <span className="meta-label">Cliente:</span>
                <span className="meta-value">{relatorio.cliente}</span>
              </div>
              <div className="meta-item">
                <span className="meta-label">Ensaio:</span>
                <span className="meta-value">
                  {relatorio.ensaioNumero || (relatorio.ensaioId ? `#${relatorio.ensaioId}` : '-')}
                </span>
              </div>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Informações do Ensaio</h3>
          <div className="info-grid">
            <div className="info-card">
              <span className="info-label">Câmara Testada</span>
              <span className="info-value">{relatorio.camaraTestada || '-'}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Pressão de Carga Configurada</span>
              <span className="info-value">
                {relatorio.pressaoCargaConfigurada != null ? `${relatorio.pressaoCargaConfigurada} bar` : '-'}
              </span>
            </div>
            <div className="info-card">
              <span className="info-label">Duração</span>
              <span className="info-value">{relatorio.duracao || '-'}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Resultado</span>
              <span className="info-value resultado aprovado">
                Aprovado
              </span>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Dados de Pressão</h3>
          <div className="pressao-grid">
            <div className="pressao-card">
              <span className="pressao-label">Pressão Máxima</span>
              <span className="pressao-value">
                {relatorio.pressaoMaxima != null ? `${relatorio.pressaoMaxima.toFixed(2)} bar` : 'N/A'}
              </span>
            </div>
            <div className="pressao-card">
              <span className="pressao-label">Pressão Média</span>
              <span className="pressao-value">
                {relatorio.pressaoMedia != null ? `${relatorio.pressaoMedia.toFixed(2)} bar` : 'N/A'}
              </span>
            </div>
            <div className="pressao-card">
              <span className="pressao-label">Pressão Mínima</span>
              <span className="pressao-value">
                {relatorio.pressaoMinima != null ? `${relatorio.pressaoMinima.toFixed(2)} bar` : 'N/A'}
              </span>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Gráfico de Pressão</h3>
          {loadingGrafico ? (
            <div className="grafico-placeholder">
              <p>Carregando dados do gráfico...</p>
            </div>
          ) : dadosGrafico.length === 0 ? (
            <div className="grafico-placeholder">
              <p>Nenhum dado disponível para o gráfico</p>
              <p className="placeholder-note">
                {relatorio?.ensaioId 
                  ? 'Não foram encontrados dados de pressão para este ensaio no período especificado.'
                  : 'Este relatório não está vinculado a um ensaio.'}
              </p>
            </div>
          ) : (
            <div className="grafico-container">
              <ResponsiveContainer width="100%" height={400}>
                <LineChart data={dadosGrafico}>
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
                    formatter={(value: number) => [`${value.toFixed(2)} bar`, 'Pressão']}
                  />
                  <Legend />
                  <Line 
                    type="monotone" 
                    dataKey="pressao" 
                    stroke="var(--modec-red)" 
                    strokeWidth={2}
                    dot={false}
                    name="Pressão (bar)"
                    animationDuration={300}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>

        <div className="relatorio-section">
          <h3>Observações</h3>
          <div className="observacoes-box">
            <p>{relatorio.observacoes || 'Sem observações adicionais.'}</p>
          </div>
        </div>

        <div className="relatorio-footer">
          <div className="footer-signature">
            <div className="signature-line"></div>
            <p>Assinatura do Técnico Responsável</p>
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


