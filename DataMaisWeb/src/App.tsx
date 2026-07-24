import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from './contexts/AuthContext'
import { ProtectedLayout, RequireRole } from './components/RouteGuards'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Ensaio from './pages/Ensaio'
import Clientes from './pages/Clientes'
import DetalhesCliente from './pages/DetalhesCliente'
import ConfiguracaoCilindro from './pages/ConfiguracaoCilindro'
import Sensores from './pages/Sensores'
import ConfiguracaoSensor from './pages/ConfiguracaoSensor'
import Relatorios from './pages/Relatorios'
import RelatoriosPorCliente from './pages/RelatoriosPorCliente'
import VisualizarRelatorio from './pages/VisualizarRelatorio'
import ComentariosDesvio from './pages/ComentariosDesvio'
import GestaoUsuarios from './pages/GestaoUsuarios'
import Configuracoes from './pages/Configuracoes'

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/login" element={<Login />} />

          <Route element={<ProtectedLayout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/ensaio" element={<Ensaio />} />
            <Route path="/ensaio/comentarios/:eventoId" element={<ComentariosDesvio />} />
            <Route path="/relatorios" element={<Relatorios />} />
            <Route path="/relatorios/cliente/:clienteId" element={<RelatoriosPorCliente />} />
            <Route path="/relatorios/:id" element={<VisualizarRelatorio />} />

            {/* Cadastros — visíveis para Admin e Operador (Operador só leitura; edição é bloqueada nas telas e no backend) */}
            <Route
              path="/clientes"
              element={<RequireRole roles={['Admin', 'Operador']}><Clientes /></RequireRole>}
            />
            <Route
              path="/clientes/:id"
              element={<RequireRole roles={['Admin', 'Operador']}><DetalhesCliente /></RequireRole>}
            />
            <Route
              path="/clientes/:clienteId/cilindros/:cilindroId"
              element={<RequireRole roles={['Admin', 'Operador']}><ConfiguracaoCilindro /></RequireRole>}
            />
            <Route
              path="/sensores"
              element={<RequireRole roles={['Admin', 'Operador']}><Sensores /></RequireRole>}
            />
            <Route
              path="/sensores/:id/configuracao"
              element={<RequireRole roles={['Admin', 'Operador']}><ConfiguracaoSensor /></RequireRole>}
            />

            {/* Somente Admin */}
            <Route
              path="/usuarios"
              element={<RequireRole roles={['Admin']}><GestaoUsuarios /></RequireRole>}
            />
            <Route
              path="/configuracoes"
              element={<RequireRole roles={['Admin']}><Configuracoes /></RequireRole>}
            />
          </Route>

          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  )
}

export default App
