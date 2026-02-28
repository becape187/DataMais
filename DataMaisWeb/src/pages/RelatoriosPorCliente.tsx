import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import api from '../config/api'
import './Relatorios.css'

interface Relatorio {
  id: number
  numero: string
  cliente: string
  clienteId: number
  data: string
  ensaioId: number | null
  ensaioNumero?: string | null
  cilindroId: number | null
  cilindroNome: string
  status: 'gerado' | 'pendente'
}

interface Cliente {
  id: number
  nome: string
}

interface Cilindro {
  id: number
  nome: string
}

interface Paginacao {
  total: number
  page: number
  pageSize: number
  totalPages: number
}

const RelatoriosPorCliente = () => {
  const { clienteId } = useParams<{ clienteId: string }>()
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  const [cliente, setCliente] = useState<Cliente | null>(null)
  const [loading, setLoading] = useState(true)
  const [cilindros, setCilindros] = useState<Cilindro[]>([])
  
  // Filtros
  const [filtroCilindro, setFiltroCilindro] = useState<number | null>(null)
  const [filtroDataInicio, setFiltroDataInicio] = useState<string>('')
  const [filtroDataFim, setFiltroDataFim] = useState<string>('')
  
  // Paginação
  const [paginaAtual, setPaginaAtual] = useState(1)
  const [paginacao, setPaginacao] = useState<Paginacao>({
    total: 0,
    page: 1,
    pageSize: 5,
    totalPages: 0
  })

  // Carrega cliente e cilindros
  useEffect(() => {
    const carregarDados = async () => {
      if (!clienteId) return
      
      try {
        const clienteResponse = await api.get(`/Cliente/${clienteId}`)
        setCliente({
          id: clienteResponse.data.id,
          nome: clienteResponse.data.nome
        })
        
        const cilindrosResponse = await api.get(`/Cilindro/cliente/${clienteId}`)
        setCilindros(cilindrosResponse.data || [])
      } catch (err) {
        console.error('Erro ao carregar dados:', err)
      }
    }
    
    carregarDados()
  }, [clienteId])

  // Carrega relatórios com filtros e paginação
  useEffect(() => {
    const carregarRelatorios = async () => {
      if (!clienteId) return
      
      try {
        setLoading(true)
        const params = new URLSearchParams()
        params.append('page', paginaAtual.toString())
        params.append('pageSize', '5')
        params.append('clienteId', clienteId)
        
        if (filtroCilindro) {
          params.append('cilindroId', filtroCilindro.toString())
        }
        if (filtroDataInicio) {
          params.append('dataInicio', new Date(filtroDataInicio).toISOString())
        }
        if (filtroDataFim) {
          params.append('dataFim', new Date(filtroDataFim).toISOString())
        }

        const response = await api.get(`/Relatorio?${params.toString()}`)
        const dados = response.data

        if (dados.dados) {
          const mapeados: Relatorio[] = dados.dados.map((r: any) => ({
            id: r.id,
            numero: r.numero,
            cliente: r.clienteNome || 'N/A',
            clienteId: r.clienteId,
            data: new Date(r.data).toLocaleString('pt-BR'),
            ensaioId: r.ensaioId ?? null,
            ensaioNumero: r.ensaioNumero ?? null,
            cilindroId: r.cilindroId,
            cilindroNome: r.cilindroNome || '',
            status: 'gerado',
          }))
          setRelatorios(mapeados)
          setPaginacao({
            total: dados.total,
            page: dados.page,
            pageSize: dados.pageSize,
            totalPages: dados.totalPages
          })
        }
      } catch (err) {
        console.error('Erro ao carregar relatórios:', err)
      } finally {
        setLoading(false)
      }
    }

    carregarRelatorios()
  }, [clienteId, paginaAtual, filtroCilindro, filtroDataInicio, filtroDataFim])

  const limparFiltros = () => {
    setFiltroCilindro(null)
    setFiltroDataInicio('')
    setFiltroDataFim('')
    setPaginaAtual(1)
  }

  if (!cliente) {
    return (
      <div className="relatorios">
        <p>Carregando...</p>
      </div>
    )
  }

  return (
    <div className="relatorios">
      <div className="page-header">
        <div>
          <Link to="/relatorios" className="back-link">← Voltar para Relatórios</Link>
          <h1>Relatórios - {cliente.nome}</h1>
          <p className="page-subtitle">Visualize todos os relatórios deste cliente</p>
        </div>
      </div>

      {/* Filtros */}
      <div className="filtros-container" style={{ marginBottom: '24px', padding: '16px', background: '#f5f5f5', borderRadius: '8px' }}>
        <h3 style={{ marginTop: 0, marginBottom: '16px' }}>Filtros</h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '12px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Cilindro</label>
            <select
              value={filtroCilindro || ''}
              onChange={(e) => {
                setFiltroCilindro(e.target.value ? parseInt(e.target.value) : null)
                setPaginaAtual(1)
              }}
              style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ddd' }}
            >
              <option value="">Todos</option>
              {cilindros.map(cilindro => (
                <option key={cilindro.id} value={cilindro.id}>{cilindro.nome}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Data Início</label>
            <input
              type="date"
              value={filtroDataInicio}
              onChange={(e) => {
                setFiltroDataInicio(e.target.value)
                setPaginaAtual(1)
              }}
              style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ddd' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Data Fim</label>
            <input
              type="date"
              value={filtroDataFim}
              onChange={(e) => {
                setFiltroDataFim(e.target.value)
                setPaginaAtual(1)
              }}
              style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ddd' }}
            />
          </div>
          <div style={{ display: 'flex', alignItems: 'end' }}>
            <button
              onClick={limparFiltros}
              className="btn btn-secondary"
              style={{ width: '100%' }}
            >
              Limpar Filtros
            </button>
          </div>
        </div>
      </div>

      <div className="relatorios-content">
        <div className="relatorios-recentes">
          <h2>Relatórios do Cliente</h2>
          <div className="relatorios-table-container">
            <table className="relatorios-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Cilindro</th>
                  <th>Ensaio</th>
                  <th>Data</th>
                  <th>Status</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '16px' }}>
                      Carregando relatórios...
                    </td>
                  </tr>
                ) : relatorios.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '16px' }}>
                      Nenhum relatório encontrado
                    </td>
                  </tr>
                ) : (
                  relatorios.map(relatorio => (
                    <tr key={relatorio.id}>
                      <td>
                        <strong>{relatorio.numero}</strong>
                      </td>
                      <td>{relatorio.cilindroNome || '-'}</td>
                      <td>
                        <span className="ensaio-badge">
                          {relatorio.ensaioNumero || (relatorio.ensaioId ? `#${relatorio.ensaioId}` : '-')}
                        </span>
                      </td>
                      <td>{relatorio.data}</td>
                      <td>
                        <span className={`status-badge ${relatorio.status}`}>
                          {relatorio.status === 'gerado' ? '✓ Gerado' : '⏳ Pendente'}
                        </span>
                      </td>
                      <td>
                        <Link 
                          to={`/relatorios/${relatorio.id}`}
                          className="btn-link"
                        >
                          👁️ Visualizar
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          
          {/* Paginação */}
          {paginacao.totalPages > 1 && (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '12px', marginTop: '16px' }}>
              <button
                onClick={() => setPaginaAtual(p => Math.max(1, p - 1))}
                disabled={paginaAtual === 1}
                className="btn btn-secondary"
              >
                ← Anterior
              </button>
              <span style={{ fontSize: '14px' }}>
                Página {paginaAtual} de {paginacao.totalPages} ({paginacao.total} relatórios)
              </span>
              <button
                onClick={() => setPaginaAtual(p => Math.min(paginacao.totalPages, p + 1))}
                disabled={paginaAtual === paginacao.totalPages}
                className="btn btn-secondary"
              >
                Próxima →
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default RelatoriosPorCliente
