import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../config/api'
import './ConfiguracaoCilindro.css'

interface Cilindro {
  id: number
  nome: string
  descricao: string
  codigoCliente: string
  codigoInterno: string
  modelo: string
  fabricante: string
  dataFabricacao: string
  diametroInterno: number
  comprimentoHaste: number
  diametroHaste: number
  maximaPressaoA: number
  maximaPressaoB: number
}

interface Relatorio {
  id: number
  numero: string
  data: string
}

const ConfiguracaoCilindro = () => {
  const { clienteId, cilindroId } = useParams<{ clienteId: string; cilindroId: string }>()
  const navigate = useNavigate()
  const isNew = cilindroId === 'novo'
  const [isEditing, setIsEditing] = useState(isNew)
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  
  const [formData, setFormData] = useState<Cilindro>({
    id: 0,
    nome: '',
    descricao: '',
    codigoCliente: '',
    codigoInterno: '',
    modelo: '',
    fabricante: '',
    dataFabricacao: '',
    diametroInterno: 0,
    comprimentoHaste: 0,
    diametroHaste: 0,
    maximaPressaoA: 0,
    maximaPressaoB: 0,
  })

  useEffect(() => {
    if (!isNew && cilindroId) {
      loadCilindro()
    }
  }, [cilindroId, isNew])

  const loadCilindro = async () => {
    try {
      const response = await api.get(`/cilindro/${cilindroId}`)
      const data = response.data
      
      setFormData({
        id: data.id,
        nome: data.nome || '',
        descricao: data.descricao || '',
        codigoCliente: data.codigoCliente || '',
        codigoInterno: data.codigoInterno || '',
        modelo: data.modelo || '',
        fabricante: data.fabricante || '',
        dataFabricacao: data.dataFabricacao || '',
        diametroInterno: Number(data.diametroInterno) || 0,
        comprimentoHaste: Number(data.comprimentoHaste) || 0,
        diametroHaste: Number(data.diametroHaste) || 0,
        maximaPressaoA: Number(data.maximaPressaoA) || 0,
        maximaPressaoB: Number(data.maximaPressaoB) || 0,
      })
      
      setRelatorios(data.relatorios || [])
    } catch (error) {
      console.error('Erro ao carregar cilindro:', error)
    }
  }

  const handleInputChange = (field: keyof Cilindro, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      // Validação básica dos campos obrigatórios
      if (!formData.nome || !formData.nome.trim()) {
        alert('O campo Nome é obrigatório')
        return
      }
      if (!formData.codigoCliente || !formData.codigoCliente.trim()) {
        alert('O campo Código Cliente é obrigatório')
        return
      }
      if (!formData.codigoInterno || !formData.codigoInterno.trim()) {
        alert('O campo Código Interno é obrigatório')
        return
      }
      if (!formData.maximaPressaoA || formData.maximaPressaoA <= 0) {
        alert('O campo Pressão Máxima A é obrigatório e deve ser maior que zero')
        return
      }
      if (!formData.maximaPressaoB || formData.maximaPressaoB <= 0) {
        alert('O campo Pressão Máxima B é obrigatório e deve ser maior que zero')
        return
      }
      if (!clienteId) {
        alert('Cliente não identificado')
        return
      }

      const clienteIdNum = Number(clienteId)
      if (isNaN(clienteIdNum) || clienteIdNum === 0) {
        alert('Erro: Cliente ID inválido. Por favor, recarregue a página e tente novamente.')
        return
      }

      // Preparar dados para envio
      // O backend está configurado para aceitar camelCase (ver Program.cs)
      const cilindroData: any = {
        nome: formData.nome.trim(),
        codigoCliente: formData.codigoCliente.trim(),
        codigoInterno: formData.codigoInterno.trim(),
        clienteId: clienteIdNum
      }
      
      console.log('Enviando dados do cilindro:', cilindroData) // Debug

      // Adicionar campos opcionais apenas se tiverem valor
      if (formData.descricao && formData.descricao.trim()) {
        cilindroData.descricao = formData.descricao.trim()
      }
      if (formData.modelo && formData.modelo.trim()) {
        cilindroData.modelo = formData.modelo.trim()
      }
      if (formData.fabricante && formData.fabricante.trim()) {
        cilindroData.fabricante = formData.fabricante.trim()
      }
      if (formData.dataFabricacao) {
        cilindroData.dataFabricacao = formData.dataFabricacao
      }

      // Campos obrigatórios de pressão
      cilindroData.maximaPressaoA = formData.maximaPressaoA
      cilindroData.maximaPressaoB = formData.maximaPressaoB

      // Adicionar campos numéricos opcionais apenas se tiverem valor diferente de 0 ou null
      const addNumericField = (field: string, value: number) => {
        if (value !== null && value !== undefined && value !== 0) {
          cilindroData[field] = value
        }
      }

      addNumericField('diametroInterno', formData.diametroInterno)
      addNumericField('comprimentoHaste', formData.comprimentoHaste)
      addNumericField('diametroHaste', formData.diametroHaste)
      
      if (isNew) {
        await api.post('/cilindro', cilindroData)
      } else {
        await api.put(`/cilindro/${cilindroId}`, cilindroData)
      }
      
      if (isNew) {
        navigate(`/clientes/${clienteId}`)
      } else {
        setIsEditing(false)
      }
    } catch (error: any) {
      console.error('Erro ao salvar cilindro:', error)
      console.error('Dados completos do erro:', JSON.stringify(error.response?.data, null, 2))
      
      // Extrair mensagens de erro detalhadas
      let errorMessage = 'Erro ao salvar cilindro'
      
      if (error.response?.data) {
        const errorData = error.response.data
        
        // Se houver erros de validação do ModelState
        if (errorData.errors) {
          const validationErrors: string[] = []
          Object.keys(errorData.errors).forEach(key => {
            const messages = errorData.errors[key]
            if (Array.isArray(messages)) {
              messages.forEach((msg: string) => {
                validationErrors.push(`${key}: ${msg}`)
              })
            } else {
              validationErrors.push(`${key}: ${messages}`)
            }
          })
          errorMessage = `Erros de validação:\n${validationErrors.join('\n')}`
          console.error('Erros de validação detalhados:', errorData.errors)
        } else if (errorData.message) {
          errorMessage = errorData.message
        } else if (errorData.title) {
          errorMessage = errorData.title
        }
      } else if (error.message) {
        errorMessage = error.message
      }
      
      alert(errorMessage)
    }
  }

  const handleCancel = () => {
    if (isNew) {
      navigate(`/clientes/${clienteId}`)
    } else {
      setIsEditing(false)
      loadCilindro() // Recarregar dados originais
    }
  }

  const handleRelatorioClick = (relatorioId: number) => {
    navigate(`/relatorios/${relatorioId}`)
  }

  return (
    <div className="configuracao-cilindro">
      <div className="page-header">
        <button className="btn-back" onClick={() => navigate(`/clientes/${clienteId}`)}>
          ← Voltar
        </button>
        <div>
          <h1>{isNew ? 'Novo Cilindro' : formData.nome || 'Cilindro'}</h1>
          {!isNew && !isEditing && (
            <button className="btn btn-primary" onClick={() => setIsEditing(true)}>
              ✏️ Editar
            </button>
          )}
        </div>
      </div>

      <form onSubmit={handleSubmit} className="form-container">
        <div className="form-sections">
          <div className="form-section-card">
            <h2 className="section-title">Informações Básicas</h2>
            <div className="form-grid">
              <div className="form-group">
                <label>Nome *</label>
                <input
                  type="text"
                  required
                  value={formData.nome}
                  onChange={(e) => handleInputChange('nome', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Descrição</label>
                <textarea
                  value={formData.descricao}
                  onChange={(e) => handleInputChange('descricao', e.target.value)}
                  disabled={!isEditing}
                  rows={3}
                />
              </div>
              <div className="form-group">
                <label>Código Cliente *</label>
                <input
                  type="text"
                  required
                  value={formData.codigoCliente}
                  onChange={(e) => handleInputChange('codigoCliente', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Código Interno *</label>
                <input
                  type="text"
                  required
                  value={formData.codigoInterno}
                  onChange={(e) => handleInputChange('codigoInterno', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Modelo</label>
                <input
                  type="text"
                  value={formData.modelo}
                  onChange={(e) => handleInputChange('modelo', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Fabricante</label>
                <input
                  type="text"
                  value={formData.fabricante}
                  onChange={(e) => handleInputChange('fabricante', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Data de Fabricação</label>
                <input
                  type="date"
                  value={formData.dataFabricacao}
                  onChange={(e) => handleInputChange('dataFabricacao', e.target.value)}
                  disabled={!isEditing}
                />
              </div>
            </div>
          </div>

          <div className="form-section-card">
            <h2 className="section-title">Dimensões</h2>
            <div className="form-grid">
              <div className="form-group">
                <label>Diâmetro Interno (mm)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.diametroInterno || ''}
                  onChange={(e) => handleInputChange('diametroInterno', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Comprimento da Haste (mm)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.comprimentoHaste || ''}
                  onChange={(e) => handleInputChange('comprimentoHaste', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Diâmetro da Haste (mm)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.diametroHaste || ''}
                  onChange={(e) => handleInputChange('diametroHaste', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
            </div>
          </div>

          <div className="form-section-card">
            <h2 className="section-title">Pressões Máximas</h2>
            <div className="form-grid">
              <div className="form-group">
                <label>Pressão Máxima A (bar) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  required
                  value={formData.maximaPressaoA || ''}
                  onChange={(e) => handleInputChange('maximaPressaoA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Pressão Máxima B (bar) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  required
                  value={formData.maximaPressaoB || ''}
                  onChange={(e) => handleInputChange('maximaPressaoB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
            </div>
          </div>

          {!isNew && (
            <div className="form-section-card">
              <h2 className="section-title">Relatórios do Cilindro</h2>
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Número</th>
                      <th>Data</th>
                    </tr>
                  </thead>
                  <tbody>
                    {relatorios.length === 0 ? (
                      <tr>
                        <td colSpan={2} className="empty-state">Nenhum relatório encontrado</td>
                      </tr>
                    ) : (
                      relatorios.map(relatorio => (
                        <tr 
                          key={relatorio.id}
                          className="clickable-row"
                          onClick={() => handleRelatorioClick(relatorio.id)}
                        >
                          <td><strong>{relatorio.numero}</strong></td>
                          <td>{new Date(relatorio.data).toLocaleDateString('pt-BR')}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

        {isEditing && (
          <div className="form-actions">
            <button type="button" className="btn btn-secondary" onClick={handleCancel}>
              Cancelar
            </button>
            <button type="submit" className="btn btn-primary">
              {isNew ? 'Salvar' : 'Atualizar'}
            </button>
          </div>
        )}
      </form>
    </div>
  )
}

export default ConfiguracaoCilindro

