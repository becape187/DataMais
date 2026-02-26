import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../config/api'
import './Clientes.css'

interface Cliente {
  id: number
  nome: string
  cnpj: string
  contato: string
  email: string
}

const Clientes = () => {
  const navigate = useNavigate()
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [loading, setLoading] = useState(true)
  const [searchTerm, setSearchTerm] = useState('')

  useEffect(() => {
    loadClientes()
  }, [])

  const loadClientes = async () => {
    try {
      setLoading(true)
      const response = await api.get('/cliente')
      // Garante que sempre seja um array
      setClientes(Array.isArray(response.data) ? response.data : [])
    } catch (error) {
      console.error('Erro ao carregar clientes:', error)
      setClientes([]) // Garante array vazio em caso de erro
    } finally {
      setLoading(false)
    }
  }

  const [showModal, setShowModal] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [formData, setFormData] = useState<Partial<Cliente>>({})

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      if (editingId === null) {
        await api.post('/cliente', formData)
      } else {
        await api.put(`/cliente/${editingId}`, formData)
      }
      await loadClientes()
      setShowModal(false)
      setEditingId(null)
      setFormData({})
    } catch (error: any) {
      console.error('Erro ao salvar cliente:', error)
      const message = error.response?.data?.message || 'Erro ao salvar cliente'
      alert(message)
    }
  }

  const handleEdit = (cliente: Cliente) => {
    setEditingId(cliente.id)
    setFormData({
      nome: cliente.nome,
      cnpj: cliente.cnpj || '',
      contato: cliente.contato || '',
      email: cliente.email || ''
    })
    setShowModal(true)
  }

  const handleDelete = async (id: number, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm('Tem certeza que deseja excluir este cliente?')) {
      return
    }

    try {
      await api.delete(`/cliente/${id}`)
      await loadClientes()
    } catch (error: any) {
      console.error('Erro ao excluir cliente:', error)
      const message = error.response?.data?.message || 'Erro ao excluir cliente'
      alert(message)
    }
  }

  const handleNew = () => {
    setEditingId(null)
    setFormData({})
    setShowModal(true)
  }

  const filteredClientes = (Array.isArray(clientes) ? clientes : []).filter(cliente =>
    cliente?.nome?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    cliente?.cnpj?.includes(searchTerm) ||
    cliente?.contato?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    cliente?.email?.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const handleClienteClick = (clienteId: number) => {
    navigate(`/clientes/${clienteId}`)
  }

  if (loading) {
    return (
      <div className="clientes">
        <div className="page-header">
          <h1>Cadastro de Clientes</h1>
        </div>
        <div className="clientes-card">
          <p>Carregando...</p>
        </div>
      </div>
    )
  }

  return (
    <div className="clientes">
      <div className="page-header">
        <div>
          <h1>Cadastro de Clientes</h1>
          <p className="page-subtitle">Gerenciamento de clientes do sistema</p>
        </div>
        <button className="btn btn-primary" onClick={handleNew}>
          ➕ Novo Cliente
        </button>
      </div>

      <div className="clientes-card">
        <div className="search-container">
          <input
            type="text"
            placeholder="🔍 Buscar por nome, CNPJ, contato ou email..."
            className="search-input"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>ID</th>
                <th>Nome</th>
                <th>CNPJ</th>
                <th>Contato</th>
                <th>Email</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {filteredClientes.map(cliente => (
                <tr 
                  key={cliente.id}
                  className="cliente-row"
                  onClick={() => handleClienteClick(cliente.id)}
                >
                  <td>#{cliente.id}</td>
                  <td><strong>{cliente.nome}</strong></td>
                  <td>{cliente.cnpj}</td>
                  <td>{cliente.contato}</td>
                  <td>{cliente.email}</td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <div className="action-buttons">
                      <button 
                        className="btn-icon" 
                        title="Editar"
                        onClick={() => handleEdit(cliente)}
                      >
                        ✏️
                      </button>
                      <button 
                        className="btn-icon" 
                        title="Excluir"
                        onClick={(e) => handleDelete(cliente.id, e)}
                      >
                        🗑️
                      </button>
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
              <h2>{editingId === null ? 'Novo Cliente' : 'Editar Cliente'}</h2>
              <button className="modal-close" onClick={() => {
                setShowModal(false)
                setEditingId(null)
              }}>×</button>
            </div>
            <form onSubmit={handleSubmit} className="modal-form">
              <div className="form-group">
                <label>Nome da Empresa</label>
                <input 
                  type="text" 
                  required
                  value={formData.nome || ''}
                  onChange={(e) => setFormData({...formData, nome: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>CNPJ</label>
                <input 
                  type="text" 
                  required
                  value={formData.cnpj || ''}
                  onChange={(e) => setFormData({...formData, cnpj: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Contato</label>
                <input 
                  type="text" 
                  required
                  value={formData.contato || ''}
                  onChange={(e) => setFormData({...formData, contato: e.target.value})}
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

export default Clientes

