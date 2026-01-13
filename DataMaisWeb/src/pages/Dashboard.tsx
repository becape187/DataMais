import './Dashboard.css'

const Dashboard = () => {
  return (
    <div className="dashboard">
      <div className="page-header">
        <h1>Dashboard</h1>
        <p className="page-subtitle">Visão geral do sistema</p>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon stat-icon-motor">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2"/>
              <path d="M12 6V12L16 14" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Status do Motor</h3>
            <p className="stat-value">Ligado</p>
            <span className="stat-badge success">Ativo</span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-ensaio">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="3" y="3" width="18" height="18" rx="2" stroke="currentColor" strokeWidth="2"/>
              <path d="M3 9H21M9 3V21" stroke="currentColor" strokeWidth="2"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Ensaio Atual</h3>
            <p className="stat-value">#1234</p>
            <span className="stat-badge warning">Em Andamento</span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-sensor">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="5" y="2" width="14" height="20" rx="2" stroke="currentColor" strokeWidth="2"/>
              <circle cx="12" cy="12" r="2" fill="currentColor"/>
              <path d="M12 6V8M12 16V18" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Sensores Ativos</h3>
            <p className="stat-value">4/4</p>
            <span className="stat-badge success">100%</span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-pressao">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M3 12L7 8L11 12L15 8L21 12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M3 20L7 16L11 20L15 16L21 20" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Pressão Atual</h3>
            <p className="stat-value">245.8 bar</p>
            <span className="stat-badge info">Normal</span>
          </div>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="dashboard-card">
          <h2>Últimos Ensaios</h2>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Cliente</th>
                  <th>Data</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>#1234</td>
                  <td>MODEC Brasil</td>
                  <td>22/12/2024 14:30</td>
                  <td><span className="badge success">Concluído</span></td>
                </tr>
                <tr>
                  <td>#1233</td>
                  <td>MODEC Brasil</td>
                  <td>22/12/2024 10:15</td>
                  <td><span className="badge success">Concluído</span></td>
                </tr>
                <tr>
                  <td>#1232</td>
                  <td>Petrobras</td>
                  <td>21/12/2024 16:45</td>
                  <td><span className="badge warning">Pendente</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div className="dashboard-card">
          <h2>Status do Sistema</h2>
          <div className="status-list">
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>Motor Hidráulico</strong>
                <p>Operacional</p>
              </div>
            </div>
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>Cilindro</strong>
                <p>Posição: Retraído</p>
              </div>
            </div>
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>Comunicação Modbus</strong>
                <p>Conectado</p>
              </div>
            </div>
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>InfluxDB</strong>
                <p>Sincronizado</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default Dashboard

