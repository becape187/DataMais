import { Link } from 'react-router-dom'
import './Relatorios.css'

interface Relatorio {
  id: number
  numero: string
  cliente: string
  data: string
  ensaioId: string
  status: 'gerado' | 'pendente'
}

const Relatorios = () => {
  const relatorios: Relatorio[] = [
    {
      id: 1,
      numero: 'REL-2024-001',
      cliente: 'MODEC Brasil',
      data: '22/12/2024 14:30',
      ensaioId: '#1234',
      status: 'gerado'
    },
    {
      id: 2,
      numero: 'REL-2024-002',
      cliente: 'MODEC Brasil',
      data: '22/12/2024 10:15',
      ensaioId: '#1233',
      status: 'gerado'
    },
    {
      id: 3,
      numero: 'REL-2024-003',
      cliente: 'Petrobras',
      data: '21/12/2024 16:45',
      ensaioId: '#1232',
      status: 'gerado'
    },
    {
      id: 4,
      numero: 'REL-2024-004',
      cliente: 'Equinor Brasil',
      data: '20/12/2024 11:20',
      ensaioId: '#1231',
      status: 'gerado'
    },
    {
      id: 5,
      numero: 'REL-2024-005',
      cliente: 'MODEC Brasil',
      data: '19/12/2024 09:00',
      ensaioId: '#1230',
      status: 'gerado'
    },
  ]

  const relatoriosPorCliente = relatorios.reduce((acc, rel) => {
    if (!acc[rel.cliente]) {
      acc[rel.cliente] = []
    }
    acc[rel.cliente].push(rel)
    return acc
  }, {} as Record<string, Relatorio[]>)

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
                {relatorios.map(relatorio => (
                  <tr key={relatorio.id}>
                    <td>
                      <strong>{relatorio.numero}</strong>
                    </td>
                    <td>{relatorio.cliente}</td>
                    <td>
                      <span className="ensaio-badge">{relatorio.ensaioId}</span>
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
                ))}
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


