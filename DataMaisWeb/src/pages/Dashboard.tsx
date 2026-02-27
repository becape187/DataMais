import { useState, useEffect } from 'react'
import api from '../config/api'
import './Dashboard.css'

interface ModbusRegistro {
  id: number
  nome: string
  ativo: boolean
}

interface Sensor {
  id: number
  nome: string
  tipo: string
  scale?: number
  modbusConfigId?: number
  ativo: boolean
}

interface Relatorio {
  id: number
  numero: string
  data: string
  clienteId: number
  clienteNome?: string
  cilindroId: number
  cilindroNome?: string
  ensaioId?: number
}

interface Cliente {
  id: number
  nome: string
  relatorios?: Relatorio[]
}

const Dashboard = () => {
  // Estados para dados reais
  const [motorStatus, setMotorStatus] = useState(false)
  const [pressaoA, setPressaoA] = useState<number | null>(null)
  const [pressaoB, setPressaoB] = useState<number | null>(null)
  const [pressaoGeral, setPressaoGeral] = useState<number | null>(null)
  const [sensoresAtivos, setSensoresAtivos] = useState(0)
  const [totalSensores, setTotalSensores] = useState(0)
  const [relatorios, setRelatorios] = useState<Relatorio[]>([])
  const [loading, setLoading] = useState(true)
  const [modbusConectado, setModbusConectado] = useState(false)

  // Estados dos registros Modbus
  const [registros, setRegistros] = useState<{
    statusMotor?: ModbusRegistro
    pressaoA?: ModbusRegistro
    pressaoB?: ModbusRegistro
  }>({})

  // Estados dos sensores
  const [sensores, setSensores] = useState<{
    sensorA?: Sensor
    sensorB?: Sensor
    pressaoGeral?: Sensor
  }>({})

  // Busca os registros Modbus e sensores
  useEffect(() => {
    const buscarDados = async () => {
      try {
        setLoading(true)

        // Busca registros Modbus
        const responseModbus = await api.get('/ModbusConfig')
        const todosRegistros: ModbusRegistro[] = responseModbus.data

        const registrosEncontrados: typeof registros = {}
        todosRegistros.forEach(reg => {
          if (!reg.ativo) return

          switch (reg.nome) {
            case 'MOTOR_BOMBA':
              registrosEncontrados.statusMotor = reg
              break
            case 'PRESSAO_A':
              registrosEncontrados.pressaoA = reg
              break
            case 'PRESSAO_B':
              registrosEncontrados.pressaoB = reg
              break
          }
        })

        setRegistros(registrosEncontrados)

        // Verifica se há registros Modbus ativos (indica conexão)
        const registrosAtivos = todosRegistros.filter(r => r.ativo)
        setModbusConectado(registrosAtivos.length > 0)

        // Busca sensores
        try {
          const responseSensores = await api.get('/Sensor')
          const todosSensores: Sensor[] = responseSensores.data

          const sensoresAtivosCount = todosSensores.filter(s => s.ativo).length
          setTotalSensores(todosSensores.length)
          setSensoresAtivos(sensoresAtivosCount)

          const sensoresEncontrados: typeof sensores = {}
          todosSensores.forEach(sensor => {
            if (!sensor.ativo) return

            const nomeUpper = sensor.nome.toUpperCase()
            if (nomeUpper.includes('A') && nomeUpper.includes('PRESSÃO')) {
              sensoresEncontrados.sensorA = sensor
            } else if (nomeUpper.includes('B') && nomeUpper.includes('PRESSÃO')) {
              sensoresEncontrados.sensorB = sensor
            } else if (nomeUpper.includes('GERAL') || nomeUpper.includes('PRESSÃO GERAL')) {
              sensoresEncontrados.pressaoGeral = sensor
            }
          })

          // Se não encontrou por nome, tenta buscar por tipo
          if (!sensoresEncontrados.sensorA || !sensoresEncontrados.sensorB) {
            const sensoresPressao = todosSensores.filter(s => 
              s.ativo && s.tipo.toLowerCase().includes('pressão')
            )
            
            if (sensoresPressao.length >= 1 && !sensoresEncontrados.sensorA) {
              sensoresEncontrados.sensorA = sensoresPressao[0]
            }
            if (sensoresPressao.length >= 2 && !sensoresEncontrados.sensorB) {
              sensoresEncontrados.sensorB = sensoresPressao[1]
            }
            if (sensoresPressao.length >= 3 && !sensoresEncontrados.pressaoGeral) {
              sensoresEncontrados.pressaoGeral = sensoresPressao[2]
            }
          }

          setSensores(sensoresEncontrados)
        } catch (err) {
          console.warn('Erro ao buscar sensores:', err)
        }

        // Busca relatórios através dos clientes
        try {
          const responseClientes = await api.get('/Cliente')
          const clientes: Cliente[] = responseClientes.data

          const todosRelatorios: Relatorio[] = []
          for (const cliente of clientes) {
            try {
              const responseCliente = await api.get(`/Cliente/${cliente.id}`)
              const clienteCompleto = responseCliente.data
              
              if (clienteCompleto.relatorios && Array.isArray(clienteCompleto.relatorios)) {
                clienteCompleto.relatorios.forEach((rel: any) => {
                  todosRelatorios.push({
                    id: rel.id,
                    numero: rel.numero || `REL-${rel.id}`,
                    data: rel.data,
                    clienteId: cliente.id,
                    clienteNome: cliente.nome,
                    cilindroId: rel.cilindroId,
                    cilindroNome: rel.cilindroNome
                  })
                })
              }
            } catch (err) {
              console.warn(`Erro ao buscar relatórios do cliente ${cliente.id}:`, err)
            }
          }

          // Ordena por data (mais recentes primeiro) e pega os últimos 3
          todosRelatorios.sort((a, b) => {
            const dataA = new Date(a.data).getTime()
            const dataB = new Date(b.data).getTime()
            return dataB - dataA
          })

          setRelatorios(todosRelatorios.slice(0, 3))
        } catch (err) {
          console.warn('Erro ao buscar relatórios:', err)
        }

      } catch (err: any) {
        console.error('Erro ao buscar dados do dashboard:', err)
      } finally {
        setLoading(false)
      }
    }

    buscarDados()
  }, [])

  // Atualiza status do motor e pressões periodicamente
  useEffect(() => {
    if (!registros.statusMotor && !registros.pressaoA && !registros.pressaoB) return

    const atualizarStatus = async () => {
      try {
        // Lê status do motor
        if (registros.statusMotor) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.statusMotor.id}/read`)
            const valor = response.data.valor
            setMotorStatus(valor === true || valor === 1 || valor === '1')
          } catch (err) {
            console.error('Erro ao ler status do motor:', err)
          }
        }

        // Lê pressão A e aplica escala do sensor
        if (registros.pressaoA) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoA.id}/read`)
            let valor = Number(response.data.valor)
            
            if (sensores.sensorA?.scale) {
              valor = valor * sensores.sensorA.scale
            }
            
            setPressaoA(valor)
          } catch (err) {
            console.error('Erro ao ler pressão A:', err)
            setPressaoA(null)
          }
        }

        // Lê pressão B e aplica escala do sensor
        if (registros.pressaoB) {
          try {
            const response = await api.get(`/ModbusConfig/${registros.pressaoB.id}/read`)
            let valor = Number(response.data.valor)
            
            if (sensores.sensorB?.scale) {
              valor = valor * sensores.sensorB.scale
            }
            
            setPressaoB(valor)
          } catch (err) {
            console.error('Erro ao ler pressão B:', err)
            setPressaoB(null)
          }
        }

        // Lê pressão geral se o sensor estiver configurado
        if (sensores.pressaoGeral?.modbusConfigId) {
          try {
            const response = await api.get(`/ModbusConfig/${sensores.pressaoGeral.modbusConfigId}/read`)
            let valor = Number(response.data.valor)
            
            if (sensores.pressaoGeral.scale) {
              valor = valor * sensores.pressaoGeral.scale
            }
            
            setPressaoGeral(valor)
          } catch (err) {
            console.error('Erro ao ler pressão geral:', err)
            setPressaoGeral(null)
          }
        }
      } catch (err) {
        console.error('Erro ao atualizar status:', err)
      }
    }

    atualizarStatus()
    const interval = setInterval(atualizarStatus, 2000) // Atualiza a cada 2 segundos

    return () => clearInterval(interval)
  }, [
    registros.statusMotor?.id, 
    registros.pressaoA?.id, 
    registros.pressaoB?.id,
    sensores.sensorA?.id,
    sensores.sensorB?.id,
    sensores.pressaoGeral?.id
  ])

  // Calcula pressão atual (média entre A e B, ou a maior, ou pressão geral)
  const pressaoAtual = pressaoGeral !== null 
    ? pressaoGeral 
    : (pressaoA !== null && pressaoB !== null) 
      ? (pressaoA + pressaoB) / 2 
      : pressaoA !== null 
        ? pressaoA 
        : pressaoB

  // Formata data para exibição
  const formatarData = (data: string) => {
    try {
      const date = new Date(data)
      return date.toLocaleString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      })
    } catch {
      return data
    }
  }

  return (
    <div className="dashboard">
      <div className="page-header">
        <h1>Dashboard</h1>
        <p className="page-subtitle">Visão geral do sistema</p>
      </div>

      {loading && (
        <div className="loading-message">
          Carregando dados do dashboard...
        </div>
      )}

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon stat-icon-motor">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2"/>
              <path d="M12 6V12L16 14" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Status do Motor</h3>
            <p className="stat-value">{motorStatus ? 'Ligado' : 'Desligado'}</p>
            <span className={`stat-badge ${motorStatus ? 'success' : ''}`}>
              {motorStatus ? 'Ativo' : 'Inativo'}
            </span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-ensaio">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="3" y="3" width="18" height="18" rx="2" stroke="currentColor" strokeWidth="2"/>
              <path d="M3 9H21M9 3V21" stroke="currentColor" strokeWidth="2"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Último Relatório</h3>
            <p className="stat-value">
              {relatorios.length > 0 ? relatorios[0].numero : 'N/A'}
            </p>
            <span className={`stat-badge ${relatorios.length > 0 ? 'success' : ''}`}>
              {relatorios.length > 0 ? 'Disponível' : 'Sem dados'}
            </span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-sensor">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="5" y="2" width="14" height="20" rx="2" stroke="currentColor" strokeWidth="2"/>
              <circle cx="12" cy="12" r="2" fill="currentColor"/>
              <path d="M12 6V8M12 16V18" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Sensores Ativos</h3>
            <p className="stat-value">
              {totalSensores > 0 ? `${sensoresAtivos}/${totalSensores}` : 'N/A'}
            </p>
            <span className={`stat-badge ${sensoresAtivos === totalSensores && totalSensores > 0 ? 'success' : ''}`}>
              {totalSensores > 0 
                ? `${Math.round((sensoresAtivos / totalSensores) * 100)}%` 
                : 'Sem dados'}
            </span>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-pressao">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M3 12L7 8L11 12L15 8L21 12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M3 20L7 16L11 20L15 16L21 20" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
          </div>
          <div className="stat-content">
            <h3>Pressão Atual</h3>
            <p className="stat-value">
              {pressaoAtual !== null ? `${pressaoAtual.toFixed(1)} bar` : 'N/A'}
            </p>
            <span className="stat-badge info">Normal</span>
          </div>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="dashboard-card">
          <h2>Últimos Relatórios</h2>
          <div className="table-container">
            {relatorios.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Número</th>
                    <th>Cliente</th>
                    <th>Data</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {relatorios.map(relatorio => (
                    <tr key={relatorio.id}>
                      <td>{relatorio.numero}</td>
                      <td>{relatorio.clienteNome || 'N/A'}</td>
                      <td>{formatarData(relatorio.data)}</td>
                      <td><span className="badge success">Concluído</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p style={{ padding: '20px', textAlign: 'center', color: '#666' }}>
                Nenhum relatório encontrado
              </p>
            )}
          </div>
        </div>

        <div className="dashboard-card">
          <h2>Status do Sistema</h2>
          <div className="status-list">
            <div className="status-item">
              <span className={`status-indicator ${motorStatus ? 'success' : ''}`}></span>
              <div>
                <strong>Motor Hidráulico</strong>
                <p>{motorStatus ? 'Operacional' : 'Desligado'}</p>
              </div>
            </div>
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>Cilindro</strong>
                <p>
                  {pressaoA !== null || pressaoB !== null 
                    ? `Pressão: ${pressaoAtual !== null ? pressaoAtual.toFixed(1) + ' bar' : 'N/A'}` 
                    : 'Sem dados'}
                </p>
              </div>
            </div>
            <div className="status-item">
              <span className={`status-indicator ${modbusConectado ? 'success' : ''}`}></span>
              <div>
                <strong>Comunicação Modbus</strong>
                <p>{modbusConectado ? 'Conectado' : 'Desconectado'}</p>
              </div>
            </div>
            <div className="status-item">
              <span className="status-indicator success"></span>
              <div>
                <strong>Sensores</strong>
                <p>
                  {totalSensores > 0 
                    ? `${sensoresAtivos} de ${totalSensores} ativos` 
                    : 'Sem sensores configurados'}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default Dashboard

