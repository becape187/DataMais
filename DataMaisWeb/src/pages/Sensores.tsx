import { Link } from 'react-router-dom'
import './Sensores.css'

interface Sensor {
  id: number
  nome: string
  modelo: string
  range: string
  status: 'ativo' | 'inativo'
  ultimaCalibracao: string
}

const Sensores = () => {
  const sensores: Sensor[] = [
    { 
      id: 1, 
      nome: 'Sensor de Pressão 01', 
      modelo: 'PT-350', 
      range: '0-350 bar',
      status: 'ativo',
      ultimaCalibracao: '15/11/2024'
    },
    { 
      id: 2, 
      nome: 'Sensor de Pressão 02', 
      modelo: 'PT-350', 
      range: '0-350 bar',
      status: 'ativo',
      ultimaCalibracao: '15/11/2024'
    },
    { 
      id: 3, 
      nome: 'Sensor de Pressão 03', 
      modelo: 'PT-350', 
      range: '0-350 bar',
      status: 'ativo',
      ultimaCalibracao: '10/11/2024'
    },
    { 
      id: 4, 
      nome: 'Sensor de Pressão 04', 
      modelo: 'PT-350', 
      range: '0-350 bar',
      status: 'inativo',
      ultimaCalibracao: '05/11/2024'
    },
  ]

  return (
    <div className="sensores">
      <div className="page-header">
        <div>
          <h1>Cadastro de Sensores</h1>
          <p className="page-subtitle">Gerenciamento e configuração de sensores</p>
        </div>
        <button className="btn btn-primary">
          ➕ Novo Sensor
        </button>
      </div>

      <div className="sensores-grid">
        {sensores.map(sensor => (
          <div key={sensor.id} className="sensor-card">
            <div className="sensor-header">
              <div>
                <h3>{sensor.nome}</h3>
                <p className="sensor-modelo">{sensor.modelo}</p>
              </div>
              <span className={`status-badge ${sensor.status}`}>
                {sensor.status === 'ativo' ? '● Ativo' : '○ Inativo'}
              </span>
            </div>
            
            <div className="sensor-info">
              <div className="info-row">
                <span className="info-label">Range:</span>
                <span className="info-value">{sensor.range}</span>
              </div>
              <div className="info-row">
                <span className="info-label">Última Calibração:</span>
                <span className="info-value">{sensor.ultimaCalibracao}</span>
              </div>
            </div>

            <div className="sensor-actions">
              <Link 
                to={`/sensores/${sensor.id}/configuracao`}
                className="btn btn-primary btn-small"
              >
                ⚙️ Configurar
              </Link>
              <button className="btn btn-secondary btn-small">
                📄 Certificado
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export default Sensores

