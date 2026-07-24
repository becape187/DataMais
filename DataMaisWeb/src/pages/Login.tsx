import { useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import './Login.css'

const Login = () => {
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const destino = (location.state as { from?: string })?.from || '/dashboard'

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErro(null)

    if (!email || !senha) {
      setErro('Informe usuário e senha.')
      return
    }

    try {
      setCarregando(true)
      await login(email.trim(), senha)
      navigate(destino, { replace: true })
    } catch (err: any) {
      const msg = err.response?.data?.message || 'Não foi possível entrar. Verifique suas credenciais.'
      setErro(msg)
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-top-bars">
        <div className="login-bar login-bar-red"></div>
        <div className="login-bar login-bar-blue"></div>
      </div>
      <div className="login-card">
        <img src="/modec-logo.png" alt="MODEC" className="login-logo" />
        <h1 className="login-title">DataMais</h1>
        <p className="login-subtitle">Ensaios Hidráulicos</p>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="login-field">
            <label>Usuário</label>
            <input
              type="text"
              autoFocus
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin"
            />
          </div>
          <div className="login-field">
            <label>Senha</label>
            <input
              type="password"
              autoComplete="current-password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              placeholder="••••••••"
            />
          </div>

          {erro && <div className="login-erro">{erro}</div>}

          <button type="submit" className="login-btn" disabled={carregando}>
            {carregando ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}

export default Login
