import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
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
    // Simulação de dados - substituir por chamada à API
    const clientesMock: Cliente[] = [
      { id: 1, nome: 'MODEC Brasil', cnpj: '12.345.678/0001-90', contato: 'João Silva', email: 'joao.silva@modec.com' },
      { id: 2, nome: 'Petrobras', cnpj: '33.000.167/0001-01', contato: 'Maria Santos', email: 'maria.santos@petrobras.com.br' },
      { id: 3, nome: 'Equinor Brasil', cnpj: '11.222.333/0001-44', contato: 'Carlos Oliveira', email: 'carlos.oliveira@equinor.com' },
    ]

    const clienteEncontrado = clientesMock.find(c => c.id === Number(id))
    setCliente(clienteEncontrado || null)

    // Simulação de relatórios
    setRelatorios([
      { id: 1, numero: 'REL-2024-001', data: '2024-01-15', cilindroId: 1, cilindroNome: 'Cilindro Principal' },
      { id: 2, numero: 'REL-2024-002', data: '2024-02-20', cilindroId: 1, cilindroNome: 'Cilindro Principal' },
      { id: 3, numero: 'REL-2024-003', data: '2024-03-10', cilindroId: 2, cilindroNome: 'Cilindro Secundário' },
    ])

    // Simulação de cilindros
    setCilindros([
      {
        id: 1,
        nome: 'Cilindro Principal',
        descricao: 'Cilindro principal do sistema',
        codigoCliente: 'CIL-001',
        codigoInterno: 'INT-001',
        modelo: 'MOD-2024',
        fabricante: 'Fabricante A',
        dataFabricacao: '2023-01-15',
        diametroInterno: 100,
        comprimentoHaste: 500,
        diametroHaste: 50,
        maximaPressaoSuportadaA: 1000,
        maximaPressaoSuportadaB: 800,
        maximaPressaoSegurancaA: 900,
        maximaPressaoSegurancaB: 700,
        preCarga: 50,
        cargaNominal: 500,
        tempoRampaSubida: 30,
        tempoDuracaoCarga: 60,
        tempoRampaDescida: 30,
        percentualVariacaoAlarme: 10,
        histereseAlarme: 5,
        percentualVariacaoDesligaProcesso: 15,
      },
      {
        id: 2,
        nome: 'Cilindro Secundário',
        descricao: 'Cilindro secundário do sistema',
        codigoCliente: 'CIL-002',
        codigoInterno: 'INT-002',
        modelo: 'MOD-2024',
        fabricante: 'Fabricante B',
        dataFabricacao: '2023-02-20',
        diametroInterno: 80,
        comprimentoHaste: 400,
        diametroHaste: 40,
        maximaPressaoSuportadaA: 800,
        maximaPressaoSuportadaB: 600,
        maximaPressaoSegurancaA: 700,
        maximaPressaoSegurancaB: 500,
        preCarga: 40,
        cargaNominal: 400,
        tempoRampaSubida: 25,
        tempoDuracaoCarga: 50,
        tempoRampaDescida: 25,
        percentualVariacaoAlarme: 8,
        histereseAlarme: 4,
        percentualVariacaoDesligaProcesso: 12,
      },
    ])
  }, [id])

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

