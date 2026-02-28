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
}

interface CampoRelatorio {
  id: number
  nome: string
  tipoResposta: 'SimOuNao' | 'TextoSimples' | 'MultiplasLinhas'
  ordem: number
  dataCriacao: string
  dataExclusao?: string | null
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
  const [camposRelatorio, setCamposRelatorio] = useState<CampoRelatorio[]>([])
  const [modbusRegistros, setModbusRegistros] = useState<ModbusRegistro[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error', text: string } | null>(null)
  const [importing, setImporting] = useState(false)
  const [showImportDialog, setShowImportDialog] = useState(false)
  const [showCampoModal, setShowCampoModal] = useState(false)
  const [campoEditando, setCampoEditando] = useState<CampoRelatorio | null>(null)
  const [novoCampo, setNovoCampo] = useState({ nome: '', tipoResposta: 'SimOuNao' as CampoRelatorio['tipoResposta'] })
  const [importConfig, setImportConfig] = useState({
    ipAddress: 'modec.automais.cloud',
    port: 502,
    slaveId: 1,
    replaceExisting: false
  })

  useEffect(() => {
    loadAllData()
  }, [])

  const loadAllData = async () => {
    setLoading(true)
    try {
      await Promise.all([loadConfig(), loadCamposRelatorio(), loadModbusRegistros()])
    } catch (error) {
      console.error('Erro ao carregar dados:', error)
    } finally {
      setLoading(false)
    }
  }

  const loadCamposRelatorio = async () => {
    try {
      const response = await api.get('/CampoRelatorio')
      const campos = response.data || []
      setCamposRelatorio(campos)
    } catch (error) {
      console.error('Erro ao carregar campos do relatório:', error)
      setMessage({ type: 'error', text: 'Erro ao carregar campos do relatório' })
    }
  }

  const loadConfig = async () => {
    try {
      const response = await api.get('/config')
      const configData = response.data
      // Remove modbusRegistros do config pois agora vem do banco
      if (configData && configData.modbusRegistros) {
        delete configData.modbusRegistros
      }
      setConfig(configData)
    } catch (error) {
      console.error('Erro ao carregar configurações:', error)
      setMessage({ type: 'error', text: 'Erro ao carregar configurações' })
    }
  }

  const loadModbusRegistros = async () => {
    try {
      const response = await api.get('/ModbusConfig')
      const registros = response.data || []
      // Mapeia os dados do backend (PascalCase) para camelCase do frontend
      const mappedRegistros = registros.map((r: any) => ({
        id: r.id || r.Id,
        nome: r.nome || r.Nome,
        descricao: r.descricao || r.Descricao,
        ipAddress: r.ipAddress || r.IpAddress,
        port: r.port || r.Port,
        slaveId: r.slaveId || r.SlaveId,
        funcaoModbus: r.funcaoModbus || r.FuncaoModbus,
        enderecoRegistro: r.enderecoRegistro || r.EnderecoRegistro,
        quantidadeRegistros: r.quantidadeRegistros || r.QuantidadeRegistros || 1,
        tipoDado: r.tipoDado || r.TipoDado,
        byteOrder: r.byteOrder || r.ByteOrder,
        fatorConversao: r.fatorConversao || r.FatorConversao,
        offset: r.offset || r.Offset,
        unidade: r.unidade || r.Unidade,
        ordemLeitura: r.ordemLeitura || r.OrdemLeitura,
        ativo: r.ativo !== undefined ? r.ativo : (r.Ativo !== undefined ? r.Ativo : true)
      }))
      setModbusRegistros(mappedRegistros)
    } catch (error) {
      console.error('Erro ao carregar registros Modbus:', error)
      setMessage({ type: 'error', text: 'Erro ao carregar registros Modbus' })
    }
  }

  const handleSave = async () => {
    if (!config) return

    setSaving(true)
    setMessage(null)

    try {
      // Salva apenas as configurações gerais (sem modbusRegistros)
      const configToSave = { ...config }
      await api.post('/config', configToSave)
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
    setModbusRegistros(prevRegistros =>
      prevRegistros.map(r =>
        r.id === id ? { ...r, [field]: value } : r
      )
    )
  }

  const handleSaveRegistro = async (registro: ModbusRegistro) => {
    try {
      // Converte de camelCase para PascalCase (formato esperado pelo backend)
      const registroToSend = {
        Id: registro.id > 0 ? registro.id : 0,
        Nome: registro.nome,
        Descricao: registro.descricao,
        IpAddress: registro.ipAddress,
        Port: registro.port,
        SlaveId: registro.slaveId,
        FuncaoModbus: registro.funcaoModbus,
        EnderecoRegistro: registro.enderecoRegistro,
        QuantidadeRegistros: registro.quantidadeRegistros,
        TipoDado: registro.tipoDado,
        ByteOrder: registro.byteOrder,
        FatorConversao: registro.fatorConversao,
        Offset: registro.offset,
        Unidade: registro.unidade,
        OrdemLeitura: registro.ordemLeitura,
        Ativo: registro.ativo
      }

      if (registro.id > 0) {
        // Atualizar registro existente
        await api.put(`/ModbusConfig/${registro.id}`, registroToSend)
        setMessage({ type: 'success', text: 'Registro Modbus atualizado com sucesso!' })
      } else {
        // Criar novo registro - remove Id para criação
        const { Id, ...registroToCreate } = registroToSend
        const response = await api.post('/ModbusConfig', registroToCreate)
        // Mapeia a resposta de volta para camelCase
        const newRegistro = {
          id: response.data.id || response.data.Id,
          nome: response.data.nome || response.data.Nome,
          descricao: response.data.descricao || response.data.Descricao,
          ipAddress: response.data.ipAddress || response.data.IpAddress,
          port: response.data.port || response.data.Port,
          slaveId: response.data.slaveId || response.data.SlaveId,
          funcaoModbus: response.data.funcaoModbus || response.data.FuncaoModbus,
          enderecoRegistro: response.data.enderecoRegistro || response.data.EnderecoRegistro,
          quantidadeRegistros: response.data.quantidadeRegistros || response.data.QuantidadeRegistros || 1,
          tipoDado: response.data.tipoDado || response.data.TipoDado,
          byteOrder: response.data.byteOrder || response.data.ByteOrder,
          fatorConversao: response.data.fatorConversao || response.data.FatorConversao,
          offset: response.data.offset || response.data.Offset,
          unidade: response.data.unidade || response.data.Unidade,
          ordemLeitura: response.data.ordemLeitura || response.data.OrdemLeitura,
          ativo: response.data.ativo !== undefined ? response.data.ativo : (response.data.Ativo !== undefined ? response.data.Ativo : true)
        }
        // Atualizar o ID do registro local com o retornado pelo servidor
        setModbusRegistros(prevRegistros =>
          prevRegistros.map(r => r === registro ? newRegistro : r)
        )
        setMessage({ type: 'success', text: 'Registro Modbus criado com sucesso!' })
      }
      // Recarregar registros para garantir sincronização
      await loadModbusRegistros()
    } catch (error) {
      console.error('Erro ao salvar registro Modbus:', error)
      setMessage({ type: 'error', text: 'Erro ao salvar registro Modbus' })
    }
  }

  const handleAddRegistro = () => {
    const maxId = modbusRegistros.length > 0 ? Math.max(...modbusRegistros.map(r => r.id), 0) : 0
    const newId = -(maxId + 1) // ID temporário negativo para novos registros
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
      ordemLeitura: modbusRegistros.length + 1,
      ativo: true
    }

    setModbusRegistros([...modbusRegistros, newRegistro])
  }

  const handleRemoveRegistro = async (id: number) => {
    try {
      // Se for um registro novo (ID negativo), apenas remove do estado
      if (id < 0) {
        setModbusRegistros(prevRegistros => prevRegistros.filter(r => r.id !== id))
        return
      }

      // Se for um registro existente, deleta do banco
      await api.delete(`/ModbusConfig/${id}`)
      setModbusRegistros(prevRegistros => prevRegistros.filter(r => r.id !== id))
      setMessage({ type: 'success', text: 'Registro Modbus removido com sucesso!' })
    } catch (error) {
      console.error('Erro ao remover registro Modbus:', error)
      setMessage({ type: 'error', text: 'Erro ao remover registro Modbus' })
    }
  }

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    if (!file.name.endsWith('.json')) {
      setMessage({ type: 'error', text: 'Por favor, selecione um arquivo JSON' })
      return
    }

    const reader = new FileReader()
    reader.onload = async (e) => {
      try {
        const jsonContent = e.target?.result as string
        await handleImportJson(jsonContent)
      } catch (error) {
        console.error('Erro ao ler arquivo:', error)
        setMessage({ type: 'error', text: 'Erro ao ler arquivo JSON' })
      }
    }
    reader.readAsText(file)
    
    // Limpa o input para permitir selecionar o mesmo arquivo novamente
    event.target.value = ''
  }

  const handleImportJson = async (jsonContent: string) => {
    setImporting(true)
    setMessage(null)

    try {
      // Valida se é um JSON válido
      JSON.parse(jsonContent)

      const requestData = {
        jsonContent: jsonContent,
        ipAddress: importConfig.ipAddress || undefined,
        port: importConfig.port || undefined,
        slaveId: importConfig.slaveId || undefined,
        replaceExisting: importConfig.replaceExisting || undefined
      }

      const response = await api.post('/ModbusConfig/import', requestData)
      
      // Mostra mensagem de sucesso com resumo
      const resumo = response.data.resumo || {}
      const mensagem = response.data.message || 'Importação realizada com sucesso!'
      const detalhes = resumo.total ? `\n${resumo.novos || 0} novos, ${resumo.atualizados || 0} atualizados` : ''
      
      setMessage({ 
        type: 'success', 
        text: `${mensagem}${detalhes}` 
      })

      // Fecha o diálogo e recarrega os registros
      setShowImportDialog(false)
      await loadModbusRegistros()
    } catch (error: any) {
      console.error('Erro ao importar JSON:', error)
      const errorMessage = error.response?.data?.message || error.message || 'Erro ao importar arquivo JSON'
      setMessage({ type: 'error', text: errorMessage })
    } finally {
      setImporting(false)
    }
  }

  const handleImportClick = () => {
    setShowImportDialog(true)
  }

  const handleAddCampo = () => {
    setCampoEditando(null)
    setNovoCampo({ nome: '', tipoResposta: 'SimOuNao' })
    setShowCampoModal(true)
  }

  const handleEditCampo = (campo: CampoRelatorio) => {
    setCampoEditando(campo)
    setNovoCampo({ nome: campo.nome, tipoResposta: campo.tipoResposta })
    setShowCampoModal(true)
  }

  const handleSaveCampo = async () => {
    if (!novoCampo.nome.trim()) {
      setMessage({ type: 'error', text: 'O nome do campo é obrigatório' })
      return
    }

    try {
      if (campoEditando) {
        // Atualizar campo existente
        await api.put(`/CampoRelatorio/${campoEditando.id}`, {
          nome: novoCampo.nome.trim(),
          tipoResposta: novoCampo.tipoResposta
        })
        setMessage({ type: 'success', text: 'Campo atualizado com sucesso!' })
      } else {
        // Criar novo campo
        await api.post('/CampoRelatorio', {
          nome: novoCampo.nome.trim(),
          tipoResposta: novoCampo.tipoResposta
        })
        setMessage({ type: 'success', text: 'Campo criado com sucesso!' })
      }
      setShowCampoModal(false)
      await loadCamposRelatorio()
    } catch (error: any) {
      console.error('Erro ao salvar campo:', error)
      const errorMessage = error.response?.data?.message || 'Erro ao salvar campo'
      setMessage({ type: 'error', text: errorMessage })
    }
  }

  const handleDeleteCampo = async (id: number) => {
    if (!window.confirm('Tem certeza que deseja excluir este campo? Ele não aparecerá em novos relatórios, mas permanecerá nos relatórios antigos.')) {
      return
    }

    try {
      await api.delete(`/CampoRelatorio/${id}`)
      setMessage({ type: 'success', text: 'Campo excluído com sucesso!' })
      await loadCamposRelatorio()
    } catch (error: any) {
      console.error('Erro ao excluir campo:', error)
      const errorMessage = error.response?.data?.message || 'Erro ao excluir campo'
      setMessage({ type: 'error', text: errorMessage })
    }
  }

  const handleMoveCampo = async (campoId: number, direction: 'up' | 'down') => {
    const camposOrdenados = [...camposRelatorio].sort((a, b) => a.ordem - b.ordem)
    const index = camposOrdenados.findIndex(c => c.id === campoId)
    
    if (index === -1) return
    
    if (direction === 'up' && index === 0) return
    if (direction === 'down' && index === camposOrdenados.length - 1) return

    const newIndex = direction === 'up' ? index - 1 : index + 1
    const camposIds = camposOrdenados.map(c => c.id)
    ;[camposIds[index], camposIds[newIndex]] = [camposIds[newIndex], camposIds[index]]

    try {
      await api.post('/CampoRelatorio/reordenar', { camposIds })
      await loadCamposRelatorio()
    } catch (error: any) {
      console.error('Erro ao reordenar campos:', error)
      setMessage({ type: 'error', text: 'Erro ao reordenar campos' })
    }
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
          <div className="section-header">
            <h2>Campos do Relatório</h2>
            <button className="btn btn-secondary" onClick={handleAddCampo}>
              ➕ Adicionar Campo
            </button>
          </div>

          {showCampoModal && (
            <div className="import-dialog">
              <div className="import-dialog-content">
                <h3>{campoEditando ? 'Editar Campo' : 'Adicionar Campo'}</h3>
                <div className="form-grid">
                  <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                    <label>Nome do Campo (Pergunta)</label>
                    <input
                      type="text"
                      value={novoCampo.nome}
                      onChange={(e) => setNovoCampo({ ...novoCampo, nome: e.target.value })}
                      placeholder="Ex: Houve algum defeito na haste?"
                    />
                  </div>
                  <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                    <label>Tipo de Resposta</label>
                    <select
                      value={novoCampo.tipoResposta}
                      onChange={(e) => setNovoCampo({ ...novoCampo, tipoResposta: e.target.value as CampoRelatorio['tipoResposta'] })}
                    >
                      <option value="SimOuNao">Sim ou Não</option>
                      <option value="TextoSimples">Texto Simples</option>
                      <option value="MultiplasLinhas">Múltiplas Linhas</option>
                    </select>
                  </div>
                </div>
                <div className="import-dialog-actions">
                  <button 
                    className="btn btn-secondary" 
                    onClick={() => setShowCampoModal(false)}
                  >
                    Cancelar
                  </button>
                  <button 
                    className="btn btn-primary" 
                    onClick={handleSaveCampo}
                  >
                    Salvar
                  </button>
                </div>
              </div>
            </div>
          )}

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '50px' }}>Ordem</th>
                  <th>Nome do Campo</th>
                  <th>Tipo de Resposta</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {camposRelatorio
                  .filter(c => !c.dataExclusao)
                  .sort((a, b) => a.ordem - b.ordem)
                  .map(campo => (
                    <tr key={campo.id}>
                      <td>
                        <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
                          <button
                            className="btn-icon"
                            onClick={() => handleMoveCampo(campo.id, 'up')}
                            title="Mover para cima"
                            style={{ padding: '4px', fontSize: '12px' }}
                          >
                            ⬆️
                          </button>
                          <span style={{ minWidth: '20px', textAlign: 'center' }}>{campo.ordem}</span>
                          <button
                            className="btn-icon"
                            onClick={() => handleMoveCampo(campo.id, 'down')}
                            title="Mover para baixo"
                            style={{ padding: '4px', fontSize: '12px' }}
                          >
                            ⬇️
                          </button>
                        </div>
                      </td>
                      <td>{campo.nome}</td>
                      <td>
                        {campo.tipoResposta === 'SimOuNao' && 'Sim ou Não'}
                        {campo.tipoResposta === 'TextoSimples' && 'Texto Simples'}
                        {campo.tipoResposta === 'MultiplasLinhas' && 'Múltiplas Linhas'}
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <button
                            className="btn-icon"
                            onClick={() => handleEditCampo(campo)}
                            title="Editar"
                          >
                            ✏️
                          </button>
                          <button
                            className="btn-icon"
                            onClick={() => handleDeleteCampo(campo.id)}
                            title="Excluir"
                          >
                            🗑️
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                {camposRelatorio.filter(c => !c.dataExclusao).length === 0 && (
                  <tr>
                    <td colSpan={4} style={{ textAlign: 'center', padding: '20px' }}>
                      Nenhum campo encontrado. Clique em "Adicionar Campo" para criar um novo.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
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
            <div className="section-actions">
              <button className="btn btn-secondary" onClick={handleImportClick}>
                📤 Importar JSON
              </button>
              <button className="btn btn-secondary" onClick={handleAddRegistro}>
                ➕ Adicionar Registro
              </button>
            </div>
          </div>

          {showImportDialog && (
            <div className="import-dialog">
              <div className="import-dialog-content">
                <h3>Importar Configuração Modbus (JSON)</h3>
                <div className="form-grid">
                  <div className="form-group">
                    <label>IP Address</label>
                    <input
                      type="text"
                      value={importConfig.ipAddress}
                      onChange={(e) => setImportConfig({ ...importConfig, ipAddress: e.target.value })}
                      placeholder="modec.automais.cloud"
                    />
                  </div>
                  <div className="form-group">
                    <label>Porta</label>
                    <input
                      type="number"
                      value={importConfig.port}
                      onChange={(e) => setImportConfig({ ...importConfig, port: parseInt(e.target.value) || 502 })}
                      placeholder="502"
                    />
                  </div>
                  <div className="form-group">
                    <label>Slave ID</label>
                    <input
                      type="number"
                      value={importConfig.slaveId}
                      onChange={(e) => setImportConfig({ ...importConfig, slaveId: parseInt(e.target.value) || 1 })}
                      placeholder="1"
                    />
                  </div>
                  <div className="form-group">
                    <label>
                      <input
                        type="checkbox"
                        checked={importConfig.replaceExisting}
                        onChange={(e) => setImportConfig({ ...importConfig, replaceExisting: e.target.checked })}
                      />
                      {' '}Substituir registros existentes do mesmo IP
                    </label>
                  </div>
                </div>
                <div className="import-file-section">
                  <input
                    type="file"
                    accept=".json"
                    onChange={handleFileSelect}
                    id="json-file-input"
                    style={{ display: 'none' }}
                  />
                  <label htmlFor="json-file-input" className="btn btn-primary file-input-label">
                    {importing ? '⏳ Importando...' : '📁 Selecionar Arquivo JSON'}
                  </label>
                </div>
                <div className="import-dialog-actions">
                  <button 
                    className="btn btn-secondary" 
                    onClick={() => setShowImportDialog(false)}
                    disabled={importing}
                  >
                    Cancelar
                  </button>
                </div>
              </div>
            </div>
          )}
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
                {modbusRegistros
                  .sort((a, b) => a.ordemLeitura - b.ordemLeitura)
                  .map(registro => (
                    <tr key={registro.id}>
                      <td>
                        <input
                          type="text"
                          value={registro.nome}
                          onChange={(e) => handleRegistroChange(registro.id, 'nome', e.target.value)}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="text"
                          value={registro.ipAddress}
                          onChange={(e) => handleRegistroChange(registro.id, 'ipAddress', e.target.value)}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.port}
                          onChange={(e) => handleRegistroChange(registro.id, 'port', parseInt(e.target.value))}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.slaveId}
                          onChange={(e) => handleRegistroChange(registro.id, 'slaveId', parseInt(e.target.value))}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <select
                          value={registro.funcaoModbus}
                          onChange={(e) => handleRegistroChange(registro.id, 'funcaoModbus', e.target.value)}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        >
                          <option value="ReadHoldingRegisters">Read Holding Registers</option>
                          <option value="ReadInputRegisters">Read Input Registers</option>
                          <option value="ReadCoils">Read Coils</option>
                          <option value="ReadInputs">Read Discrete Inputs (0x02)</option>
                          <option value="WriteSingleRegister">Write Single Register</option>
                          <option value="WriteSingleCoil">Write Single Coil</option>
                        </select>
                      </td>
                      <td>
                        <input
                          type="number"
                          value={registro.enderecoRegistro}
                          onChange={(e) => handleRegistroChange(registro.id, 'enderecoRegistro', parseInt(e.target.value))}
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <select
                          value={registro.tipoDado}
                          onChange={(e) => handleRegistroChange(registro.id, 'tipoDado', e.target.value)}
                          onBlur={() => handleSaveRegistro(registro)}
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
                          onBlur={() => handleSaveRegistro(registro)}
                          className="table-input"
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          checked={registro.ativo}
                          onChange={(e) => {
                            handleRegistroChange(registro.id, 'ativo', e.target.checked)
                            handleSaveRegistro({ ...registro, ativo: e.target.checked })
                          }}
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
                {modbusRegistros.length === 0 && (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: '20px' }}>
                      Nenhum registro Modbus encontrado. Clique em "Adicionar Registro" para criar um novo.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

export default Configuracoes
