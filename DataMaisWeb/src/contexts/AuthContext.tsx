import { createContext, useContext, useState, useEffect } from 'react'
import type { ReactNode } from 'react'
import api from '../config/api'

export type Role = 'Admin' | 'Operador' | 'Visualizador'

export interface Usuario {
  id: number
  nome: string
  email: string
  role: Role
}

interface AuthContextType {
  usuario: Usuario | null
  isAuthenticated: boolean
  loading: boolean
  login: (email: string, senha: string) => Promise<void>
  logout: () => void
  /** Admin */
  isAdmin: boolean
  /** Pode operar bomba/ensaio/relatório (Admin ou Operador) */
  podeOperar: boolean
}

const TOKEN_KEY = 'datamais_token'
const USUARIO_KEY = 'datamais_usuario'

export const getToken = () => localStorage.getItem(TOKEN_KEY)

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export const AuthProvider = ({ children }: { children: ReactNode }) => {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = getToken()
    const usuarioSalvo = localStorage.getItem(USUARIO_KEY)
    if (token && usuarioSalvo) {
      try {
        setUsuario(JSON.parse(usuarioSalvo))
      } catch {
        localStorage.removeItem(USUARIO_KEY)
      }
    }
    setLoading(false)
  }, [])

  const login = async (email: string, senha: string) => {
    const response = await api.post('/auth/login', { email, senha })
    const { token, usuario: u } = response.data
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(USUARIO_KEY, JSON.stringify(u))
    setUsuario(u)
  }

  const logout = () => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USUARIO_KEY)
    setUsuario(null)
    window.location.href = '/login'
  }

  const value: AuthContextType = {
    usuario,
    isAuthenticated: !!usuario,
    loading,
    login,
    logout,
    isAdmin: usuario?.role === 'Admin',
    podeOperar: usuario?.role === 'Admin' || usuario?.role === 'Operador',
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth deve ser usado dentro de AuthProvider')
  }
  return ctx
}
