import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import api from '../config/api'
import './Ensaio.css'

interface DataPoint {
  time: string
  pressaoA?: number | null
  pressaoB?: number | null
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
  const [ensaioDataInicio, setEnsaioDataInicio] = useState<Date | null>(null)
  const [dados, setDados] = useState<DataPoint[]>([])
  const [logEventos, setLogEventos] = useState<LogEvento[]>([])
  const [camara, setCamara] = useState<'A' | 'B' | ''>('')
  const [pressaoCarga, setPressaoCarga] = useState<string>('')
  const [tempoCarga, setTempoCarga] = useState<string>('')
  const [registroRodando, setRegistroRodando] = useState<boolean | null>(null)
  const [totalPontosColetados, setTotalPontosColetados] = useState(0)
  const [tempoAtual, setTempoAtual] = useState(Date.now())

  // Ao abrir a tela, retoma o ensaio em execução (backend é a fonte da verdade).
  // Idempotente: reentrar não cria outro ensaio, apenas reidrata o que está rodando,
  // recuperando o cronômetro, os dados e o botão de Encerrar.
  useEffect(() => {
    const retomarEnsaioAtivo = async () => {
      try {
        const { data } = await api.get('/ensaio/ativo')
        if (data && data.ativo) {
          setEnsaioId(data.id)
          setEnsaioAtivo(true)
          setEnsaioDataInicio(data.dataInicio ? new Date(data.dataInicio) : new Date())
          setTempoAtual(Date.now())
          if (data.camara) setCamara(data.camara)
          if (data.pressaoCarga != null) setPressaoCarga(String(data.pressaoCarga))
          if (data.tempoCarga != null) setTempoCarga(String(data.tempoCarga))
          setLogEventos([{
            id: Date.now(),
            texto: `[${new Date().toLocaleTimeString('pt-BR')}] Registro em andamento retomado (ID ${data.id})`,
            tipo: 'normal',
            comentarios: 0,
          }])

          // Reconstrói o gráfico com o histórico já coletado (InfluxDB), para sair/voltar não perder a curva
          try {
            const hist = await api.get(`/ensaio/${data.id}/historico`)
            const pontos: DataPoint[] = (hist.data?.dados ?? []).map((p: any) => ({
              time: p.time,
              pressaoA: p.pressaoA,
              pressaoB: p.pressaoB,
            }))
            setTotalPontosColetados(pontos.length)
            // Mantém só os últimos 100 no gráfico (mesma regra da coleta em tempo real)
            setDados(pontos.slice(-100))
          } catch (errHist) {
            console.error('Erro ao reconstruir histórico do ensaio:', errHist)
          }
        }
      } catch (err) {
        console.error('Erro ao retomar ensaio ativo:', err)
      }
    }

    retomarEnsaioAtivo()
  }, [])

  // Verifica REGISTRO_RODANDO ao carregar a página
  useEffect(() => {
    const verificarRegistroRodando = async () => {
      try {
        const response = await api.get('/ModbusConfig/registro/rodando')
        setRegistroRodando(response.data.rodando)
        
        if (response.data.rodando) {
          const evento: LogEvento = {
            id: Date.now(),
            texto: `[${new Date().toLocaleTimeString('pt-BR')}] Registro está rodando`,
            tipo: 'normal',
            comentarios: 0,
          }
          setLogEventos(prev => [evento, ...prev])
        }
      } catch (err: any) {
        console.error('Erro ao verificar REGISTRO_RODANDO:', err)
        setRegistroRodando(null)
      }
    }

    verificarRegistroRodando()
    
    // Verifica periodicamente a cada 5 segundos (quando não há ensaio ativo)
    if (!ensaioAtivo) {
      const interval = setInterval(verificarRegistroRodando, 5000)
      return () => clearInterval(interval)
    }
  }, [ensaioAtivo])

  // Refs para rastrear estado sem causar re-renders
  const registroAnteriorRef = useRef<boolean | null>(null)
  const dialogAbertoRef = useRef(false)

  // Verifica periodicamente REGISTRO_RODANDO quando ensaio está ativo
  // Detecta se o CLP encerrou o ensaio
  useEffect(() => {
    if (!ensaioAtivo || !ensaioId) {
      registroAnteriorRef.current = null
      dialogAbertoRef.current = false
      return
    }

    const verificarRegistroRodando = async () => {
      try {
        const response = await api.get('/ModbusConfig/registro/rodando')
        const rodandoAtual = response.data.rodando
        
        // Inicializa o estado anterior na primeira verificação
        if (registroAnteriorRef.current === null) {
          registroAnteriorRef.current = rodandoAtual
          setRegistroRodando(rodandoAtual)
          return
        }
        
        // Detecta se o registro parou de rodar (CLP concluiu o ensaio).
        // A conclusão e a geração do relatório são feitas AUTOMATICAMENTE pelo backend
        // (monitor de REGISTRO_RODANDO); aqui apenas refletimos na tela.
        if (registroAnteriorRef.current === true && rodandoAtual === false && !dialogAbertoRef.current) {
          dialogAbertoRef.current = true

          const evento: LogEvento = {
            id: Date.now(),
            texto: `[${new Date().toLocaleTimeString('pt-BR')}] CLP concluiu o ensaio — relatório gerado automaticamente`,
            tipo: 'normal',
            comentarios: 0,
          }
          setLogEventos(prev => [evento, ...prev])

          setEnsaioAtivo(false)
          setEnsaioDataInicio(null)
          setRegistroRodando(rodandoAtual)
          registroAnteriorRef.current = rodandoAtual
          dialogAbertoRef.current = false
        } else {
          setRegistroRodando(rodandoAtual)
          registroAnteriorRef.current = rodandoAtual
        }
      } catch (err: any) {
        console.error('Erro ao verificar REGISTRO_RODANDO:', err)
      }
    }

    // Verifica a cada 2 segundos quando o ensaio está ativo
    const interval = setInterval(verificarRegistroRodando, 2000)
    
    return () => clearInterval(interval)
  }, [ensaioAtivo, ensaioId])

  // Atualiza o tempo decorrido a cada segundo quando o ensaio está ativo
  useEffect(() => {
    if (!ensaioAtivo || !ensaioDataInicio) return

    const interval = setInterval(() => {
      setTempoAtual(Date.now()) // Força re-render para atualizar o tempo decorrido
    }, 1000)

    return () => clearInterval(interval)
  }, [ensaioAtivo, ensaioDataInicio])

  useEffect(() => {
    if (!ensaioAtivo || !ensaioId) return

    const abortController = new AbortController()
    let isMounted = true
    let requestInProgress = false

    const interval = setInterval(async () => {
      // Evita requisições simultâneas
      if (requestInProgress) {
        return
      }

      requestInProgress = true
      try {
        const response = await api.get(`/ensaio/${ensaioId}/pressao-atual`, {
          signal: abortController.signal
        })
        
        if (isMounted) {
          const ponto: DataPoint = {
            time: response.data.time,
            pressaoA: response.data.pressaoA,
            pressaoB: response.data.pressaoB,
          }

          setDados(prev => {
            const novos = [...prev, ponto]
            // Mantém apenas os últimos 100 pontos para o gráfico (performance)
            return novos.slice(-100)
          })
          
          // Incrementa contador total de pontos (sem limite)
          setTotalPontosColetados(prev => prev + 1)
        }
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && isMounted) {
          console.error('Erro ao ler pressão atual do ensaio:', err)
        }
      } finally {
        requestInProgress = false
      }
    }, 1000) // leitura a cada 1 segundo

    return () => {
      isMounted = false
      abortController.abort()
      clearInterval(interval)
    }
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
      const dataInicio = response.data.dataInicio 
        ? new Date(response.data.dataInicio) 
        : new Date()

      setEnsaioId(id)
      setEnsaioAtivo(true)
      setEnsaioDataInicio(dataInicio)
      setTempoAtual(Date.now())
      setDados([])
      setTotalPontosColetados(0)
      setLogEventos([])

      // Verifica avisos Modbus se houver
      if (response.data.avisosModbus && response.data.avisosModbus.length > 0) {
        const avisos = response.data.avisosModbus.join('\n')
        alert(`Ensaio iniciado, mas com avisos:\n${avisos}`)
      }

      // Atualiza status do registro rodando
      if (response.data.registroRodando !== undefined) {
        setRegistroRodando(response.data.registroRodando)
      }

      const evento: LogEvento = {
        id: Date.now(),
        texto: `[${new Date().toLocaleTimeString('pt-BR')}] Ensaio iniciado (ID ${id}) - Câmara ${camara}, Pressão de Carga ${pressaoVal} bar, Tempo de Carga ${tempoVal} min`,
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
      setEnsaioDataInicio(null)
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
              <label>Tempo de Carga (min)</label>
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
          <span className="stat-mini-label">Pressão A</span>
          <span className="stat-mini-value">
            {dados.length > 0 && dados[dados.length - 1].pressaoA != null 
              ? Math.round(dados[dados.length - 1].pressaoA!)
              : 0} bar
          </span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Pressão B</span>
          <span className="stat-mini-value">
            {dados.length > 0 && dados[dados.length - 1].pressaoB != null 
              ? Math.round(dados[dados.length - 1].pressaoB!)
              : 0} bar
          </span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Tempo Decorrido</span>
          <span className="stat-mini-value">
            {ensaioDataInicio 
              ? Math.floor((tempoAtual - ensaioDataInicio.getTime()) / 1000)
              : 0}s
          </span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Pontos Coletados</span>
          <span className="stat-mini-value">{totalPontosColetados}</span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Status</span>
          <span className={`stat-mini-value ${ensaioAtivo ? 'status-active' : 'status-inactive'}`}>
            {ensaioAtivo ? '● Ativo' : '○ Inativo'}
          </span>
        </div>
        <div className="stat-mini">
          <span className="stat-mini-label">Registro</span>
          <span className={`stat-mini-value ${registroRodando ? 'status-active' : registroRodando === false ? 'status-inactive' : ''}`}>
            {registroRodando === null ? '○ Verificando...' : registroRodando ? '● Rodando' : '○ Parado'}
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
                  formatter={(value: number | undefined, name: string | undefined) => {
                    const nameStr = name || 'Pressão'
                    if (value === undefined || value === null) return ['N/A', nameStr]
                    return [`${value.toFixed(2)} bar`, nameStr]
                  }}
                />
                <Legend />
                <Line 
                  type="monotone" 
                  dataKey="pressaoA" 
                  stroke="#dc3545" 
                  strokeWidth={3}
                  dot={false}
                  name="Pressão A (bar)"
                  animationDuration={300}
                />
                <Line 
                  type="monotone" 
                  dataKey="pressaoB" 
                  stroke="#007bff" 
                  strokeWidth={3}
                  dot={false}
                  name="Pressão B (bar)"
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

