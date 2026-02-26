import { useState, useEffect } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import api from '../config/api'
import './Layout.css'

interface LayoutProps {
  children: ReactNode
}

const Layout = ({ children }: LayoutProps) => {
  const location = useLocation()
  const [isLigado, setIsLigado] = useState(false)
  const [processando, setProcessando] = useState(false)

  const isActive = (path: string) => location.pathname === path

  // Busca o status do motor ao carregar
  useEffect(() => {
    const buscarStatusMotor = async () => {
      try {
        const response = await api.get('/ModbusConfig')
        const todosRegistros = response.data
        const statusMotor = todosRegistros.find((r: any) => r.nome === 'MOTOR_BOMBA' && r.ativo)
        
        if (statusMotor) {
          const readResponse = await api.get(`/ModbusConfig/${statusMotor.id}/read`)
          const valor = readResponse.data.valor
          setIsLigado(valor === true || valor === 1 || valor === '1')
        }
      } catch (err) {
        console.error('Erro ao buscar status do motor:', err)
      }
    }

    buscarStatusMotor()
    // Atualiza a cada 2 segundos
    const interval = setInterval(buscarStatusMotor, 2000)
    return () => clearInterval(interval)
  }, [])

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
      }
    } catch (err: any) {
      console.error('❌ Erro ao executar comando:', err)
      const errorMsg = err.response?.data?.message || err.message || 'Erro ao executar comando'
      alert(errorMsg)
    } finally {
      setProcessando(false)
    }
  }

  const handleAvança = () => {
    console.log('🔵 Botão Avança da sidebar CLICADO!')
    // Aqui seria a lógica de avançar
  }

  const handleRecua = () => {
    console.log('🔵 Botão Recua da sidebar CLICADO!')
    // Aqui seria a lógica de recuar
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
            <Link 
              to="/clientes" 
              className={`nav-item ${isActive('/clientes') ? 'active' : ''}`}
            >
              <span className="nav-icon">👥</span>
              Clientes
            </Link>
          <Link 
            to="/sensores" 
            className={`nav-item ${isActive('/sensores') ? 'active' : ''}`}
          >
            <span className="nav-icon">🔧</span>
            Sensores
          </Link>
          <Link 
            to="/relatorios"
            className={`nav-item ${isActive('/relatorios') ? 'active' : ''}`}
          >
            <span className="nav-icon">📄</span>
            Relatórios
          </Link>
          <Link 
            to="/usuarios" 
            className={`nav-item ${isActive('/usuarios') ? 'active' : ''}`}
          >
            <span className="nav-icon">👤</span>
            Usuários
          </Link>
          <Link 
            to="/configuracoes" 
            className={`nav-item ${isActive('/configuracoes') ? 'active' : ''}`}
          >
            <span className="nav-icon">⚙️</span>
            Configurações
          </Link>
        </nav>
        
        <div className="hidraulica-controls">
          <div className="hidraulica-display">
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
            <div className="display-row">
              <div className="display-box pressao-box">
                <span className="display-value pressao-value">
                  0.0<span className="display-unit">bar</span>
                </span>
              </div>
              <div className="display-box pressao-box">
                <span className="display-value pressao-value">
                  0.0<span className="display-unit">bar</span>
                </span>
              </div>
            </div>
          </div>
          <div className="hidraulica-buttons">
            <div className="movimento-buttons">
              <button className="btn-hidraulica btn-avanca" onClick={handleAvança}>
                Avança
              </button>
              <button className="btn-hidraulica btn-recua" onClick={handleRecua}>
                Recua
              </button>
            </div>
            <button 
              className={`btn-hidraulica btn-liga-desliga ${isLigado ? 'ligado' : ''}`}
              onClick={(e) => {
                e.preventDefault()
                e.stopPropagation()
                console.log('🔵 onClick handler executado!', { isLigado, processando })
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
        </aside>
        <main className="main-content">
          {children}
        </main>
      </div>
    </div>
  )
}

export default Layout

