import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import './Ensaio.css'

interface DataPoint {
  time: string
  pressao: number
}

interface LogEvento {
  id: number
  texto: string
  tipo: 'normal' | 'desvio'
  comentarios: number
}

const Ensaio = () => {
  // Dados fictícios iniciais para demonstração
  const gerarDadosFicticios = (): DataPoint[] => {
    const dados: DataPoint[] = []
    const agora = new Date()
    
    for (let i = 0; i < 60; i++) {
      const tempo = new Date(agora.getTime() - (60 - i) * 500)
      // Simula uma curva de pressão realista
      const pressao = Math.sin((i / 60) * Math.PI * 2) * 100 + 150 + Math.random() * 20
      dados.push({
        time: tempo.toLocaleTimeString('pt-BR'),
        pressao: Math.max(0, Math.min(350, pressao))
      })
    }
    return dados
  }

  const navigate = useNavigate()
  const [ensaioAtivo, setEnsaioAtivo] = useState(false)
  const [dados, setDados] = useState<DataPoint[]>(gerarDadosFicticios())
  const [logEventos, setLogEventos] = useState<LogEvento[]>([
    { id: 1, texto: '[14:25:30] Ensaio iniciado', tipo: 'normal', comentarios: 0 },
    { id: 2, texto: '[14:25:45] Pressão estabilizada em 120 bar', tipo: 'normal', comentarios: 0 },
    { id: 3, texto: '[14:26:10] Desvio detectado: Pressão 245.8 bar', tipo: 'desvio', comentarios: 2 },
    { id: 4, texto: '[14:26:30] Pressão normalizada', tipo: 'normal', comentarios: 0 },
    { id: 5, texto: '[14:27:00] Ciclo de pressão completo', tipo: 'normal', comentarios: 0 },
    { id: 6, texto: '[14:27:15] Pressão em 180 bar - dentro dos limites', tipo: 'normal', comentarios: 0 },
    { id: 7, texto: '[14:27:45] Desvio detectado: Pressão 298.2 bar', tipo: 'desvio', comentarios: 0 },
    { id: 8, texto: '[14:28:00] Pressão retornando ao normal', tipo: 'normal', comentarios: 0 }
  ])

  useEffect(() => {
    if (!ensaioAtivo) return

    const interval = setInterval(() => {
      const novoPonto: DataPoint = {
        time: new Date().toLocaleTimeString('pt-BR'),
        pressao: Math.random() * 350
      }
      
      setDados(prev => {
        const novos = [...prev, novoPonto]
        return novos.slice(-100) // Mantém apenas os últimos 100 pontos
      })

      // Simula eventos de desvio
      if (Math.random() > 0.95) {
        const evento: LogEvento = {
          id: Date.now(),
          texto: `[${new Date().toLocaleTimeString('pt-BR')}] Desvio detectado: Pressão ${novoPonto.pressao.toFixed(2)} bar`,
          tipo: 'desvio',
          comentarios: 0
        }
        setLogEventos(prev => [evento, ...prev].slice(0, 50))
      }
    }, 500)

    return () => clearInterval(interval)
  }, [ensaioAtivo])

  const iniciarEnsaio = () => {
    setEnsaioAtivo(true)
    setDados([])
    setLogEventos([])
    const evento: LogEvento = {
      id: Date.now(),
      texto: `[${new Date().toLocaleTimeString('pt-BR')}] Ensaio iniciado`,
      tipo: 'normal',
      comentarios: 0
    }
    setLogEventos([evento])
  }

  const interromperEnsaio = () => {
    setEnsaioAtivo(false)
    const evento: LogEvento = {
      id: Date.now(),
      texto: `[${new Date().toLocaleTimeString('pt-BR')}] Ensaio interrompido`,
      tipo: 'normal',
      comentarios: 0
    }
    setLogEventos(prev => [evento, ...prev])
  }

  const abrirComentarios = (eventoId: number) => {
    navigate(`/ensaio/comentarios/${eventoId}`)
  }

  return (
    <div className="ensaio">
      <div className="page-header">
        <div>
          <h1>Ensaio em Tempo Real</h1>
          <p className="page-subtitle">Monitoramento da curva de pressão</p>
        </div>
        <div className="ensaio-controls">
          <button 
            className={`btn ${ensaioAtivo ? 'btn-danger' : 'btn-primary'}`}
            onClick={ensaioAtivo ? interromperEnsaio : iniciarEnsaio}
          >
            {ensaioAtivo ? '⏹️ Interromper Ensaio' : '▶️ Iniciar Ensaio'}
          </button>
        </div>
      </div>

      <div className="ensaio-stats">
        <div className="stat-mini">
          <span className="stat-mini-label">Pressão Atual</span>
          <span className="stat-mini-value">
            {dados.length > 0 ? dados[dados.length - 1].pressao.toFixed(2) : '0.00'} bar
          </span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Tempo Decorrido</span>
          <span className="stat-mini-value">{Math.floor(dados.length * 0.5)}s</span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Pontos Coletados</span>
          <span className="stat-mini-value">{dados.length}</span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Status</span>
          <span className={`stat-mini-value ${ensaioAtivo ? 'status-active' : 'status-inactive'}`}>
            {ensaioAtivo ? '● Ativo' : '○ Inativo'}
          </span>
        </div>
      </div>

      <div className="ensaio-main-content">
        <div className="grafico-container">
          <div className="grafico-card">
            <h2>Curva de Pressão em Tempo Real</h2>
            <ResponsiveContainer width="100%" height={280}>
              <LineChart data={dados}>
                <CartesianGrid strokeDasharray="3 3" stroke="#E0E0E0" />
                <XAxis 
                  dataKey="time" 
                  stroke="#666"
                  tick={{ fill: '#666', fontSize: 11 }}
                />
                <YAxis 
                  stroke="#666"
                  tick={{ fill: '#666', fontSize: 11 }}
                  label={{ value: 'Pressão (bar)', angle: -90, position: 'insideLeft', style: { fontSize: 12 } }}
                  domain={[0, 350]}
                />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'rgba(255, 255, 255, 0.95)',
                    border: '1px solid #E0E0E0',
                    borderRadius: '8px'
                  }}
                />
                <Legend />
                <Line 
                  type="monotone" 
                  dataKey="pressao" 
                  stroke="var(--modec-red)" 
                  strokeWidth={3}
                  dot={false}
                  name="Pressão (bar)"
                  animationDuration={300}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="log-container">
          <div className="log-card">
            <h2>Log de Eventos e Desvios</h2>
            <div className="log-content">
              {logEventos.length === 0 ? (
                <div className="log-empty">Nenhum evento registrado</div>
              ) : (
                logEventos.map((evento) => (
                  <div key={evento.id} className={`log-entry ${evento.tipo === 'desvio' ? 'log-desvio' : ''}`}>
                    <span className="log-texto">{evento.texto}</span>
                    {evento.tipo === 'desvio' && (
                      <button 
                        className="log-comentario-btn"
                        onClick={() => abrirComentarios(evento.id)}
                        title="Adicionar comentário sobre o desvio"
                      >
                        {evento.comentarios > 0 ? (
                          <>
                            <span className="comentario-icon">💬</span>
                            <span className="comentario-badge">{evento.comentarios}</span>
                          </>
                        ) : (
                          <span className="comentario-icon-empty">💬</span>
                        )}
                      </button>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default Ensaio

