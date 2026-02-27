import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import api from '../config/api'
import './Relatorios.css'

interface Relatorio {
  id: number
  numero: string
  cliente: string
  data: string
  ensaioId: number | null
  ensaioNumero?: string | null
  status: 'gerado' | 'pendente'
}

const Relatorios = () => {
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const carregarRelatorios = async () => {
      try {
        const response = await api.get('/Relatorio')
        const dados = response.data as any[]

        const mapeados: Relatorio[] = dados.map((r) => ({
          id: r.id,
          numero: r.numero,
          cliente: r.clienteNome || 'N/A',
          data: new Date(r.data).toLocaleString('pt-BR'),
          ensaioId: r.ensaioId ?? null,
          ensaioNumero: r.ensaioNumero ?? null,
          status: 'gerado',
        }))

        setRelatorios(mapeados)
      } catch (err) {
        console.error('Erro ao carregar relatórios:', err)
      } finally {
        setLoading(false)
      }
    }

    carregarRelatorios()
  }, [])

  const relatoriosPorCliente = relatorios.reduce((acc, rel) => {
    if (!acc[rel.cliente]) {
      acc[rel.cliente] = []
    }
    acc[rel.cliente].push(rel)
    return acc
  }, {} as Record<string, Relatorio[]>)

  const relatoriosRecentes = [...relatorios].slice(0, 10)

  return (
    <div className="relatorios">
      <div className="page-header">
        <div>
          <h1>Repositório de Relatórios</h1>
          <p className="page-subtitle">Acesse e visualize os relatórios de ensaios por cliente</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary">
            📄 Gerar Novo Relatório
          </button>
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
                  <th>Cliente</th>
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
                ) : relatoriosRecentes.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '16px' }}>
                      Nenhum relatório encontrado
                    </td>
                  </tr>
                ) : (
                  relatoriosRecentes.map(relatorio => (
                    <tr key={relatorio.id}>
                      <td>
                        <strong>{relatorio.numero}</strong>
                      </td>
                      <td>{relatorio.cliente}</td>
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
        </div>

        <div className="relatorios-por-cliente">
          <h2>Relatórios por Cliente</h2>
          <div className="clientes-grid">
            {Object.entries(relatoriosPorCliente).map(([cliente, rels]) => (
              <div key={cliente} className="cliente-card">
                <div className="cliente-header">
                  <h3>{cliente}</h3>
                  <span className="rel-count">{rels.length} relatório{rels.length > 1 ? 's' : ''}</span>
                </div>
                <div className="cliente-relatorios">
                  {rels.map(rel => (
                    <div key={rel.id} className="rel-item">
                      <div className="rel-info">
                        <span className="rel-numero">{rel.numero}</span>
                        <span className="rel-data">{rel.data}</span>
                      </div>
                      <Link 
                        to={`/relatorios/${rel.id}`}
                        className="btn-link-small"
                      >
                        Visualizar →
                      </Link>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

export default Relatorios


