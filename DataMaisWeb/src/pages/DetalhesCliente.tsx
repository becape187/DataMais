import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../config/api'
import './DetalhesCliente.css'

interface Cliente {
  id: number
  nome: string
  cnpj: string
  contato: string
  email: string
}

interface Relatorio {
  id: number
  numero: string
  data: string
  cilindroId: number
  cilindroNome: string
}

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
  preCarga: number
  cargaNominal: number
  tempoRampaSubida: number
  tempoDuracaoCarga: number
  tempoRampaDescida: number
  percentualVariacaoAlarme: number
  histereseAlarme: number
  percentualVariacaoDesligaProcesso: number
}

const DetalhesCliente = () => {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [cliente, setCliente] = useState<Cliente | null>(null)
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  const [cilindros, setCilindros] = useState<Cilindro[]>([])

  useEffect(() => {
    loadCliente()
  }, [id])

  const loadCliente = async () => {
    try {
      const response = await api.get(`/cliente/${id}`)
      const data = response.data
      
      setCliente({
        id: data.id,
        nome: data.nome,
        cnpj: data.cnpj || '',
        contato: data.contato || '',
        email: data.email || ''
      })
      
      setRelatorios(Array.isArray(data.relatorios) ? data.relatorios : [])
      setCilindros(Array.isArray(data.cilindros) ? data.cilindros : [])
    } catch (error) {
      console.error('Erro ao carregar cliente:', error)
    }
  }

  if (!cliente) {
    return (
      <div className="detalhes-cliente">
        <p>Cliente não encontrado</p>
      </div>
    )
  }

  const handleCilindroClick = (cilindroId: number) => {
    navigate(`/clientes/${id}/cilindros/${cilindroId}`)
  }

  const handleAdicionarCilindro = () => {
    navigate(`/clientes/${id}/cilindros/novo`)
  }

  const handleRelatorioClick = (relatorioId: number) => {
    navigate(`/relatorios/${relatorioId}`)
  }

  return (
    <div className="detalhes-cliente">
      <div className="cliente-header">
        <button className="btn-back" onClick={() => navigate('/clientes')}>
          ← Voltar
        </button>
        <h1 className="cliente-nome">{cliente.nome}</h1>
        <div className="cliente-info">
          <span><strong>CNPJ:</strong> {cliente.cnpj}</span>
          <span><strong>Contato:</strong> {cliente.contato}</span>
          <span><strong>Email:</strong> {cliente.email}</span>
        </div>
      </div>

      <div className="sections-container">
        <section className="section-card">
          <div className="section-header">
            <h2>Relatórios</h2>
          </div>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Data</th>
                  <th>Cilindro</th>
                </tr>
              </thead>
              <tbody>
                {relatorios.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="empty-state">Nenhum relatório encontrado</td>
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
                      <td>{relatorio.cilindroNome}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>

        <section className="section-card">
          <div className="section-header">
            <h2>Cilindros Cadastrados</h2>
            <button className="btn btn-primary" onClick={handleAdicionarCilindro}>
              ➕ Adicionar Cilindro
            </button>
          </div>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Nome</th>
                  <th>Código Cliente</th>
                  <th>Código Interno</th>
                  <th>Modelo</th>
                  <th>Fabricante</th>
                </tr>
              </thead>
              <tbody>
                {cilindros.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="empty-state">Nenhum cilindro cadastrado</td>
                  </tr>
                ) : (
                  cilindros.map(cilindro => (
                    <tr 
                      key={cilindro.id}
                      className="clickable-row"
                      onClick={() => handleCilindroClick(cilindro.id)}
                    >
                      <td><strong>{cilindro.nome}</strong></td>
                      <td>{cilindro.codigoCliente}</td>
                      <td>{cilindro.codigoInterno}</td>
                      <td>{cilindro.modelo}</td>
                      <td>{cilindro.fabricante}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

export default DetalhesCliente

