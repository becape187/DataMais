import { useState, useEffect } from 'react'
import { useParams, Link } from 'react-router-dom'
import api from '../config/api'
import './ConfiguracaoSensor.css'

const ConfiguracaoSensor = () => {
  const { id } = useParams<{ id: string }>()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // Campos de calibração linear (2 pontos)
  const [x1, setX1] = useState<number>(0) // InputMin (valor AD mínimo)
  const [y1, setY1] = useState<number>(0) // OutputMin (valor medido mínimo)
  const [x2, setX2] = useState<number>(0) // InputMax (valor AD máximo)
  const [y2, setY2] = useState<number>(0) // OutputMax (valor medido máximo)
  
  const [certificado, setCertificado] = useState<File | null>(null)

  useEffect(() => {
    const carregarSensor = async () => {
      if (!id) return
      
      try {
        setLoading(true)
        const response = await api.get(`/Sensor/${id}`)
        const sensor = response.data
        
        setX1(sensor.inputMin || 0)
        setY1(sensor.outputMin || 0)
        setX2(sensor.inputMax || 0)
        setY2(sensor.outputMax || 0)
      } catch (err: any) {
        console.error('Erro ao carregar sensor:', err)
        setError('Erro ao carregar dados do sensor')
      } finally {
        setLoading(false)
      }
    }

    carregarSensor()
  }, [id])

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setCertificado(e.target.files[0])
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!id) return

    try {
      setSaving(true)
      setError(null)

      await api.put(`/Sensor/${id}`, {
        inputMin: x1,
        outputMin: y1,
        inputMax: x2,
        outputMax: y2
      })

      alert('Configuração salva com sucesso!')
    } catch (err: any) {
      console.error('Erro ao salvar configuração:', err)
      setError('Erro ao salvar configuração: ' + (err.response?.data?.message || err.message))
    } finally {
      setSaving(false)
    }
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

      {loading && (
        <div className="loading-message">
          Carregando configuração do sensor...
        </div>
      )}

      {error && (
        <div className="message message-error">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="config-form">
        <div className="form-section">
          <h2>Calibração Linear do Sensor</h2>
          <p className="section-description">
            Configure dois pontos para calibração linear: (x1, y1) e (x2, y2)
            <br />
            <strong>x1, x2:</strong> Valores AD (Analógico-Digital) lidos do Modbus
            <br />
            <strong>y1, y2:</strong> Valores medidos correspondentes (ex: pressão em bar)
          </p>
          
          <div className="calibracao-grid">
            <div className="ponto-card">
              <h3>Ponto 1 (Mínimo)</h3>
              <div className="ponto-inputs">
                <div className="form-group">
                  <label>X1 - InputMin (Valor AD mínimo)</label>
                  <input 
                    type="number" 
                    step="0.01"
                    value={x1}
                    onChange={(e) => setX1(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Y1 - OutputMin (Valor medido mínimo)</label>
                  <input 
                    type="number" 
                    step="0.01"
                    value={y1}
                    onChange={(e) => setY1(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>
              </div>
            </div>

            <div className="ponto-card">
              <h3>Ponto 2 (Máximo)</h3>
              <div className="ponto-inputs">
                <div className="form-group">
                  <label>X2 - InputMax (Valor AD máximo)</label>
                  <input 
                    type="number" 
                    step="0.01"
                    value={x2}
                    onChange={(e) => setX2(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Y2 - OutputMax (Valor medido máximo)</label>
                  <input 
                    type="number" 
                    step="0.01"
                    value={y2}
                    onChange={(e) => setY2(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>
              </div>
            </div>
          </div>

          {(x1 !== 0 || y1 !== 0 || x2 !== 0 || y2 !== 0) && x2 !== x1 && (
            <div className="calibracao-info">
              <h4>Fórmula de Conversão:</h4>
              <p>
                <code>Output = (({y2} - {y1}) / ({x2} - {x1})) × (Input - {x1}) + {y1}</code>
              </p>
              <p>
                <strong>Inclinação (slope):</strong> {((y2 - y1) / (x2 - x1)).toFixed(6)}
              </p>
            </div>
          )}
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
          <button type="submit" className="btn btn-primary" disabled={saving || loading}>
            {saving ? '⏳ Salvando...' : '💾 Salvar Configuração'}
          </button>
        </div>
      </form>
    </div>
  )
}

export default ConfiguracaoSensor

