import { useState, useEffect } from 'react'
import api from '../config/api'
import './ControleHidraulico.css'

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

interface Sensor {
  id: number
  nome: string
  tipo: string
  inputMin?: number
  outputMin?: number
  inputMax?: number
  outputMax?: number
  modbusConfigId?: number
  modbusConfig?: {
    id: number
    nome: string
  }
  ativo: boolean
}

// Versão da página - incrementar quando houver mudanças importantes
const VERSAO_PAGINA = '1.0.3'

const ControleHidraulico = () => {
  console.log(`🟢 Componente ControleHidraulico renderizado - Versão ${VERSAO_PAGINA}`)
  
  // Estados dos registros Modbus
  const [registros, setRegistros] = useState<{
    ligaMotor?: ModbusRegistro
    desligaMotor?: ModbusRegistro
    ligaRadiador?: ModbusRegistro
    desligaRadiador?: ModbusRegistro
    avancaCilindro?: ModbusRegistro
    recuaCilindro?: ModbusRegistro
    statusMotor?: ModbusRegistro
    pressaoA?: ModbusRegistro
    pressaoB?: ModbusRegistro
    pressaoAConv?: ModbusRegistro
    pressaoBConv?: ModbusRegistro
    pressaoGeralConv?: ModbusRegistro
  }>({})

  // Estados dos sensores
  const [sensores, setSensores] = useState<{
    sensorA?: Sensor
    sensorB?: Sensor
    pressaoGeral?: Sensor
  }>({})

  // Estados de controle
  const [motorStatus, setMotorStatus] = useState(false)
  const [pressaoA, setPressaoA] = useState<number | null>(null)
  const [pressaoB, setPressaoB] = useState<number | null>(null)
  const [pressaoGeral, setPressaoGeral] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error', text: string } | null>(null)

  // Busca os registros Modbus específicos e sensores
  useEffect(() => {
    const buscarRegistrosESensores = async () => {
      try {
        setLoading(true)
        setError(null)

        // Busca registros Modbus
        const responseModbus = await api.get('/ModbusConfig')
        const todosRegistros: ModbusRegistro[] = responseModbus.data

        const registrosEncontrados: typeof registros = {}

        // Busca cada registro pelo nome exato
        todosRegistros.forEach(reg => {
          if (!reg.ativo) return

          switch (reg.nome) {
            case 'BOTAO_LIGA_MOTOR':
              registrosEncontrados.ligaMotor = reg
              break
            case 'BOTAO_DESLIGA_MOTOR':
              registrosEncontrados.desligaMotor = reg
              break
            case 'BOTAO_LIGA_RADIADOR':
              registrosEncontrados.ligaRadiador = reg
              break
            case 'BOTAO_DESLIGA_RADIADOR':
              registrosEncontrados.desligaRadiador = reg
              break
            case 'BOTAO_AVANCA_IHM':
              registrosEncontrados.avancaCilindro = reg
              break
            case 'BOTAO_RECUA_IHM':
              registrosEncontrados.recuaCilindro = reg
              break
            case 'MOTOR_BOMBA':
              registrosEncontrados.statusMotor = reg
              break
            case 'PRESSAO_A':
              registrosEncontrados.pressaoA = reg
              break
            case 'PRESSAO_B':
              registrosEncontrados.pressaoB = reg
              break
            case 'PRESSAO_A_CONV':
              registrosEncontrados.pressaoAConv = reg
              break
            case 'PRESSAO_B_CONV':
              registrosEncontrados.pressaoBConv = reg
              break
            case 'PRESSAO_GERAL_CONV':
              registrosEncontrados.pressaoGeralConv = reg
              break
          }
        })

        setRegistros(registrosEncontrados)

        // Busca sensores
        try {
          const responseSensores = await api.get('/Sensor')
          const todosSensores: Sensor[] = responseSensores.data

          const sensoresEncontrados: typeof sensores = {}

          // Busca sensores por nome (A, B e pressão geral)
          todosSensores.forEach(sensor => {
            if (!sensor.ativo) return

            const nomeUpper = sensor.nome.toUpperCase()
            if (nomeUpper.includes('A') && nomeUpper.includes('PRESSÃO')) {
              sensoresEncontrados.sensorA = sensor
            } else if (nomeUpper.includes('B') && nomeUpper.includes('PRESSÃO')) {
              sensoresEncontrados.sensorB = sensor
            } else if (nomeUpper.includes('GERAL') || nomeUpper.includes('PRESSÃO GERAL')) {
              sensoresEncontrados.pressaoGeral = sensor
            }
          })

          // Se não encontrou por nome, tenta buscar por tipo e ordem
          if (!sensoresEncontrados.sensorA || !sensoresEncontrados.sensorB) {
            const sensoresPressao = todosSensores.filter(s => 
              s.ativo && s.tipo.toLowerCase().includes('pressão')
            )
            
            if (sensoresPressao.length >= 1 && !sensoresEncontrados.sensorA) {
              sensoresEncontrados.sensorA = sensoresPressao[0]
            }
            if (sensoresPressao.length >= 2 && !sensoresEncontrados.sensorB) {
              sensoresEncontrados.sensorB = sensoresPressao[1]
            }
            if (sensoresPressao.length >= 3 && !sensoresEncontrados.pressaoGeral) {
              sensoresEncontrados.pressaoGeral = sensoresPressao[2]
            }
          }

          setSensores(sensoresEncontrados)
          console.log('Sensores encontrados:', sensoresEncontrados)
        } catch (err: any) {
          console.warn('Erro ao buscar sensores:', err)
          // Não bloqueia a aplicação se não encontrar sensores
        }

        console.log('Registros Modbus encontrados:', registrosEncontrados)

        // Verifica quais registros não foram encontrados
        const registrosFaltando = []
        if (!registrosEncontrados.ligaMotor) registrosFaltando.push('BOTAO_LIGA_MOTOR')
        if (!registrosEncontrados.desligaMotor) registrosFaltando.push('BOTAO_DESLIGA_MOTOR')
        if (!registrosEncontrados.ligaRadiador) registrosFaltando.push('BOTAO_LIGA_RADIADOR')
        if (!registrosEncontrados.desligaRadiador) registrosFaltando.push('BOTAO_DESLIGA_RADIADOR')
        if (!registrosEncontrados.avancaCilindro) registrosFaltando.push('BOTAO_AVANCA_IHM')
        if (!registrosEncontrados.recuaCilindro) registrosFaltando.push('BOTAO_RECUA_IHM')
        if (!registrosEncontrados.statusMotor) registrosFaltando.push('MOTOR_BOMBA')
        if (!registrosEncontrados.pressaoAConv) registrosFaltando.push('PRESSAO_A_CONV')
        if (!registrosEncontrados.pressaoBConv) registrosFaltando.push('PRESSAO_B_CONV')
        if (!registrosEncontrados.pressaoGeralConv) registrosFaltando.push('PRESSAO_GERAL_CONV')

        if (registrosFaltando.length > 0) {
          console.warn('Registros Modbus não encontrados:', registrosFaltando)
          setError(`Registros Modbus não encontrados: ${registrosFaltando.join(', ')}`)
        } else {
          console.log('Todos os registros Modbus foram encontrados com sucesso!')
        }
      } catch (err: any) {
        console.error('Erro ao buscar registros Modbus:', err)
        setError('Erro ao buscar registros Modbus: ' + (err.response?.data?.message || err.message))
      } finally {
        setLoading(false)
      }
    }

    buscarRegistrosESensores()
  }, [])

  // Controle de erros para evitar logs repetitivos
  const [ultimoErroLogado, setUltimoErroLogado] = useState<{ [key: string]: number }>({})

  const logarErroSeNecessario = (chave: string, mensagem: string, err: any) => {
    const agora = Date.now()
    const ultimoErro = ultimoErroLogado[chave] || 0
    
    // Só loga erros a cada 30 segundos para evitar spam no console
    if (agora - ultimoErro > 30000) {
      const isTimeout = err?.code === 'ECONNABORTED' || err?.message?.includes('timeout')
      if (isTimeout) {
        console.warn(`${mensagem} (timeout - tentando reconectar...)`)
      } else {
        console.error(mensagem, err)
      }
      setUltimoErroLogado({ ...ultimoErroLogado, [chave]: agora })
    }
  }

  // Atualiza status do motor e pressões periodicamente
  useEffect(() => {
    if (!registros.statusMotor && !registros.pressaoAConv && !registros.pressaoBConv && !registros.pressaoGeralConv) return

    const atualizarStatus = async () => {
      try {
        // Lê status do motor
        if (registros.statusMotor) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.statusMotor.id}/read`)
            const valor = response.data.valor
            setMotorStatus(valor === true || valor === 1 || valor === '1')
          } catch (err: any) {
            logarErroSeNecessario('motor', 'Erro ao buscar status do motor', err)
            // Não altera o status em caso de erro para manter o último valor conhecido
          }
        }

        // Lê pressão A convertida diretamente do Modbus
        if (registros.pressaoAConv) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoAConv.id}/read`, { timeout: 15000 })
            setPressaoA(Number(response.data.valor))
          } catch (err: any) {
            logarErroSeNecessario('pressaoA', 'Erro ao ler pressão A convertida', err)
          }
        }

        // Lê pressão B convertida diretamente do Modbus
        if (registros.pressaoBConv) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoBConv.id}/read`, { timeout: 15000 })
            setPressaoB(Number(response.data.valor))
          } catch (err: any) {
            logarErroSeNecessario('pressaoB', 'Erro ao ler pressão B convertida', err)
          }
        }

        // Lê pressão geral convertida diretamente do Modbus
        if (registros.pressaoGeralConv) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoGeralConv.id}/read`, { timeout: 15000 })
            setPressaoGeral(Number(response.data.valor))
          } catch (err: any) {
            logarErroSeNecessario('pressaoGeral', 'Erro ao ler pressão geral convertida', err)
          }
        }
      } catch (err: any) {
        logarErroSeNecessario('geral', 'Erro ao atualizar status', err)
      }
    }

    atualizarStatus()
    const interval = setInterval(atualizarStatus, 2000) // Atualiza a cada 2 segundos

    return () => clearInterval(interval)
  }, [
    registros.statusMotor?.id, 
    registros.pressaoAConv?.id, 
    registros.pressaoBConv?.id,
    registros.pressaoGeralConv?.id
  ])

  const escreverRegistro = async (
    registro: ModbusRegistro, 
    valor: boolean | number, 
    nomeAcao: string,
    mostrarMensagem: boolean = false
  ) => {
    if (!registro) {
      if (mostrarMensagem) {
        setMessage({ type: 'error', text: `Registro Modbus não configurado para ${nomeAcao}` })
      }
      return false
    }

    try {
      // Só atualiza saving se for uma ação principal (não para ações internas)
      if (mostrarMensagem) {
        setSaving(nomeAcao)
        setError(null)
        setMessage(null)
      }

      console.log(`Enviando comando ${nomeAcao}:`, {
        registroId: registro.id,
        nome: registro.nome,
        funcaoModbus: registro.funcaoModbus,
        tipoDado: registro.tipoDado,
        endereco: registro.enderecoRegistro,
        valor: valor,
        valorTipo: typeof valor
      })

      const response = await api.post(`/ModbusConfig/${registro.id}/write`, { valor })
      
      console.log(`Resposta do servidor para ${nomeAcao}:`, response.data)

      if (mostrarMensagem) {
        setMessage({ 
          type: 'success', 
          text: `${nomeAcao} executado com sucesso!` 
        })
        setTimeout(() => setMessage(null), 3000)
      }

      return true
    } catch (err: any) {
      console.error(`Erro ao ${nomeAcao}:`, err)
      console.error('Detalhes do erro:', {
        status: err.response?.status,
        data: err.response?.data,
        message: err.message
      })
      
      if (mostrarMensagem) {
        const errorMsg = err.response?.data?.message || err.response?.data?.error || err.message || `Erro ao ${nomeAcao}`
        setError(errorMsg)
        setMessage({ type: 'error', text: errorMsg })
      }
      
      return false
    } finally {
      if (mostrarMensagem) {
        setSaving(null)
      }
    }
  }


  const ligarMotor = async () => {
    console.log('🚀 ligarMotor chamado!', { saving, loading })
    try {
      setSaving('Ligar Motor')
      setError(null)
      setMessage(null)

      console.log('📤 Enviando comando para ligar motor...')
      const response = await api.post('/ModbusConfig/motor/ligar')
      
      console.log('Resposta do servidor:', response.data)

      if (response.data.sucesso) {
        setMotorStatus(true)
        setMessage({ 
          type: 'success', 
          text: response.data.message || 'Motor ligado com sucesso!' 
        })
        setTimeout(() => setMessage(null), 3000)
      } else {
        setMessage({ 
          type: 'error', 
          text: response.data.message || 'Erro ao ligar motor' 
        })
        setError(response.data.message || 'Erro ao ligar motor')
        // Atualiza status se veio na resposta
        if (response.data.status !== undefined) {
          setMotorStatus(response.data.status)
        }
      }
    } catch (err: any) {
      console.error('Erro ao ligar motor:', err)
      const errorMsg = err.response?.data?.message || err.message || 'Erro ao ligar motor'
      setError(errorMsg)
      setMessage({ type: 'error', text: errorMsg })
    } finally {
      setSaving(null)
    }
  }

  const desligarMotor = async () => {
    console.log('🚀 desligarMotor chamado!', { saving, loading })
    try {
      setSaving('Desligar Motor')
      setError(null)
      setMessage(null)

      console.log('📤 Enviando comando para desligar motor...')
      const response = await api.post('/ModbusConfig/motor/desligar')
      
      console.log('Resposta do servidor:', response.data)

      if (response.data.sucesso) {
        setMotorStatus(false)
        setMessage({ 
          type: 'success', 
          text: response.data.message || 'Motor desligado com sucesso!' 
        })
        setTimeout(() => setMessage(null), 3000)
      } else {
        setMessage({ 
          type: 'error', 
          text: response.data.message || 'Erro ao desligar motor' 
        })
        setError(response.data.message || 'Erro ao desligar motor')
        // Atualiza status se veio na resposta
        if (response.data.status !== undefined) {
          setMotorStatus(response.data.status)
        }
      }
    } catch (err: any) {
      console.error('Erro ao desligar motor:', err)
      const errorMsg = err.response?.data?.message || err.message || 'Erro ao desligar motor'
      setError(errorMsg)
      setMessage({ type: 'error', text: errorMsg })
    } finally {
      setSaving(null)
    }
  }

  const ligarRadiador = async () => {
    try {
      setSaving('Ligar Radiador')
      setError(null)
      setMessage(null)

      const response = await api.post('/ModbusConfig/radiador/ligar')
      
      if (response.data.sucesso) {
        setMessage({ 
          type: 'success', 
          text: response.data.message || 'Radiador ligado com sucesso!' 
        })
        setTimeout(() => setMessage(null), 3000)
      } else {
        setMessage({ 
          type: 'error', 
          text: response.data.message || 'Erro ao ligar radiador' 
        })
        setError(response.data.message || 'Erro ao ligar radiador')
      }
    } catch (err: any) {
      console.error('Erro ao ligar radiador:', err)
      const errorMsg = err.response?.data?.message || err.message || 'Erro ao ligar radiador'
      setError(errorMsg)
      setMessage({ type: 'error', text: errorMsg })
    } finally {
      setSaving(null)
    }
  }

  const desligarRadiador = async () => {
    try {
      setSaving('Desligar Radiador')
      setError(null)
      setMessage(null)

      const response = await api.post('/ModbusConfig/radiador/desligar')
      
      if (response.data.sucesso) {
        setMessage({ 
          type: 'success', 
          text: response.data.message || 'Radiador desligado com sucesso!' 
        })
        setTimeout(() => setMessage(null), 3000)
      } else {
        setMessage({ 
          type: 'error', 
          text: response.data.message || 'Erro ao desligar radiador' 
        })
        setError(response.data.message || 'Erro ao desligar radiador')
      }
    } catch (err: any) {
      console.error('Erro ao desligar radiador:', err)
      const errorMsg = err.response?.data?.message || err.message || 'Erro ao desligar radiador'
      setError(errorMsg)
      setMessage({ type: 'error', text: errorMsg })
    } finally {
      setSaving(null)
    }
  }
  const avancarCilindro = () => escreverRegistro(registros.avancaCilindro!, true, 'Avançar Cilindro', true)
  const recuarCilindro = () => escreverRegistro(registros.recuaCilindro!, true, 'Recuar Cilindro', true)

  return (
    <div className="controle-hidraulico">
      <div className="page-header">
        <h1>Controle da Unidade Hidráulica</h1>
        <p className="page-subtitle">Controle manual do motor, radiador e cilindro</p>
        <div className="version-badge">
          v{VERSAO_PAGINA}
        </div>
      </div>

      {message && (
        <div className={`message ${message.type === 'success' ? 'message-success' : 'message-error'}`}>
          {message.text}
        </div>
      )}

      {error && (
        <div className="message message-error">
          {error}
        </div>
      )}

      {loading && (
        <div className="loading-message">
          Carregando configuração Modbus...
        </div>
      )}

      {/* Debug info */}
      {import.meta.env.DEV && (
        <div style={{ padding: '10px', background: '#f0f0f0', marginBottom: '10px', fontSize: '12px' }}>
          <strong>Debug:</strong> loading={loading ? 'true' : 'false'}, saving={saving || 'null'}
        </div>
      )}

      <div className="controle-grid">
        {/* Card do Motor */}
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
              className="btn btn-success"
              onClick={(e) => {
                e.preventDefault()
                e.stopPropagation()
                console.log('🔵 Botão Ligar Motor CLICADO!', { 
                  saving, 
                  loading, 
                  disabled: saving !== null || loading,
                  buttonEnabled: !(saving !== null || loading)
                })
                if (!loading && saving === null) {
                  ligarMotor()
                } else {
                  console.warn('⚠️ Botão desabilitado!', { loading, saving })
                }
              }}
              disabled={saving !== null || loading}
              style={{ 
                cursor: (saving !== null || loading) ? 'not-allowed' : 'pointer',
                pointerEvents: (saving !== null || loading) ? 'none' : 'auto'
              }}
            >
              {saving === 'Ligar Motor' ? '⏳ Processando...' : '▶️ Ligar Motor'}
            </button>
            <button 
              className="btn btn-danger"
              onClick={(e) => {
                e.preventDefault()
                e.stopPropagation()
                console.log('🔴 Botão Desligar Motor CLICADO!', { 
                  saving, 
                  loading, 
                  disabled: saving !== null || loading,
                  buttonEnabled: !(saving !== null || loading)
                })
                if (!loading && saving === null) {
                  desligarMotor()
                } else {
                  console.warn('⚠️ Botão desabilitado!', { loading, saving })
                }
              }}
              disabled={saving !== null || loading}
              style={{ 
                cursor: (saving !== null || loading) ? 'not-allowed' : 'pointer',
                pointerEvents: (saving !== null || loading) ? 'none' : 'auto'
              }}
            >
              {saving === 'Desligar Motor' ? '⏳ Processando...' : '⏹️ Desligar Motor'}
            </button>
          </div>

          <div className="info-panel">
            <div className="info-item">
              <span className="info-label">Status:</span>
              <span className="info-value">{motorStatus ? 'Ligado' : 'Desligado'}</span>
            </div>
            <div className="info-item">
              <span className="info-label">Tensão:</span>
              <span className="info-value">380V</span>
            </div>
            <div className="info-item">
              <span className="info-label">Corrente:</span>
              <span className="info-value">{motorStatus ? '15.2 A' : '0 A'}</span>
            </div>
          </div>
        </div>

        {/* Card do Radiador */}
        <div className="controle-card">
          <div className="controle-header">
            <h2>Radiador</h2>
            <div className="status-indicator-large">
              <span className="status-dot"></span>
              Controle Manual
            </div>
          </div>
          
          <div className="controle-actions">
            <button 
              className="btn btn-success"
              onClick={ligarRadiador}
              disabled={saving !== null || loading}
            >
              {saving === 'Ligar Radiador' ? '⏳ Processando...' : '▶️ Ligar Radiador'}
            </button>
            <button 
              className="btn btn-danger"
              onClick={desligarRadiador}
              disabled={saving !== null || loading}
            >
              {saving === 'Desligar Radiador' ? '⏳ Processando...' : '⏹️ Desligar Radiador'}
            </button>
          </div>
        </div>

        {/* Card do Cilindro */}
        <div className="controle-card">
          <div className="controle-header">
            <h2>Controle do Cilindro</h2>
            <div className="status-indicator-large">
              <span className="status-dot"></span>
              Controle Manual
            </div>
          </div>

          <div className="controle-actions">
            <button 
              className="btn btn-success"
              onClick={() => {
                console.log('Botão Avançar Cilindro clicado', { 
                  registro: registros.avancaCilindro,
                  saving,
                  loading,
                  motorStatus 
                })
                avancarCilindro()
              }}
              disabled={saving !== null || !registros.avancaCilindro || loading || !motorStatus}
              title={
                !motorStatus ? 'Motor deve estar ligado' : 
                !registros.avancaCilindro ? 'Registro Modbus não encontrado' : ''
              }
            >
              {saving === 'Avançar Cilindro' ? '⏳ Processando...' : '⬆️ Avançar Cilindro'}
            </button>
            <button 
              className="btn btn-warning"
              onClick={() => {
                console.log('Botão Recuar Cilindro clicado', { 
                  registro: registros.recuaCilindro,
                  saving,
                  loading,
                  motorStatus 
                })
                recuarCilindro()
              }}
              disabled={saving !== null || !registros.recuaCilindro || loading || !motorStatus}
              title={
                !motorStatus ? 'Motor deve estar ligado' : 
                !registros.recuaCilindro ? 'Registro Modbus não encontrado' : ''
              }
            >
              {saving === 'Recuar Cilindro' ? '⏳ Processando...' : '⬇️ Recuar Cilindro'}
            </button>
          </div>

          <div className="info-panel">
            <div className="info-item">
              <span className="info-label">Pressão A:</span>
              <span className="info-value">
                {pressaoA !== null ? `${pressaoA.toFixed(2)} bar` : '--'}
                {sensores.sensorA && ` (${sensores.sensorA.nome})`}
              </span>
            </div>
            <div className="info-item">
              <span className="info-label">Pressão B:</span>
              <span className="info-value">
                {pressaoB !== null ? `${pressaoB.toFixed(2)} bar` : '--'}
                {sensores.sensorB && ` (${sensores.sensorB.nome})`}
              </span>
            </div>
            {pressaoGeral !== null && (
              <div className="info-item">
                <span className="info-label">Pressão Geral:</span>
                <span className="info-value">
                  {pressaoGeral.toFixed(2)} bar
                  {sensores.pressaoGeral && ` (${sensores.pressaoGeral.nome})`}
                </span>
              </div>
            )}
            <div className="info-item">
              <span className="info-label">Motor:</span>
              <span className="info-value">{motorStatus ? 'Ligado' : 'Desligado'}</span>
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
            <div className="cilindro-diagram">
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
