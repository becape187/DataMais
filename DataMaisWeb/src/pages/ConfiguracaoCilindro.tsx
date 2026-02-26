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
  maximaPressaoSuportadaA: number
  maximaPressaoSuportadaB: number
  maximaPressaoSegurancaA: number
  maximaPressaoSegurancaB: number
  // Parâmetros Câmara A
  preCargaA: number
  cargaNominalA: number
  tempoRampaSubidaA: number
  tempoDuracaoCargaA: number
  tempoRampaDescidaA: number
  percentualVariacaoAlarmeA: number
  histereseAlarmeA: number
  percentualVariacaoDesligaProcessoA: number
  // Parâmetros Câmara B
  preCargaB: number
  cargaNominalB: number
  tempoRampaSubidaB: number
  tempoDuracaoCargaB: number
  tempoRampaDescidaB: number
  percentualVariacaoAlarmeB: number
  histereseAlarmeB: number
  percentualVariacaoDesligaProcessoB: number
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
    maximaPressaoSuportadaA: 0,
    maximaPressaoSuportadaB: 0,
    maximaPressaoSegurancaA: 0,
    maximaPressaoSegurancaB: 0,
    preCargaA: 0,
    cargaNominalA: 0,
    tempoRampaSubidaA: 0,
    tempoDuracaoCargaA: 0,
    tempoRampaDescidaA: 0,
    percentualVariacaoAlarmeA: 0,
    histereseAlarmeA: 0,
    percentualVariacaoDesligaProcessoA: 0,
    preCargaB: 0,
    cargaNominalB: 0,
    tempoRampaSubidaB: 0,
    tempoDuracaoCargaB: 0,
    tempoRampaDescidaB: 0,
    percentualVariacaoAlarmeB: 0,
    histereseAlarmeB: 0,
    percentualVariacaoDesligaProcessoB: 0,
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
        maximaPressaoSuportadaA: Number(data.maximaPressaoSuportadaA) || 0,
        maximaPressaoSuportadaB: Number(data.maximaPressaoSuportadaB) || 0,
        maximaPressaoSegurancaA: Number(data.maximaPressaoSegurancaA) || 0,
        maximaPressaoSegurancaB: Number(data.maximaPressaoSegurancaB) || 0,
        preCargaA: Number(data.preCargaA) || 0,
        cargaNominalA: Number(data.cargaNominalA) || 0,
        tempoRampaSubidaA: Number(data.tempoRampaSubidaA) || 0,
        tempoDuracaoCargaA: Number(data.tempoDuracaoCargaA) || 0,
        tempoRampaDescidaA: Number(data.tempoRampaDescidaA) || 0,
        percentualVariacaoAlarmeA: Number(data.percentualVariacaoAlarmeA) || 0,
        histereseAlarmeA: Number(data.histereseAlarmeA) || 0,
        percentualVariacaoDesligaProcessoA: Number(data.percentualVariacaoDesligaProcessoA) || 0,
        preCargaB: Number(data.preCargaB) || 0,
        cargaNominalB: Number(data.cargaNominalB) || 0,
        tempoRampaSubidaB: Number(data.tempoRampaSubidaB) || 0,
        tempoDuracaoCargaB: Number(data.tempoDuracaoCargaB) || 0,
        tempoRampaDescidaB: Number(data.tempoRampaDescidaB) || 0,
        percentualVariacaoAlarmeB: Number(data.percentualVariacaoAlarmeB) || 0,
        histereseAlarmeB: Number(data.histereseAlarmeB) || 0,
        percentualVariacaoDesligaProcessoB: Number(data.percentualVariacaoDesligaProcessoB) || 0,
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
      const cilindroData = {
        ...formData,
        clienteId: Number(clienteId)
      }
      
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
    } catch (error) {
      console.error('Erro ao salvar cilindro:', error)
      alert('Erro ao salvar cilindro')
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
                <label>Máxima Pressão Suportada A - Maior Área (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.maximaPressaoSuportadaA || ''}
                  onChange={(e) => handleInputChange('maximaPressaoSuportadaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Máxima Pressão Suportada B (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.maximaPressaoSuportadaB || ''}
                  onChange={(e) => handleInputChange('maximaPressaoSuportadaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Máxima Pressão Segurança A (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.maximaPressaoSegurancaA || ''}
                  onChange={(e) => handleInputChange('maximaPressaoSegurancaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Máxima Pressão Segurança B - Menor Área (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.maximaPressaoSegurancaB || ''}
                  onChange={(e) => handleInputChange('maximaPressaoSegurancaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
            </div>
          </div>

          <div className="form-section-card">
            <h2 className="section-title">Parâmetros de Ensaio - Câmara A</h2>
            <div className="form-grid">
              <div className="form-group">
                <label>Pré-Carga (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.preCargaA || ''}
                  onChange={(e) => handleInputChange('preCargaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Carga Nominal (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.cargaNominalA || ''}
                  onChange={(e) => handleInputChange('cargaNominalA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Rampa Subida (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoRampaSubidaA || ''}
                  onChange={(e) => handleInputChange('tempoRampaSubidaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Duração Carga (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoDuracaoCargaA || ''}
                  onChange={(e) => handleInputChange('tempoDuracaoCargaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Rampa Descida (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoRampaDescidaA || ''}
                  onChange={(e) => handleInputChange('tempoRampaDescidaA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Percentual de Variação Alarme (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.percentualVariacaoAlarmeA || ''}
                  onChange={(e) => handleInputChange('percentualVariacaoAlarmeA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Histerese do Alarme (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.histereseAlarmeA || ''}
                  onChange={(e) => handleInputChange('histereseAlarmeA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Percentual de Variação Desliga Processo (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.percentualVariacaoDesligaProcessoA || ''}
                  onChange={(e) => handleInputChange('percentualVariacaoDesligaProcessoA', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
            </div>
          </div>

          <div className="form-section-card">
            <h2 className="section-title">Parâmetros de Ensaio - Câmara B</h2>
            <div className="form-grid">
              <div className="form-group">
                <label>Pré-Carga (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.preCargaB || ''}
                  onChange={(e) => handleInputChange('preCargaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Carga Nominal (bar)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.cargaNominalB || ''}
                  onChange={(e) => handleInputChange('cargaNominalB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Rampa Subida (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoRampaSubidaB || ''}
                  onChange={(e) => handleInputChange('tempoRampaSubidaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Duração Carga (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoDuracaoCargaB || ''}
                  onChange={(e) => handleInputChange('tempoDuracaoCargaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Tempo Rampa Descida (s)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.tempoRampaDescidaB || ''}
                  onChange={(e) => handleInputChange('tempoRampaDescidaB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Percentual de Variação Alarme (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.percentualVariacaoAlarmeB || ''}
                  onChange={(e) => handleInputChange('percentualVariacaoAlarmeB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Histerese do Alarme (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.histereseAlarmeB || ''}
                  onChange={(e) => handleInputChange('histereseAlarmeB', parseFloat(e.target.value) || 0)}
                  disabled={!isEditing}
                />
              </div>
              <div className="form-group">
                <label>Percentual de Variação Desliga Processo (%)</label>
                <input
                  type="number"
                  step="0.01"
                  value={formData.percentualVariacaoDesligaProcessoB || ''}
                  onChange={(e) => handleInputChange('percentualVariacaoDesligaProcessoB', parseFloat(e.target.value) || 0)}
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

