import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import Dashboard from './pages/Dashboard'
import Ensaio from './pages/Ensaio'
import Clientes from './pages/Clientes'
import DetalhesCliente from './pages/DetalhesCliente'
import ConfiguracaoCilindro from './pages/ConfiguracaoCilindro'
import Sensores from './pages/Sensores'
import ConfiguracaoSensor from './pages/ConfiguracaoSensor'
import Relatorios from './pages/Relatorios'
import VisualizarRelatorio from './pages/VisualizarRelatorio'
import ComentariosDesvio from './pages/ComentariosDesvio'
import GestaoUsuarios from './pages/GestaoUsuarios'
import Configuracoes from './pages/Configuracoes'

function App() {
  return (
    <Router>
      <Layout>
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/ensaio" element={<Ensaio />} />
          <Route path="/clientes" element={<Clientes />} />
          <Route path="/clientes/:id" element={<DetalhesCliente />} />
          <Route path="/clientes/:clienteId/cilindros/:cilindroId" element={<ConfiguracaoCilindro />} />
          <Route path="/sensores" element={<Sensores />} />
          <Route path="/sensores/:id/configuracao" element={<ConfiguracaoSensor />} />
          <Route path="/relatorios" element={<Relatorios />} />
          <Route path="/relatorios/:id" element={<VisualizarRelatorio />} />
          <Route path="/ensaio/comentarios/:eventoId" element={<ComentariosDesvio />} />
          <Route path="/usuarios" element={<GestaoUsuarios />} />
          <Route path="/configuracoes" element={<Configuracoes />} />
        </Routes>
      </Layout>
    </Router>
  )
}

export default App

