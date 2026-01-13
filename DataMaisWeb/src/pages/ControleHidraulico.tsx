import { useState } from 'react'
import './ControleHidraulico.css'

const ControleHidraulico = () => {
  const [motorStatus, setMotorStatus] = useState(false)
  const [cilindroPosicao, setCilindroPosicao] = useState<'retraido' | 'avançado'>('retraido')

  const toggleMotor = () => {
    setMotorStatus(!motorStatus)
  }

  const avancarCilindro = () => {
    setCilindroPosicao('avançado')
  }

  const recuarCilindro = () => {
    setCilindroPosicao('retraido')
  }

  return (
    <div className="controle-hidraulico">
      <div className="page-header">
        <h1>Controle da Unidade Hidráulica</h1>
        <p className="page-subtitle">Controle manual do motor e cilindro</p>
      </div>

      <div className="controle-grid">
        <div className="controle-card">
          <div className="controle-header">
            <h2>Motor Hidráulico</h2>
            <div className={`status-indicator-large ${motorStatus ? 'active' : 'inactive'}`}>
              <span className="status-dot"></span>
              {motorStatus ? 'Ligado' : 'Desligado'}
            </div>
          </div>
          
          <div className="controle-actions">
            <button 
              className={`btn btn-primary ${motorStatus ? 'btn-danger' : ''}`}
              onClick={toggleMotor}
            >
              {motorStatus ? '⏹️ Desligar Motor' : '▶️ Ligar Motor'}
            </button>
          </div>

          <div className="info-panel">
            <div className="info-item">
              <span className="info-label">Tensão:</span>
              <span className="info-value">380V</span>
            </div>
            <div className="info-item">
              <span className="info-label">Corrente:</span>
              <span className="info-value">{motorStatus ? '15.2 A' : '0 A'}</span>
            </div>
            <div className="info-item">
              <span className="info-label">Potência:</span>
              <span className="info-value">{motorStatus ? '5.8 kW' : '0 kW'}</span>
            </div>
          </div>
        </div>

        <div className="controle-card">
          <div className="controle-header">
            <h2>Controle do Cilindro</h2>
            <div className="status-indicator-large">
              <span className="status-dot"></span>
              Posição: {cilindroPosicao === 'avançado' ? 'Avançado' : 'Retraído'}
            </div>
          </div>

          <div className="controle-actions">
            <button 
              className="btn btn-success"
              onClick={avancarCilindro}
              disabled={!motorStatus}
            >
              ⬆️ Avançar Cilindro
            </button>
            <button 
              className="btn btn-warning"
              onClick={recuarCilindro}
              disabled={!motorStatus}
            >
              ⬇️ Recuar Cilindro
            </button>
          </div>

          <div className="info-panel">
            <div className="info-item">
              <span className="info-label">Posição Atual:</span>
              <span className="info-value">{cilindroPosicao === 'avançado' ? '100%' : '0%'}</span>
            </div>
            <div className="info-item">
              <span className="info-label">Pressão:</span>
              <span className="info-value">{motorStatus ? '245.8 bar' : '0 bar'}</span>
            </div>
            <div className="info-item">
              <span className="info-label">Velocidade:</span>
              <span className="info-value">{motorStatus ? '2.5 mm/s' : '0 mm/s'}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="controle-card full-width">
        <h2>Diagrama do Sistema</h2>
        <div className="diagram-container">
          <div className="diagram">
            <div className={`motor-diagram ${motorStatus ? 'active' : ''}`}>
              <div className="motor-icon">⚙️</div>
              <div className="motor-label">Motor</div>
            </div>
            <div className="pipe"></div>
            <div className={`cilindro-diagram ${cilindroPosicao === 'avançado' ? 'extended' : ''}`}>
              <div className="cilindro-body">
                <div className="cilindro-piston"></div>
              </div>
              <div className="cilindro-label">Cilindro</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default ControleHidraulico

