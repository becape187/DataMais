import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import api from '../config/api'
import './Sensores.css'

interface Sensor {
  id: number
  nome: string
  descricao?: string
  tipo: string
  unidade?: string
  inputMin?: number
  outputMin?: number
  inputMax?: number
  outputMax?: number
  modbusConfigId?: number
  modbusConfig?: {
    id: number
    nome: string
    enderecoRegistro: number
    fatorConversao?: number
    offset?: number
  }
  ativo: boolean
  dataCriacao?: string
  dataAtualizacao?: string
}

const Sensores = () => {
  const [sensores, setSensores] = useState<Sensor[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const buscarSensores = async () => {
      try {
        setLoading(true)
        setError(null)
        const response = await api.get('/Sensor')
        setSensores(response.data)
      } catch (err: any) {
        console.error('Erro ao buscar sensores:', err)
        setError('Erro ao carregar sensores: ' + (err.response?.data?.message || err.message))
      } finally {
        setLoading(false)
      }
    }

    buscarSensores()
  }, [])

  const formatarData = (data?: string) => {
    if (!data) return '--'
    try {
      const date = new Date(data)
      return date.toLocaleDateString('pt-BR')
    } catch {
      return '--'
    }
  }

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

      {error && (
        <div className="message message-error">
          {error}
        </div>
      )}

      {loading && (
        <div className="loading-message">
          Carregando sensores...
        </div>
      )}

      {!loading && sensores.length === 0 && (
        <div className="message">
          Nenhum sensor cadastrado. Clique em "Novo Sensor" para adicionar.
        </div>
      )}

      <div className="sensores-grid">
        {sensores.map(sensor => (
          <div key={sensor.id} className="sensor-card">
            <div className="sensor-header">
              <div>
                <h3>{sensor.nome}</h3>
                <p className="sensor-modelo">{sensor.tipo} {sensor.unidade ? `(${sensor.unidade})` : ''}</p>
              </div>
              <span className={`status-badge ${sensor.ativo ? 'ativo' : 'inativo'}`}>
                {sensor.ativo ? '● Ativo' : '○ Inativo'}
              </span>
            </div>
            
            <div className="sensor-info">
              <div className="info-row">
                <span className="info-label">Tipo:</span>
                <span className="info-value">{sensor.tipo}</span>
              </div>
              {(sensor.inputMin !== null && sensor.inputMin !== undefined) && (
                <div className="info-row">
                  <span className="info-label">Calibração:</span>
                  <span className="info-value">
                    ({sensor.inputMin}, {sensor.outputMin}) → ({sensor.inputMax}, {sensor.outputMax})
                  </span>
                </div>
              )}
              {sensor.modbusConfig && (
                <div className="info-row">
                  <span className="info-label">Modbus:</span>
                  <span className="info-value">{sensor.modbusConfig.nome}</span>
                </div>
              )}
              <div className="info-row">
                <span className="info-label">Criado em:</span>
                <span className="info-value">{formatarData(sensor.dataCriacao)}</span>
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

