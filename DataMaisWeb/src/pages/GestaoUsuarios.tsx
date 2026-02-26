import { useState, useEffect } from 'react'
import api from '../config/api'
import './GestaoUsuarios.css'

interface Usuario {
  id: number
  nome: string
  email: string
  role: string
  ativo: boolean
  ultimoAcesso: string | null
}

const GestaoUsuarios = () => {
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [loading, setLoading] = useState(true)
  const [showModal, setShowModal] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [formData, setFormData] = useState<{
    nome: string
    email: string
    role: string
    senha: string
    confirmarSenha: string
    ativo: boolean
  }>({
    nome: '',
    email: '',
    role: 'Usuario',
    senha: '',
    confirmarSenha: '',
    ativo: true
  })

  useEffect(() => {
    loadUsuarios()
  }, [])

  const loadUsuarios = async () => {
    try {
      setLoading(true)
      const response = await api.get('/usuario')
      setUsuarios(Array.isArray(response.data) ? response.data : [])
    } catch (error) {
      console.error('Erro ao carregar usuários:', error)
      setUsuarios([])
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()

    // Validações
    if (!formData.nome || !formData.email) {
      alert('Nome e email são obrigatórios')
      return
    }

    if (editingId === null && (!formData.senha || formData.senha.length < 8)) {
      alert('Senha deve ter no mínimo 8 caracteres')
      return
    }

    if (editingId === null && formData.senha !== formData.confirmarSenha) {
      alert('Senhas não coincidem')
      return
    }

    try {
      if (editingId === null) {
        // Criar novo usuário
        await api.post('/usuario', {
          nome: formData.nome,
          email: formData.email,
          role: formData.role,
          senha: formData.senha,
          ativo: formData.ativo
        })
      } else {
        // Atualizar usuário existente
        const updateData: any = {
          nome: formData.nome,
          email: formData.email,
          role: formData.role,
          ativo: formData.ativo
        }
        
        if (formData.senha && formData.senha.length >= 8) {
          if (formData.senha !== formData.confirmarSenha) {
            alert('Senhas não coincidem')
            return
          }
          updateData.senha = formData.senha
        }
        
        await api.put(`/usuario/${editingId}`, updateData)
      }

      await loadUsuarios()
      setShowModal(false)
      setEditingId(null)
      setFormData({
        nome: '',
        email: '',
        role: 'Usuario',
        senha: '',
        confirmarSenha: '',
        ativo: true
      })
    } catch (error: any) {
      console.error('Erro ao salvar usuário:', error)
      const message = error.response?.data?.message || 'Erro ao salvar usuário'
      alert(message)
    }
  }

  const handleEdit = (usuario: Usuario) => {
    setEditingId(usuario.id)
    setFormData({
      nome: usuario.nome,
      email: usuario.email,
      role: usuario.role || 'Usuario',
      senha: '',
      confirmarSenha: '',
      ativo: usuario.ativo
    })
    setShowModal(true)
  }

  const handleDelete = async (id: number) => {
    if (!confirm('Tem certeza que deseja excluir este usuário?')) {
      return
    }

    try {
      await api.delete(`/usuario/${id}`)
      await loadUsuarios()
    } catch (error) {
      console.error('Erro ao excluir usuário:', error)
      alert('Erro ao excluir usuário')
    }
  }

  const handleNew = () => {
    setEditingId(null)
    setFormData({
      nome: '',
      email: '',
      role: 'Usuario',
      senha: '',
      confirmarSenha: '',
      ativo: true
    })
    setShowModal(true)
  }

  const getPerfilLabel = (role: string) => {
    const roles: { [key: string]: string } = {
      'Admin': 'Administrador',
      'Usuario': 'Operador',
      'Operador': 'Operador',
      'Visualizador': 'Visualizador'
    }
    return roles[role] || role
  }

  if (loading) {
    return (
      <div className="gestao-usuarios">
        <div className="page-header">
          <h1>Gestão de Usuários</h1>
        </div>
        <div className="usuarios-card">
          <p>Carregando...</p>
        </div>
      </div>
    )
  }

  return (
    <div className="gestao-usuarios">
      <div className="page-header">
        <div>
          <h1>Gestão de Usuários</h1>
          <p className="page-subtitle">Gerenciamento de usuários e permissões do sistema</p>
        </div>
        <button className="btn btn-primary" onClick={handleNew}>
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
              {usuarios.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '2rem' }}>
                    Nenhum usuário cadastrado
                  </td>
                </tr>
              ) : (
                usuarios.map(usuario => (
                  <tr key={usuario.id}>
                    <td><strong>{usuario.nome}</strong></td>
                    <td>{usuario.email}</td>
                    <td>
                      <span className={`perfil-badge perfil-${(usuario.role || 'Usuario').toLowerCase()}`}>
                        {getPerfilLabel(usuario.role || 'Usuario')}
                      </span>
                    </td>
                    <td>
                      <span className={`status-badge ${usuario.ativo ? 'ativo' : 'inativo'}`}>
                        {usuario.ativo ? '● Ativo' : '○ Inativo'}
                      </span>
                    </td>
                    <td>{usuario.ultimoAcesso || 'Nunca'}</td>
                    <td>
                      <div className="action-buttons">
                        <button 
                          className="btn-icon" 
                          title="Editar"
                          onClick={() => handleEdit(usuario)}
                        >
                          ✏️
                        </button>
                        <button 
                          className="btn-icon" 
                          title="Excluir"
                          onClick={() => handleDelete(usuario.id)}
                        >
                          🗑️
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => {
          setShowModal(false)
          setEditingId(null)
        }}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingId === null ? 'Novo Usuário' : 'Editar Usuário'}</h2>
              <button className="modal-close" onClick={() => {
                setShowModal(false)
                setEditingId(null)
              }}>×</button>
            </div>
            <form onSubmit={handleSubmit} className="modal-form">
              <div className="form-group">
                <label>Nome Completo</label>
                <input 
                  type="text" 
                  required
                  value={formData.nome}
                  onChange={(e) => setFormData({...formData, nome: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Email</label>
                <input 
                  type="email" 
                  required
                  value={formData.email}
                  onChange={(e) => setFormData({...formData, email: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Perfil</label>
                <select
                  required
                  value={formData.role}
                  onChange={(e) => setFormData({...formData, role: e.target.value})}
                >
                  <option value="Usuario">Operador</option>
                  <option value="Admin">Administrador</option>
                  <option value="Operador">Operador</option>
                  <option value="Visualizador">Visualizador</option>
                </select>
              </div>
              <div className="form-group">
                <label>Senha {editingId !== null && '(deixe em branco para não alterar)'}</label>
                <input 
                  type="password" 
                  required={editingId === null}
                  placeholder="Mínimo 8 caracteres"
                  value={formData.senha}
                  onChange={(e) => setFormData({...formData, senha: e.target.value})}
                />
              </div>
              {(!editingId || formData.senha) && (
                <div className="form-group">
                  <label>Confirmar Senha</label>
                  <input 
                    type="password" 
                    required={editingId === null || formData.senha.length > 0}
                    placeholder="Digite a senha novamente"
                    value={formData.confirmarSenha}
                    onChange={(e) => setFormData({...formData, confirmarSenha: e.target.value})}
                  />
                </div>
              )}
              <div className="form-group">
                <label>
                  <input 
                    type="checkbox" 
                    checked={formData.ativo}
                    onChange={(e) => setFormData({...formData, ativo: e.target.checked})}
                  />
                  {' '}Usuário Ativo
                </label>
              </div>
              <div className="modal-actions">
                <button 
                  type="button" 
                  className="btn btn-secondary" 
                  onClick={() => {
                    setShowModal(false)
                    setEditingId(null)
                  }}
                >
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  {editingId === null ? 'Salvar' : 'Atualizar'}
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


