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

interface ModbusConfig {
  id: number
  nome: string
  descricao?: string
}

const Sensores = () => {
  const [sensores, setSensores] = useState<Sensor[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showModal, setShowModal] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [modbusConfigs, setModbusConfigs] = useState<ModbusConfig[]>([])
  const [formData, setFormData] = useState<Partial<Sensor>>({
    nome: '',
    descricao: '',
    tipo: 'pressao',
    unidade: 'bar',
    ativo: true
  })

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

  useEffect(() => {
    const buscarModbusConfigs = async () => {
      try {
        const response = await api.get('/ModbusConfig')
        setModbusConfigs(response.data.filter((r: any) => r.ativo))
      } catch (err) {
        console.error('Erro ao buscar configurações Modbus:', err)
      }
    }

    if (showModal) {
      buscarModbusConfigs()
    }
  }, [showModal])

  const formatarData = (data?: string) => {
    if (!data) return '--'
    try {
      const date = new Date(data)
      return date.toLocaleDateString('pt-BR')
    } catch {
      return '--'
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      if (editingId === null) {
        await api.post('/Sensor', formData)
      } else {
        await api.put(`/Sensor/${editingId}`, formData)
      }
      
      // Recarrega a lista de sensores
      const response = await api.get('/Sensor')
      setSensores(response.data)
      
      setShowModal(false)
      setEditingId(null)
      setFormData({
        nome: '',
        descricao: '',
        tipo: 'pressao',
        unidade: 'bar',
        ativo: true
      })
    } catch (err: any) {
      console.error('Erro ao salvar sensor:', err)
      const message = err.response?.data?.message || 'Erro ao salvar sensor'
      alert(message)
    }
  }

  const handleEdit = (sensor: Sensor) => {
    setEditingId(sensor.id)
    setFormData({
      nome: sensor.nome,
      descricao: sensor.descricao || '',
      tipo: sensor.tipo,
      unidade: sensor.unidade || '',
      modbusConfigId: sensor.modbusConfigId,
      ativo: sensor.ativo
    })
    setShowModal(true)
  }

  const handleDelete = async (id: number) => {
    if (!confirm('Tem certeza que deseja excluir este sensor?')) {
      return
    }

    try {
      await api.delete(`/Sensor/${id}`)
      const response = await api.get('/Sensor')
      setSensores(response.data)
    } catch (err: any) {
      console.error('Erro ao deletar sensor:', err)
      alert('Erro ao deletar sensor: ' + (err.response?.data?.message || err.message))
    }
  }

  const handleNewSensor = () => {
    setEditingId(null)
    setFormData({
      nome: '',
      descricao: '',
      tipo: 'pressao',
      unidade: 'bar',
      ativo: true
    })
    setShowModal(true)
  }

  return (
    <div className="sensores">
      <div className="page-header">
        <div>
          <h1>Cadastro de Sensores</h1>
          <p className="page-subtitle">Gerenciamento e configuração de sensores</p>
        </div>
        <button className="btn btn-primary" onClick={handleNewSensor}>
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
              <button 
                className="btn btn-secondary btn-small"
                onClick={() => handleEdit(sensor)}
              >
                ✏️ Editar
              </button>
              <button 
                className="btn btn-danger btn-small"
                onClick={() => handleDelete(sensor.id)}
              >
                🗑️ Excluir
              </button>
            </div>
          </div>
        ))}
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => {
          setShowModal(false)
          setEditingId(null)
        }}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingId === null ? 'Novo Sensor' : 'Editar Sensor'}</h2>
              <button className="modal-close" onClick={() => {
                setShowModal(false)
                setEditingId(null)
              }}>×</button>
            </div>
            <form onSubmit={handleSubmit} className="modal-form">
              <div className="form-group">
                <label>Nome *</label>
                <input 
                  type="text" 
                  required
                  value={formData.nome || ''}
                  onChange={(e) => setFormData({...formData, nome: e.target.value})}
                  placeholder="Ex: Sensor de Pressão A"
                />
              </div>
              <div className="form-group">
                <label>Descrição</label>
                <textarea 
                  value={formData.descricao || ''}
                  onChange={(e) => setFormData({...formData, descricao: e.target.value})}
                  placeholder="Descrição do sensor"
                  rows={3}
                />
              </div>
              <div className="form-group">
                <label>Tipo *</label>
                <select
                  required
                  value={formData.tipo || 'pressao'}
                  onChange={(e) => setFormData({...formData, tipo: e.target.value})}
                >
                  <option value="pressao">Pressão</option>
                  <option value="carga">Carga</option>
                  <option value="temperatura">Temperatura</option>
                  <option value="vazao">Vazão</option>
                  <option value="outro">Outro</option>
                </select>
              </div>
              <div className="form-group">
                <label>Unidade</label>
                <input 
                  type="text" 
                  value={formData.unidade || ''}
                  onChange={(e) => setFormData({...formData, unidade: e.target.value})}
                  placeholder="Ex: bar, kg, °C, L/min"
                />
              </div>
              <div className="form-group">
                <label>Configuração Modbus</label>
                <select
                  value={formData.modbusConfigId || ''}
                  onChange={(e) => setFormData({
                    ...formData, 
                    modbusConfigId: e.target.value ? parseInt(e.target.value) : undefined
                  })}
                >
                  <option value="">Selecione uma configuração Modbus</option>
                  {modbusConfigs.map(config => (
                    <option key={config.id} value={config.id}>
                      {config.nome} {config.descricao ? `- ${config.descricao}` : ''}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label>
                  <input 
                    type="checkbox" 
                    checked={formData.ativo !== false}
                    onChange={(e) => setFormData({...formData, ativo: e.target.checked})}
                  />
                  {' '}Ativo
                </label>
              </div>
              <div className="modal-actions">
                <button 
                  type="button" 
                  className="btn btn-secondary" 
                  onClick={() => {
                    setShowModal(false)
                    setEditingId(null)
                  }}
                >
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  {editingId === null ? 'Criar' : 'Salvar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

export default Sensores

