import { useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import './Layout.css'

interface LayoutProps {
  children: ReactNode
}

const Layout = ({ children }: LayoutProps) => {
  const location = useLocation()
  const [isLigado, setIsLigado] = useState(false)

  const isActive = (path: string) => location.pathname === path

  const handleLigaDesliga = () => {
    setIsLigado(!isLigado)
    // Aqui seria a lógica de ligar/desligar o sistema
  }

  const handleAvança = () => {
    // Aqui seria a lógica de avançar
  }

  const handleRecua = () => {
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
              onClick={handleLigaDesliga}
            >
              {isLigado ? 'Desliga' : 'Liga'}
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

