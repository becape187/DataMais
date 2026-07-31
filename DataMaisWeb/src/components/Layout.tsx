import { useState, useEffect } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import api from '../config/api'
import { useAuth } from '../contexts/AuthContext'
import './Layout.css'

interface LayoutProps {
  children: ReactNode
}

interface ModbusRegistro {
  id: number
  nome: string
  ativo: boolean
}

// Versão do Layout - incrementar quando houver mudanças importantes
// v1.0.4 - Atualizado para usar PRESSAO_A_CONV e PRESSAO_B_CONV (valores já convertidos pelo dispositivo)

const Layout = ({ children }: LayoutProps) => {
  const location = useLocation()
  const { usuario, isAdmin, podeOperar, logout } = useAuth()
  const [isLigado, setIsLigado] = useState(false)
  const [processando, setProcessando] = useState(false)
  const [avancaPressionado, setAvancaPressionado] = useState(false)
  const [recuaPressionado, setRecuaPressionado] = useState(false)
  // Estado REAL lido do CLP (AUX_AVANCA/AUX_RECUA) — reflete acionamento por fora da app também
  const [avancaAtivo, setAvancaAtivo] = useState(false)
  const [recuaAtivo, setRecuaAtivo] = useState(false)
  const [pressaoA, setPressaoA] = useState<number | null>(null)
  const [pressaoB, setPressaoB] = useState<number | null>(null)
  const [registros, setRegistros] = useState<{
    avanca?: ModbusRegistro
    recua?: ModbusRegistro
    pressaoA?: ModbusRegistro
    pressaoB?: ModbusRegistro
    auxAvanca?: ModbusRegistro
    auxRecua?: ModbusRegistro
  }>({})

  const isActive = (path: string) => location.pathname === path

  // Busca os registros Modbus necessários
  useEffect(() => {
    const buscarRegistros = async () => {
      try {
        const response = await api.get('/ModbusConfig')
        const todosRegistros: ModbusRegistro[] = response.data
        
        const avancaReg = todosRegistros.find((r: any) => r.nome === 'BOTAO_AVANCA_IHM' && r.ativo)
        const recuaReg = todosRegistros.find((r: any) => r.nome === 'BOTAO_RECUA_IHM' && r.ativo)
        // Usa sempre as pressões já convertidas pelo dispositivo
        const pressaoAReg = todosRegistros.find((r: any) => r.nome === 'PRESSAO_A_CONV' && r.ativo)
        const pressaoBReg = todosRegistros.find((r: any) => r.nome === 'PRESSAO_B_CONV' && r.ativo)
        // Estado real de avanço/recuo (discrete inputs - ReadInputs), reflete acionamento externo
        const auxAvancaReg = todosRegistros.find((r: any) => r.nome === 'AUX_AVANCA' && r.ativo)
        const auxRecuaReg = todosRegistros.find((r: any) => r.nome === 'AUX_RECUA' && r.ativo)

        setRegistros({
          avanca: avancaReg,
          recua: recuaReg,
          pressaoA: pressaoAReg,
          pressaoB: pressaoBReg,
          auxAvanca: auxAvancaReg,
          auxRecua: auxRecuaReg
        })

        console.log('Registros Modbus encontrados na sidebar:', {
          avanca: avancaReg,
          recua: recuaReg,
          pressaoA: pressaoAReg,
          pressaoB: pressaoBReg,
          auxAvanca: auxAvancaReg,
          auxRecua: auxRecuaReg
        })
      } catch (err) {
        console.error('Erro ao buscar registros Modbus:', err)
      }
    }

    buscarRegistros()
  }, [])

  // Garante que os botões sejam desativados se a janela perder o foco
  useEffect(() => {
    const handleWindowBlur = () => {
      if (avancaPressionado) {
        handleAvancaUp()
      }
      if (recuaPressionado) {
        handleRecuaUp()
      }
    }

    window.addEventListener('blur', handleWindowBlur)
    return () => window.removeEventListener('blur', handleWindowBlur)
  }, [avancaPressionado, recuaPressionado])

  // Busca o status do motor ao carregar
  useEffect(() => {
    const abortController = new AbortController()
    let isMounted = true
    let requestInProgress = false

    const buscarStatusMotor = async () => {
      // Evita requisições simultâneas
      if (requestInProgress) {
        return
      }

      requestInProgress = true
      try {
        const response = await api.get('/ModbusConfig', {
          signal: abortController.signal
        })
        if (!isMounted) return
        
        const todosRegistros = response.data
        // Busca o registro de LEITURA do status do motor (ReadInputs - Input Discrete)
        // Existem dois registros MOTOR_BOMBA: um para leitura e outro para escrita
        const statusMotor = todosRegistros.find((r: any) => 
          r.nome === 'MOTOR_BOMBA' && r.ativo && r.funcaoModbus === 'ReadInputs'
        )
        
        if (statusMotor && isMounted) {
          const readResponse = await api.get(`/ModbusConfig/${statusMotor.id}/read`, {
            signal: abortController.signal
          })
          if (isMounted) {
            const valor = readResponse.data.valor
            setIsLigado(valor === true || valor === 1 || valor === '1')
          }
        }
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
          console.error('Erro ao buscar status do motor:', err)
        }
      } finally {
        requestInProgress = false
      }
    }

    buscarStatusMotor()
    // Atualiza a cada 2 segundos
    const interval = setInterval(buscarStatusMotor, 2000)
    
    return () => {
      isMounted = false
      abortController.abort()
      clearInterval(interval)
    }
  }, [])

  // Atualiza as pressões em tempo real a cada 1 segundo
  useEffect(() => {
    if (!registros.pressaoA && !registros.pressaoB) return

    const abortController = new AbortController()
    let isMounted = true
    let requestInProgress = false

    const atualizarPressoes = async () => {
      // Evita requisições simultâneas
      if (requestInProgress) {
        return
      }

      requestInProgress = true
      try {
        // Lê Pressão A
        if (registros.pressaoA && isMounted) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoA.id}/read`, {
              signal: abortController.signal
            })
            if (isMounted) {
              const valor = Number(response.data.valor)
              setPressaoA(isNaN(valor) ? null : valor)
            }
          } catch (err: any) {
            if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
              console.error('Erro ao ler Pressão A:', err)
              setPressaoA(null)
            }
          }
        }

        // Lê Pressão B
        if (registros.pressaoB && isMounted) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoB.id}/read`, {
              signal: abortController.signal
            })
            if (isMounted) {
              const valor = Number(response.data.valor)
              setPressaoB(isNaN(valor) ? null : valor)
            }
          } catch (err: any) {
            if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
              console.error('Erro ao ler Pressão B:', err)
              setPressaoB(null)
            }
          }
        }
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
          console.error('Erro ao atualizar pressões:', err)
        }
      } finally {
        requestInProgress = false
      }
    }

    // Atualiza imediatamente e depois a cada 1 segundo
    atualizarPressoes()
    const interval = setInterval(atualizarPressoes, 1000)

    return () => {
      isMounted = false
      abortController.abort()
      clearInterval(interval)
    }
  }, [registros.pressaoA?.id, registros.pressaoB?.id])

  // Lê o estado REAL de avanço/recuo (AUX_AVANCA/AUX_RECUA) a cada 1s.
  // Reflete o estado mesmo quando acionado por fora da aplicação (IHM, campo, etc.).
  useEffect(() => {
    if (!registros.auxAvanca && !registros.auxRecua) return

    const abortController = new AbortController()
    let isMounted = true
    let requestInProgress = false

    const ehAtivo = (valor: any) => valor === true || valor === 1 || valor === '1'

    const atualizarAux = async () => {
      if (requestInProgress) return
      requestInProgress = true
      try {
        if (registros.auxAvanca && isMounted) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.auxAvanca.id}/read`, {
              signal: abortController.signal
            })
            if (isMounted) setAvancaAtivo(ehAtivo(response.data.valor))
          } catch (err: any) {
            if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
              setAvancaAtivo(false)
            }
          }
        }

        if (registros.auxRecua && isMounted) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.auxRecua.id}/read`, {
              signal: abortController.signal
            })
            if (isMounted) setRecuaAtivo(ehAtivo(response.data.valor))
          } catch (err: any) {
            if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
              setRecuaAtivo(false)
            }
          }
        }
      } finally {
        requestInProgress = false
      }
    }

    atualizarAux()
    const interval = setInterval(atualizarAux, 1000)

    return () => {
      isMounted = false
      abortController.abort()
      clearInterval(interval)
    }
  }, [registros.auxAvanca?.id, registros.auxRecua?.id])

  const handleLigaDesliga = async () => {
    console.log('🔵 Botão Liga/Desliga da sidebar CLICADO!', { isLigado, processando })
    
    if (processando) {
      console.warn('⚠️ Já está processando, ignorando clique')
      return
    }

    try {
      setProcessando(true)
      const acao = isLigado ? 'desligar' : 'ligar'
      console.log(`📤 Enviando comando para ${acao} motor...`)
      
      const response = await api.post(`/ModbusConfig/motor/${acao}`)
      console.log('Resposta do servidor:', response.data)

      if (response.data.sucesso) {
        setIsLigado(!isLigado)
        console.log(`✅ Motor ${acao === 'ligar' ? 'ligado' : 'desligado'} com sucesso!`)
      } else {
        console.error('❌ Erro:', response.data.message)
        // Atualiza status se veio na resposta
        if (response.data.status !== undefined) {
          setIsLigado(response.data.status)
        }
        alert(response.data.message || 'Erro ao executar comando')
      }
    } catch (err: any) {
      console.error('❌ Erro ao executar comando:', err)
      console.error('Erro da API:', err.response?.data)
      
      // Tenta obter mensagem detalhada do backend
      let errorMsg = 'Erro ao executar comando'
      if (err.response?.data?.message) {
        errorMsg = err.response.data.message
      } else if (err.message) {
        errorMsg = err.message
      }
      
      // Adiciona informações adicionais se disponíveis
      if (err.response?.data?.ultimoStatusLido !== undefined) {
        errorMsg += ` (Status atual: ${err.response.data.ultimoStatusLido})`
      }
      if (err.response?.data?.tentativas) {
        errorMsg += ` (Tentativas: ${err.response.data.tentativas})`
      }
      
      // Atualiza status se veio na resposta (mesmo em caso de erro)
      if (err.response?.data?.status !== undefined) {
        setIsLigado(err.response.data.status)
      }
      
      alert(errorMsg)
    } finally {
      setProcessando(false)
    }
  }

  // Funções para Avança - botão momentâneo (pressionar = true, soltar = false)
  const handleAvancaDown = async () => {
    console.log('🔵 Botão Avança PRESSIONADO (MouseDown)!', { avancaPressionado, registro: registros.avanca })
    
    if (avancaPressionado || !registros.avanca) {
      return
    }

    try {
      setAvancaPressionado(true)
      console.log(`📤 Enviando TRUE para BOTAO_AVANCA_IHM (ID: ${registros.avanca.id})...`)
      await api.post(`/ModbusConfig/${registros.avanca.id}/write`, { valor: true })
      console.log('✅ TRUE enviado para Avança')
    } catch (err: any) {
      console.error('❌ Erro ao enviar TRUE para Avança:', err)
      setAvancaPressionado(false)
    }
  }

  const handleAvancaUp = async () => {
    console.log('🔵 Botão Avança SOLTO (MouseUp)!', { avancaPressionado, registro: registros.avanca })
    
    if (!avancaPressionado || !registros.avanca) {
      return
    }

    try {
      console.log(`📤 Enviando FALSE para BOTAO_AVANCA_IHM (ID: ${registros.avanca.id})...`)
      await api.post(`/ModbusConfig/${registros.avanca.id}/write`, { valor: false })
      console.log('✅ FALSE enviado para Avança')
    } catch (err: any) {
      console.error('❌ Erro ao enviar FALSE para Avança:', err)
    } finally {
      setAvancaPressionado(false)
    }
  }

  // Funções para Recua - botão momentâneo (pressionar = true, soltar = false)
  const handleRecuaDown = async () => {
    console.log('🔵 Botão Recua PRESSIONADO (MouseDown)!', { recuaPressionado, registro: registros.recua })
    
    if (recuaPressionado || !registros.recua) {
      return
    }

    try {
      setRecuaPressionado(true)
      console.log(`📤 Enviando TRUE para BOTAO_RECUA_IHM (ID: ${registros.recua.id})...`)
      await api.post(`/ModbusConfig/${registros.recua.id}/write`, { valor: true })
      console.log('✅ TRUE enviado para Recua')
    } catch (err: any) {
      console.error('❌ Erro ao enviar TRUE para Recua:', err)
      setRecuaPressionado(false)
    }
  }

  const handleRecuaUp = async () => {
    console.log('🔵 Botão Recua SOLTO (MouseUp)!', { recuaPressionado, registro: registros.recua })
    
    if (!recuaPressionado || !registros.recua) {
      return
    }

    try {
      console.log(`📤 Enviando FALSE para BOTAO_RECUA_IHM (ID: ${registros.recua.id})...`)
      await api.post(`/ModbusConfig/${registros.recua.id}/write`, { valor: false })
      console.log('✅ FALSE enviado para Recua')
    } catch (err: any) {
      console.error('❌ Erro ao enviar FALSE para Recua:', err)
    } finally {
      setRecuaPressionado(false)
    }
  }

  return (
    <div className="layout">
      <div className="top-bars">
        <div className="top-bar top-bar-red"></div>
        <div className="top-bar top-bar-blue"></div>
      </div>
      <div className="layout-content">
        <aside className="sidebar">
          <div className="sidebar-header">
            <img 
              src="/modec-logo.png" 
              alt="MODEC Logo" 
              className="logo"
            />
          </div>
          <nav className="sidebar-nav">
            <Link 
              to="/dashboard" 
              className={`nav-item ${isActive('/dashboard') ? 'active' : ''}`}
            >
              <span className="nav-icon">📊</span>
              Dashboard
            </Link>
            <Link 
              to="/ensaio" 
              className={`nav-item ${isActive('/ensaio') ? 'active' : ''}`}
            >
              <span className="nav-icon">📈</span>
              Ensaio
            </Link>
            {podeOperar && (
              <Link
                to="/clientes"
                className={`nav-item ${isActive('/clientes') ? 'active' : ''}`}
              >
                <span className="nav-icon">👥</span>
                Vessel/Frota
              </Link>
            )}
          {podeOperar && (
            <Link
              to="/sensores"
              className={`nav-item ${isActive('/sensores') ? 'active' : ''}`}
            >
              <span className="nav-icon">🔧</span>
              Sensores
            </Link>
          )}
          <Link
            to="/relatorios"
            className={`nav-item ${isActive('/relatorios') ? 'active' : ''}`}
          >
            <span className="nav-icon">📄</span>
            Relatórios
          </Link>
          {isAdmin && (
            <Link
              to="/usuarios"
              className={`nav-item ${isActive('/usuarios') ? 'active' : ''}`}
            >
              <span className="nav-icon">👤</span>
              Usuários
            </Link>
          )}
          {isAdmin && (
            <Link
              to="/configuracoes"
              className={`nav-item ${isActive('/configuracoes') ? 'active' : ''}`}
            >
              <span className="nav-icon">⚙️</span>
              Configurações
            </Link>
          )}
        </nav>
        
        {podeOperar && (
        <div className="hidraulica-controls">
          <div className="hidraulica-display">
            <div className="display-row">
              <div className="display-box pressao-box">
                <span className="display-value pressao-value">
                  {pressaoA !== null ? Math.round(pressaoA) : '--'}<span className="display-unit">bar</span>
                </span>
              </div>
              <div className="display-box pressao-box">
                <span className="display-value pressao-value">
                  {pressaoB !== null ? Math.round(pressaoB) : '--'}<span className="display-unit">bar</span>
                </span>
              </div>
            </div>
            <div className="display-row">
              <div className="display-box carga-box">
                <span className="display-value">
                  0.0<span className="display-unit">ton</span>
                </span>
              </div>
              <div className="display-box carga-box">
                <span className="display-value">
                  0.0<span className="display-unit">ton</span>
                </span>
              </div>
            </div>
          </div>
          <div className="hidraulica-buttons">
            <div className="movimento-buttons">
              <button
                className={`btn-hidraulica btn-avanca ${avancaAtivo ? 'ativo' : ''}`}
                onMouseDown={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  handleAvancaDown()
                }}
                onMouseUp={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  handleAvancaUp()
                }}
                onMouseLeave={(e) => {
                  // Se o mouse sair do botão enquanto está pressionado, também desativa
                  if (avancaPressionado) {
                    e.preventDefault()
                    handleAvancaUp()
                  }
                }}
                disabled={!registros.avanca}
                title={avancaAtivo ? 'Avanço ATIVADO' : 'Avanço desativado'}
                style={{
                  cursor: !registros.avanca ? 'not-allowed' : 'pointer',
                  opacity: !registros.avanca ? 0.6 : (avancaPressionado ? 0.8 : 1)
                }}
              >
                <span className={`led-status ${avancaAtivo ? 'on' : 'off'}`}></span>
                Avança
              </button>
              <button
                className={`btn-hidraulica btn-recua ${recuaAtivo ? 'ativo' : ''}`}
                onMouseDown={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  handleRecuaDown()
                }}
                onMouseUp={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  handleRecuaUp()
                }}
                onMouseLeave={(e) => {
                  // Se o mouse sair do botão enquanto está pressionado, também desativa
                  if (recuaPressionado) {
                    e.preventDefault()
                    handleRecuaUp()
                  }
                }}
                disabled={!registros.recua}
                title={recuaAtivo ? 'Recuo ATIVADO' : 'Recuo desativado'}
                style={{
                  cursor: !registros.recua ? 'not-allowed' : 'pointer',
                  opacity: !registros.recua ? 0.6 : (recuaPressionado ? 0.8 : 1)
                }}
              >
                <span className={`led-status ${recuaAtivo ? 'on' : 'off'}`}></span>
                Recua
              </button>
            </div>
            <button 
              className={`btn-hidraulica btn-liga-desliga ${isLigado ? 'ligado' : ''}`}
              onClick={(e) => {
                e.preventDefault()
                e.stopPropagation()
                console.log('🔵 onClick Liga/Desliga executado!', { isLigado, processando })
                handleLigaDesliga()
              }}
              disabled={processando}
              style={{ 
                cursor: processando ? 'not-allowed' : 'pointer',
                opacity: processando ? 0.6 : 1
              }}
            >
              {processando ? '⏳ Processando...' : (isLigado ? 'Desliga' : 'Liga')}
            </button>
          </div>
        </div>
        )}

        <div className="sidebar-user">
          <div className="sidebar-user-info">
            <span className="sidebar-user-name">{usuario?.nome}</span>
            <span className="sidebar-user-role">{usuario?.role}</span>
          </div>
          <button className="sidebar-logout" onClick={logout} title="Sair">
            ⎋ Sair
          </button>
        </div>
        </aside>
        <main className="main-content">
          {children}
        </main>
      </div>
    </div>
  )
}

export default Layout

