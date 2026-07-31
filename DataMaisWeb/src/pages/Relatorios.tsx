import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import api from '../config/api'
import './Relatorios.css'

interface Relatorio {
  id: number
  numero: string
  cliente: string
  clienteId: number
  data: string
  ensaioId: number | null
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

const Relatorios = () => {
  const navigate = useNavigate()
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  const [loading, setLoading] = useState(true)
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [cilindros, setCilindros] = useState<Cilindro[]>([])
  
  // Filtros
  const [filtroCliente, setFiltroCliente] = useState<number | null>(null)
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

  // Carrega clientes e cilindros para os filtros
  useEffect(() => {
    const carregarFiltros = async () => {
      try {
        const clientesResponse = await api.get('/Cliente')
        setClientes(clientesResponse.data || [])
        
        if (filtroCliente) {
          const cilindrosResponse = await api.get(`/Cilindro/cliente/${filtroCliente}`)
          setCilindros(cilindrosResponse.data || [])
        } else {
          setCilindros([])
        }
      } catch (err) {
        console.error('Erro ao carregar filtros:', err)
      }
    }
    
    carregarFiltros()
  }, [filtroCliente])

  // Carrega relatórios com filtros e paginação
  useEffect(() => {
    const carregarRelatorios = async () => {
      try {
        setLoading(true)
        const params = new URLSearchParams()
        params.append('page', paginaAtual.toString())
        params.append('pageSize', '5')
        
        if (filtroCliente) {
          params.append('clienteId', filtroCliente.toString())
        }
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
          // Nova estrutura com paginação
          const mapeados: Relatorio[] = dados.dados.map((r: any) => ({
            id: r.id,
            numero: r.numero,
            cliente: r.clienteNome || 'N/A',
            clienteId: r.clienteId,
            data: new Date(r.data).toLocaleString('pt-BR'),
            ensaioId: r.ensaioId ?? null,
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
        } else {
          // Estrutura antiga (fallback)
          const mapeados: Relatorio[] = dados.map((r: any) => ({
            id: r.id,
            numero: r.numero,
            cliente: r.clienteNome || 'N/A',
            clienteId: r.clienteId,
            data: new Date(r.data).toLocaleString('pt-BR'),
            ensaioId: r.ensaioId ?? null,
            cilindroId: r.cilindroId,
            cilindroNome: r.cilindroNome || '',
            status: 'gerado',
          }))
          setRelatorios(mapeados.slice(0, 5))
        }
      } catch (err) {
        console.error('Erro ao carregar relatórios:', err)
      } finally {
        setLoading(false)
      }
    }

    carregarRelatorios()
  }, [paginaAtual, filtroCliente, filtroCilindro, filtroDataInicio, filtroDataFim])

  // Busca todos os relatórios para agrupar por cliente (sem filtros)
  const [todosRelatorios, setTodosRelatorios] = useState<Relatorio[]>([])

  useEffect(() => {
    const carregarTodosRelatorios = async () => {
      try {
        const response = await api.get('/Relatorio?page=1&pageSize=1000')
        const dados = response.data

        if (dados.dados) {
          const mapeados: Relatorio[] = dados.dados.map((r: any) => ({
            id: r.id,
            numero: r.numero,
            cliente: r.clienteNome || 'N/A',
            clienteId: r.clienteId,
            data: new Date(r.data).toLocaleString('pt-BR'),
            ensaioId: r.ensaioId ?? null,
            cilindroId: r.cilindroId,
            cilindroNome: r.cilindroNome || '',
            status: 'gerado',
          }))
          setTodosRelatorios(mapeados)
        } else {
          const mapeados: Relatorio[] = dados.map((r: any) => ({
            id: r.id,
            numero: r.numero,
            cliente: r.clienteNome || 'N/A',
            clienteId: r.clienteId,
            data: new Date(r.data).toLocaleString('pt-BR'),
            ensaioId: r.ensaioId ?? null,
            cilindroId: r.cilindroId,
            cilindroNome: r.cilindroNome || '',
            status: 'gerado',
          }))
          setTodosRelatorios(mapeados)
        }
      } catch (err) {
        console.error('Erro ao carregar todos os relatórios:', err)
      }
    }

    carregarTodosRelatorios()
  }, [])

  // Agrupa relatórios por cliente e pega apenas os 5 primeiros clientes
  const relatoriosPorCliente = todosRelatorios.reduce((acc, rel) => {
    if (!acc[rel.cliente]) {
      acc[rel.cliente] = []
    }
    acc[rel.cliente].push(rel)
    return acc
  }, {} as Record<string, Relatorio[]>)

  const clientesUnicos = Object.keys(relatoriosPorCliente).slice(0, 5)

  const limparFiltros = () => {
    setFiltroCliente(null)
    setFiltroCilindro(null)
    setFiltroDataInicio('')
    setFiltroDataFim('')
    setPaginaAtual(1)
  }

  return (
    <div className="relatorios">
      <div className="page-header">
        <div>
          <h1>Repositório de Relatórios</h1>
          <p className="page-subtitle">Acesse e visualize os relatórios de ensaios por vessel/frota</p>
        </div>
      </div>

      {/* Filtros */}
      <div className="filtros-container" style={{ marginBottom: '24px', padding: '16px', background: '#f5f5f5', borderRadius: '8px' }}>
        <h3 style={{ marginTop: 0, marginBottom: '16px' }}>Filtros</h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '12px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Vessel/Frota</label>
            <select
              value={filtroCliente || ''}
              onChange={(e) => {
                setFiltroCliente(e.target.value ? parseInt(e.target.value) : null)
                setFiltroCilindro(null)
                setPaginaAtual(1)
              }}
              style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ddd' }}
            >
              <option value="">Todos</option>
              {clientes.map(cliente => (
                <option key={cliente.id} value={cliente.id}>{cliente.nome}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Cilindro</label>
            <select
              value={filtroCilindro || ''}
              onChange={(e) => {
                setFiltroCilindro(e.target.value ? parseInt(e.target.value) : null)
                setPaginaAtual(1)
              }}
              disabled={!filtroCliente}
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
          <h2>Últimos Relatórios</h2>
          <div className="relatorios-table-container">
            <table className="relatorios-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Vessel/Frota</th>
                  <th>Data</th>
                  <th>Status</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={5} style={{ textAlign: 'center', padding: '16px' }}>
                      Carregando relatórios...
                    </td>
                  </tr>
                ) : relatorios.length === 0 ? (
                  <tr>
                    <td colSpan={5} style={{ textAlign: 'center', padding: '16px' }}>
                      Nenhum relatório encontrado
                    </td>
                  </tr>
                ) : (
                  relatorios.map(relatorio => (
                    <tr key={relatorio.id}>
                      <td>
                        <strong>{relatorio.numero}</strong>
                      </td>
                      <td>{relatorio.cliente}</td>
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

        <div className="relatorios-por-cliente">
          <h2>Relatórios por Vessel/Frota</h2>
          <div className="clientes-grid">
            {clientesUnicos.map((cliente) => {
              const rels = relatoriosPorCliente[cliente]
              const clienteId = rels[0]?.clienteId
              return (
                <div 
                  key={cliente} 
                  className="cliente-card"
                  style={{ cursor: 'pointer' }}
                  onClick={() => navigate(`/relatorios/cliente/${clienteId}`)}
                >
                  <div className="cliente-header">
                    <h3>{cliente}</h3>
                    <span className="rel-count">{rels.length} relatório{rels.length > 1 ? 's' : ''}</span>
                  </div>
                  <div className="cliente-relatorios">
                    {rels.slice(0, 5).map(rel => (
                      <div key={rel.id} className="rel-item">
                        <div className="rel-info">
                          <span className="rel-numero">{rel.numero}</span>
                          <span className="rel-data">{rel.data}</span>
                        </div>
                        <Link 
                          to={`/relatorios/${rel.id}`}
                          className="btn-link-small"
                          onClick={(e) => e.stopPropagation()}
                        >
                          Visualizar →
                        </Link>
                      </div>
                    ))}
                  </div>
                  {rels.length > 5 && (
                    <div style={{ padding: '12px', textAlign: 'center', borderTop: '1px solid #eee' }}>
                      <span style={{ fontSize: '14px', color: '#666' }}>
                        +{rels.length - 5} relatório{rels.length - 5 > 1 ? 's' : ''} mais
                      </span>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </div>
  )
}

export default Relatorios
