import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import api from '../config/api'
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
  const navigate = useNavigate()
  const [ensaioAtivo, setEnsaioAtivo] = useState(false)
  const [ensaioId, setEnsaioId] = useState<number | null>(null)
  const [dados, setDados] = useState<DataPoint[]>([])
  const [logEventos, setLogEventos] = useState<LogEvento[]>([])
  const [camara, setCamara] = useState<'A' | 'B' | ''>('')
  const [pressaoCarga, setPressaoCarga] = useState<string>('')
  const [tempoCarga, setTempoCarga] = useState<string>('')

  useEffect(() => {
    if (!ensaioAtivo || !ensaioId) return

    const interval = setInterval(async () => {
      try {
        const response = await api.get(`/ensaio/${ensaioId}/pressao-atual`)
        const ponto: DataPoint = {
          time: response.data.time,
          pressao: response.data.pressao,
        }

        setDados(prev => {
          const novos = [...prev, ponto]
          return novos.slice(-100) // Mantém apenas os últimos 100 pontos
        })
      } catch (err) {
        console.error('Erro ao ler pressão atual do ensaio:', err)
      }
    }, 1000) // leitura a cada 1 segundo

    return () => clearInterval(interval)
  }, [ensaioAtivo, ensaioId])

  const iniciarEnsaio = async () => {
    try {
      const pressaoVal = Number(pressaoCarga.replace(',', '.'))
      const tempoVal = Number(tempoCarga.replace(',', '.'))

      if (!camara) {
        alert('Selecione a câmara a ser testada (A ou B).')
        return
      }

      if (!pressaoCarga || isNaN(pressaoVal) || pressaoVal <= 0) {
        alert('Informe uma Pressão de Carga válida (maior que 0).')
        return
      }

      if (!tempoCarga || isNaN(tempoVal) || tempoVal <= 0) {
        alert('Informe um Tempo de Carga válido (maior que 0).')
        return
      }

      const response = await api.post('/ensaio/iniciar', {
        camara,
        pressaoCarga: pressaoVal,
        tempoCarga: tempoVal,
      })
      const id = response.data.id as number

      setEnsaioId(id)
      setEnsaioAtivo(true)
      setDados([])
      setLogEventos([])

      const evento: LogEvento = {
        id: Date.now(),
        texto: `[${new Date().toLocaleTimeString('pt-BR')}] Ensaio iniciado (ID ${id}) - Câmara ${camara}, Pressão de Carga ${pressaoVal} bar, Tempo de Carga ${tempoVal} s`,
        tipo: 'normal',
        comentarios: 0,
      }
      setLogEventos([evento])
    } catch (err: any) {
      console.error('Erro ao iniciar ensaio:', err)
      const msg = err?.response?.data?.message || 'Erro ao iniciar ensaio'
      alert(msg)
    }
  }

  const interromperEnsaio = async () => {
    if (!ensaioAtivo) return

    const salvar = window.confirm('Deseja salvar este ensaio?')

    try {
      if (ensaioId) {
        if (salvar) {
          await api.post(`/ensaio/interromper/${ensaioId}`)
        } else {
          await api.post(`/ensaio/cancelar/${ensaioId}`)
        }
      }
    } catch (err) {
      console.error('Erro ao interromper/cancelar ensaio:', err)
    } finally {
      setEnsaioAtivo(false)
      const evento: LogEvento = {
        id: Date.now(),
        texto: `[${new Date().toLocaleTimeString('pt-BR')}] Ensaio ${salvar ? 'salvo' : 'descartado (não salvo)'}`,
        tipo: 'normal',
        comentarios: 0,
      }
      setLogEventos(prev => [evento, ...prev])
    }
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
          <div className="ensaio-config">
            <div className="config-field">
              <label>Câmara</label>
              <select
                value={camara}
                onChange={(e) => setCamara(e.target.value as 'A' | 'B' | '')}
                disabled={ensaioAtivo}
              >
                <option value="">Selecione...</option>
                <option value="A">Câmara A (Avança)</option>
                <option value="B">Câmara B (Recua)</option>
              </select>
            </div>
            <div className="config-field">
              <label>Pressão de Carga (bar)</label>
              <input
                type="number"
                step="0.01"
                value={pressaoCarga}
                onChange={(e) => setPressaoCarga(e.target.value)}
                disabled={ensaioAtivo}
              />
            </div>
            <div className="config-field">
              <label>Tempo de Carga (s)</label>
              <input
                type="number"
                step="0.01"
                value={tempoCarga}
                onChange={(e) => setTempoCarga(e.target.value)}
                disabled={ensaioAtivo}
              />
            </div>
          </div>
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

