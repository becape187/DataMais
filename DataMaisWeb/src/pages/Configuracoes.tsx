import { useState, useEffect } from 'react'
import api from '../config/api'
import './Configuracoes.css'

interface AppConfig {
  database: {
    host: string
    port: number
    database: string
    username: string
    password: string
  }
  influx: {
    url: string
    token: string
    organization: string
    bucket: string
  }
  modbus: {
    timeoutMs: number
    retryCount: number
    poolingIntervalMs: number
  }
  modbusRegistros: ModbusRegistro[]
}

interface ModbusRegistro {
  id: number
  nome: string
  descricao?: string
  ipAddress: string
  port: number
  slaveId: number
  funcaoModbus: string
  enderecoRegistro: number
  quantidadeRegistros: number
  tipoDado: string
  byteOrder: string
  fatorConversao?: number
  offset?: number
  unidade?: string
  ordemLeitura: number
  ativo: boolean
}

const Configuracoes = () => {
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error', text: string } | null>(null)

  useEffect(() => {
    loadConfig()
  }, [])

  const loadConfig = async () => {
    try {
      const response = await api.get('/config')
      setConfig(response.data)
    } catch (error) {
      console.error('Erro ao carregar configurações:', error)
      setMessage({ type: 'error', text: 'Erro ao carregar configurações' })
    } finally {
      setLoading(false)
    }
  }

  const handleSave = async () => {
    if (!config) return

    setSaving(true)
    setMessage(null)

    try {
      await api.post('/config', config)
      setMessage({ type: 'success', text: 'Configurações salvas com sucesso!' })
    } catch (error) {
      setMessage({ type: 'error', text: 'Erro ao salvar configurações' })
    } finally {
      setSaving(false)
    }
  }

  const handleConfigChange = (section: keyof AppConfig, field: string, value: any) => {
    if (!config) return

    setConfig({
      ...config,
      [section]: {
        ...config[section],
        [field]: value
      }
    })
  }

  const handleRegistroChange = (id: number, field: keyof ModbusRegistro, value: any) => {
    if (!config) return

    setConfig({
      ...config,
      modbusRegistros: config.modbusRegistros.map(r =>
        r.id === id ? { ...r, [field]: value } : r
      )
    })
  }

  const handleAddRegistro = () => {
    if (!config) return

    const newId = Math.max(...config.modbusRegistros.map(r => r.id), 0) + 1
    const newRegistro: ModbusRegistro = {
      id: newId,
      nome: '',
      ipAddress: '192.168.1.100',
      port: 502,
      slaveId: 1,
      funcaoModbus: 'ReadHoldingRegisters',
      enderecoRegistro: 0,
      quantidadeRegistros: 1,
      tipoDado: 'UInt16',
      byteOrder: 'BigEndian',
      ordemLeitura: config.modbusRegistros.length + 1,
      ativo: true
    }

    setConfig({
      ...config,
      modbusRegistros: [...config.modbusRegistros, newRegistro]
    })
  }

  const handleRemoveRegistro = (id: number) => {
    if (!config) return

    setConfig({
      ...config,
      modbusRegistros: config.modbusRegistros.filter(r => r.id !== id)
    })
  }

  if (loading) {
    return <div className="configuracoes">Carregando...</div>
  }

  if (!config) {
    return <div className="configuracoes">Erro ao carregar configurações</div>
  }

  return (
    <div className="configuracoes">
      <div className="page-header">
        <h1>Configurações do Sistema</h1>
        <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
          {saving ? 'Salvando...' : '💾 Salvar Configurações'}
        </button>
      </div>

      {message && (
        <div className={`message message-${message.type}`}>
          {message.text}
        </div>
      )}

      <div className="config-sections">
        <section className="config-section">
          <h2>PostgreSQL</h2>
          <div className="form-grid">
            <div className="form-group">
              <label>Host</label>
              <input
                type="text"
                value={config.database.host}
                onChange={(e) => handleConfigChange('database', 'host', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Porta</label>
              <input
                type="number"
                value={config.database.port}
                onChange={(e) => handleConfigChange('database', 'port', parseInt(e.target.value))}
              />
            </div>
            <div className="form-group">
              <label>Database</label>
              <input
                type="text"
                value={config.database.database}
                onChange={(e) => handleConfigChange('database', 'database', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Usuário</label>
              <input
                type="text"
                value={config.database.username}
                onChange={(e) => handleConfigChange('database', 'username', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Senha</label>
              <input
                type="password"
                value={config.database.password}
                onChange={(e) => handleConfigChange('database', 'password', e.target.value)}
              />
            </div>
          </div>
        </section>

        <section className="config-section">
          <h2>InfluxDB</h2>
          <div className="form-grid">
            <div className="form-group">
              <label>URL</label>
              <input
                type="text"
                value={config.influx.url}
                onChange={(e) => handleConfigChange('influx', 'url', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Token</label>
              <input
                type="password"
                value={config.influx.token}
                onChange={(e) => handleConfigChange('influx', 'token', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Organization</label>
              <input
                type="text"
                value={config.influx.organization}
                onChange={(e) => handleConfigChange('influx', 'organization', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label>Bucket</label>
              <input
                type="text"
                value={config.influx.bucket}
                onChange={(e) => handleConfigChange('influx', 'bucket', e.target.value)}
              />
            </div>
          </div>
        </section>

        <section className="config-section">
          <h2>Modbus - Configurações Gerais</h2>
          <div className="form-grid">
            <div className="form-group">
              <label>Timeout (ms)</label>
              <input
                type="number"
                value={config.modbus.timeoutMs}
                onChange={(e) => handleConfigChange('modbus', 'timeoutMs', parseInt(e.target.value))}
              />
            </div>
            <div className="form-group">
              <label>Tentativas</label>
              <input
                type="number"
                value={config.modbus.retryCount}
                onChange={(e) => handleConfigChange('modbus', 'retryCount', parseInt(e.target.value))}
              />
            </div>
            <div className="form-group">
              <label>Intervalo Pooling (ms)</label>
              <input
                type="number"
                value={config.modbus.poolingIntervalMs}
                onChange={(e) => handleConfigChange('modbus', 'poolingIntervalMs', parseInt(e.target.value))}
              />
            </div>
          </div>
        </section>

        <section className="config-section">
          <div className="section-header">
            <h2>Registros Modbus</h2>
            <button className="btn btn-secondary" onClick={handleAddRegistro}>
              ➕ Adicionar Registro
            </button>
          </div>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Nome</th>
                  <th>IP</th>
                  <th>Porta</th>
                  <th>Slave ID</th>
                  <th>Função</th>
                  <th>Endereço</th>
                  <th>Tipo</th>
                  <th>Ordem</th>
                  <th>Ativo</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {config.modbusRegistros
                  .sort((a, b) => a.ordemLeitura - b.ordemLeitura)
                  .map(registro => (
                    <tr key={registro.id}>
                      <td>
                        <input
                          type="text"
                          value={registro.nome}
                          onChange={(e) => handleRegistroChange(registro.id, 'nome', e.target.value)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="text"
                          value={registro.ipAddress}
                          onChange={(e) => handleRegistroChange(registro.id, 'ipAddress', e.target.value)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.port}
                          onChange={(e) => handleRegistroChange(registro.id, 'port', parseInt(e.target.value))}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.slaveId}
                          onChange={(e) => handleRegistroChange(registro.id, 'slaveId', parseInt(e.target.value))}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <select
                          value={registro.funcaoModbus}
                          onChange={(e) => handleRegistroChange(registro.id, 'funcaoModbus', e.target.value)}
                          className="table-input"
                        >
                          <option value="ReadHoldingRegisters">Read Holding Registers</option>
                          <option value="ReadInputRegisters">Read Input Registers</option>
                          <option value="WriteSingleRegister">Write Single Register</option>
                          <option value="WriteSingleCoil">Write Single Coil</option>
                          <option value="ReadCoils">Read Coils</option>
                        </select>
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.enderecoRegistro}
                          onChange={(e) => handleRegistroChange(registro.id, 'enderecoRegistro', parseInt(e.target.value))}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <select
                          value={registro.tipoDado}
                          onChange={(e) => handleRegistroChange(registro.id, 'tipoDado', e.target.value)}
                          className="table-input"
                        >
                          <option value="UInt16">UInt16</option>
                          <option value="Int16">Int16</option>
                          <option value="UInt32">UInt32</option>
                          <option value="Int32">Int32</option>
                          <option value="Float">Float</option>
                          <option value="Boolean">Boolean</option>
                        </select>
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.ordemLeitura}
                          onChange={(e) => handleRegistroChange(registro.id, 'ordemLeitura', parseInt(e.target.value))}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          checked={registro.ativo}
                          onChange={(e) => handleRegistroChange(registro.id, 'ativo', e.target.checked)}
                        />
                      </td>
                      <td>
                        <button
                          className="btn-icon"
                          onClick={() => handleRemoveRegistro(registro.id)}
                          title="Remover"
                        >
                          🗑️
                        </button>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

export default Configuracoes
