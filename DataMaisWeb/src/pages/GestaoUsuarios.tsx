import { useState } from 'react'
import './GestaoUsuarios.css'

interface Usuario {
  id: number
  nome: string
  email: string
  perfil: 'Administrador' | 'Operador' | 'Visualizador'
  status: 'ativo' | 'inativo'
  ultimoAcesso: string
}

const GestaoUsuarios = () => {
  const [usuarios] = useState<Usuario[]>([
    {
      id: 1,
      nome: 'João Silva',
      email: 'joao.silva@modec.com',
      perfil: 'Administrador',
      status: 'ativo',
      ultimoAcesso: '22/12/2024 14:30'
    },
    {
      id: 2,
      nome: 'Maria Santos',
      email: 'maria.santos@modec.com',
      perfil: 'Operador',
      status: 'ativo',
      ultimoAcesso: '22/12/2024 13:15'
    },
    {
      id: 3,
      nome: 'Carlos Oliveira',
      email: 'carlos.oliveira@modec.com',
      perfil: 'Visualizador',
      status: 'ativo',
      ultimoAcesso: '21/12/2024 16:45'
    },
    {
      id: 4,
      nome: 'Ana Costa',
      email: 'ana.costa@modec.com',
      perfil: 'Operador',
      status: 'inativo',
      ultimoAcesso: '20/12/2024 10:20'
    }
  ])

  const [showModal, setShowModal] = useState(false)
  const [formData, setFormData] = useState<Partial<Usuario>>({})

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    // Aqui seria a lógica de salvar
    setShowModal(false)
    setFormData({})
  }

  return (
    <div className="gestao-usuarios">
      <div className="page-header">
        <div>
          <h1>Gestão de Usuários</h1>
          <p className="page-subtitle">Gerenciamento de usuários e permissões do sistema</p>
        </div>
        <button className="btn btn-primary" onClick={() => setShowModal(true)}>
          ➕ Novo Usuário
        </button>
      </div>

      <div className="usuarios-card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Nome</th>
                <th>Email</th>
                <th>Perfil</th>
                <th>Status</th>
                <th>Último Acesso</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {usuarios.map(usuario => (
                <tr key={usuario.id}>
                  <td><strong>{usuario.nome}</strong></td>
                  <td>{usuario.email}</td>
                  <td>
                    <span className={`perfil-badge perfil-${usuario.perfil.toLowerCase()}`}>
                      {usuario.perfil}
                    </span>
                  </td>
                  <td>
                    <span className={`status-badge ${usuario.status}`}>
                      {usuario.status === 'ativo' ? '● Ativo' : '○ Inativo'}
                    </span>
                  </td>
                  <td>{usuario.ultimoAcesso}</td>
                  <td>
                    <div className="action-buttons">
                      <button className="btn-icon" title="Editar">✏️</button>
                      <button className="btn-icon" title="Alterar Senha">🔒</button>
                      <button className="btn-icon" title="Excluir">🗑️</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Novo Usuário</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>
            <form onSubmit={handleSubmit} className="modal-form">
              <div className="form-group">
                <label>Nome Completo</label>
                <input 
                  type="text" 
                  required
                  value={formData.nome || ''}
                  onChange={(e) => setFormData({...formData, nome: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Email</label>
                <input 
                  type="email" 
                  required
                  value={formData.email || ''}
                  onChange={(e) => setFormData({...formData, email: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Perfil</label>
                <select
                  required
                  value={formData.perfil || ''}
                  onChange={(e) => setFormData({...formData, perfil: e.target.value as Usuario['perfil']})}
                >
                  <option value="">Selecione...</option>
                  <option value="Administrador">Administrador</option>
                  <option value="Operador">Operador</option>
                  <option value="Visualizador">Visualizador</option>
                </select>
              </div>
              <div className="form-group">
                <label>Senha</label>
                <input 
                  type="password" 
                  required
                  placeholder="Mínimo 8 caracteres"
                />
              </div>
              <div className="form-group">
                <label>Confirmar Senha</label>
                <input 
                  type="password" 
                  required
                  placeholder="Digite a senha novamente"
                />
              </div>
              <div className="modal-actions">
                <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  Salvar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

export default GestaoUsuarios


