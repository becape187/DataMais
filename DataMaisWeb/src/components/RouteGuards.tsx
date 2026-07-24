import type { ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import Layout from './Layout'
import { useAuth } from '../contexts/AuthContext'
import type { Role } from '../contexts/AuthContext'

/** Exige autenticação; renderiza o Layout + rota filha (Outlet). */
export const ProtectedLayout = () => {
  const { isAuthenticated, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return <div style={{ padding: 40 }}>Carregando...</div>
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }

  return (
    <Layout>
      <Outlet />
    </Layout>
  )
}

/** Restringe uma rota a determinados perfis; caso contrário volta ao dashboard. */
export const RequireRole = ({ roles, children }: { roles: Role[]; children: ReactNode }) => {
  const { usuario } = useAuth()

  if (!usuario || !roles.includes(usuario.role)) {
    return <Navigate to="/dashboard" replace />
  }

  return <>{children}</>
}
