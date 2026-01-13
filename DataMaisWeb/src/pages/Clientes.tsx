import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
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
  const [clientes] = useState<Cliente[]>([
    { id: 1, nome: 'MODEC Brasil', cnpj: '12.345.678/0001-90', contato: 'João Silva', email: 'joao.silva@modec.com' },
    { id: 2, nome: 'Petrobras', cnpj: '33.000.167/0001-01', contato: 'Maria Santos', email: 'maria.santos@petrobras.com.br' },
    { id: 3, nome: 'Equinor Brasil', cnpj: '11.222.333/0001-44', contato: 'Carlos Oliveira', email: 'carlos.oliveira@equinor.com' },
  ])
  const [searchTerm, setSearchTerm] = useState('')

  const [showModal, setShowModal] = useState(false)
  const [formData, setFormData] = useState<Partial<Cliente>>({})

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    // Aqui seria a lógica de salvar
    setShowModal(false)
    setFormData({})
  }

  const filteredClientes = clientes.filter(cliente =>
    cliente.nome.toLowerCase().includes(searchTerm.toLowerCase()) ||
    cliente.cnpj.includes(searchTerm) ||
    cliente.contato.toLowerCase().includes(searchTerm.toLowerCase()) ||
    cliente.email.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const handleClienteClick = (clienteId: number) => {
    navigate(`/clientes/${clienteId}`)
  }

  return (
    <div className="clientes">
      <div className="page-header">
        <div>
          <h1>Cadastro de Clientes</h1>
          <p className="page-subtitle">Gerenciamento de clientes do sistema</p>
        </div>
        <button className="btn btn-primary" onClick={() => setShowModal(true)}>
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
                      <button className="btn-icon" title="Editar">✏️</button>
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
              <h2>Novo Cliente</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
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

export default Clientes

