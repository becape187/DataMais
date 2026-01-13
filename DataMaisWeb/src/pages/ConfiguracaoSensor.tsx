import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import './ConfiguracaoSensor.css'

interface PontoCalibracao {
  x: number
  y: number
}

const ConfiguracaoSensor = () => {
  const { id } = useParams<{ id: string }>()
  const [pontos, setPontos] = useState<PontoCalibracao[]>([
    { x: 0, y: 0 },
    { x: 0, y: 0 },
    { x: 0, y: 0 },
    { x: 0, y: 0 }
  ])
  const [range, setRange] = useState('0-350')
  const [certificado, setCertificado] = useState<File | null>(null)

  const handlePontoChange = (index: number, field: 'x' | 'y', value: number) => {
    const novosPontos = [...pontos]
    novosPontos[index][field] = value
    setPontos(novosPontos)
  }

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setCertificado(e.target.files[0])
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    // Aqui seria a lógica de salvar
    alert('Configuração salva com sucesso!')
  }

  return (
    <div className="configuracao-sensor">
      <div className="page-header">
        <div>
          <Link to="/sensores" className="back-link">← Voltar para Sensores</Link>
          <h1>Configuração do Sensor #{id}</h1>
          <p className="page-subtitle">Configuração de calibração e pontos de correção</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="config-form">
        <div className="form-section">
          <h2>Informações do Sensor</h2>
          <div className="form-row">
            <div className="form-group">
              <label>Range do Sensor (bar)</label>
              <input 
                type="text" 
                value={range}
                onChange={(e) => setRange(e.target.value)}
                placeholder="0-350"
              />
            </div>
          </div>
        </div>

        <div className="form-section">
          <h2>Pontos de Correção de Curva</h2>
          <p className="section-description">
            Configure 4 pontos para correção da curva de calibração
          </p>
          <div className="pontos-grid">
            {pontos.map((ponto, index) => (
              <div key={index} className="ponto-card">
                <h3>Ponto {index} (x{index}, y{index})</h3>
                <div className="ponto-inputs">
                  <div className="form-group">
                    <label>X{index}</label>
                    <input 
                      type="number" 
                      step="0.01"
                      value={ponto.x}
                      onChange={(e) => handlePontoChange(index, 'x', parseFloat(e.target.value) || 0)}
                    />
                  </div>
                  <div className="form-group">
                    <label>Y{index}</label>
                    <input 
                      type="number" 
                      step="0.01"
                      value={ponto.y}
                      onChange={(e) => handlePontoChange(index, 'y', parseFloat(e.target.value) || 0)}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="form-section">
          <h2>Certificado de Calibração</h2>
          <div className="upload-area">
            <input 
              type="file" 
              id="certificado"
              accept=".pdf,.jpg,.jpeg,.png"
              onChange={handleFileChange}
              className="file-input"
            />
            <label htmlFor="certificado" className="upload-label">
              {certificado ? (
                <span>📄 {certificado.name}</span>
              ) : (
                <span>📤 Clique para fazer upload do certificado</span>
              )}
            </label>
          </div>
        </div>

        <div className="form-section">
          <h2>Dados da Calibração</h2>
          <div className="form-row">
            <div className="form-group">
              <label>Data da Calibração</label>
              <input type="date" />
            </div>
            <div className="form-group">
              <label>Laboratório</label>
              <input type="text" placeholder="Nome do laboratório" />
            </div>
            <div className="form-group">
              <label>Número do Certificado</label>
              <input type="text" placeholder="Nº do certificado" />
            </div>
          </div>
        </div>

        <div className="form-actions">
          <Link to="/sensores" className="btn btn-secondary">
            Cancelar
          </Link>
          <button type="submit" className="btn btn-primary">
            💾 Salvar Configuração
          </button>
        </div>
      </form>
    </div>
  )
}

export default ConfiguracaoSensor

