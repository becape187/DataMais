import { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import api from '../config/api'
import { useAuth } from '../contexts/AuthContext'
import { formatarDuracao } from '../utils/tempo'
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

type Camara = 'A' | 'B'

interface Etapa {
  id: number
  camara: Camara
  tentativa: number
  status: 'EmExecucao' | 'Concluida' | 'Descartada' | 'Repetida'
  dataInicio: string
  dataFim: string | null
  /** Quando o CLP ligou o INICIA_CONTAGEM — t0 real do teste. Null enquanto na rampa. */
  dataInicioContagem: string | null
  dataFimContagem: string | null
  pressaoCargaConfigurada: number
  tempoCargaConfigurado: number
}

interface EnsaioAberto {
  id: number
  numero: string
  status: 'EmAndamento' | 'AguardandoAceite' | 'Aceito' | 'Cancelado'
  dataCriacao: string
  vessel: string | null
  localTeste: string | null
  departamento: string | null
  ordemServico: string | null
  clienteNome: string | null
  cilindroNome: string | null
  etapas: Etapa[]
  etapaEmExecucaoId: number | null
  podeAceitar: boolean
}

interface OpcaoCliente {
  id: number
  nome: string
}

interface OpcaoCilindro {
  id: number
  nome: string
  codigoCliente?: string | null
}

/** Estado das pressões da bancada com o ensaio parado — quem libera o início. */
interface PressoesBancada {
  pressaoA: number | null
  pressaoB: number | null
  limite: number
  podeIniciar: boolean
  impedimento: string | null
}

/** Recusa do backend em iniciar por cilindro ainda pressurizado. */
interface BloqueioPressao {
  mensagem: string
  pressaoA: number | null
  pressaoB: number | null
  limite: number
}

const CAMARAS: Camara[] = ['A', 'B']

const formatarBar = (valor: number | null | undefined) =>
  valor == null ? '—' : `${valor.toFixed(2)} bar`

/**
 * Como o ensaio se apresenta na tela. O `numero` do ensaio (ENSAIO-20260731-134721) é
 * só um identificador interno do cabeçalho — o número oficial é o REH-MPR, e ele só é
 * queimado no aceite, para ensaio descartado não consumir sequencial. Mostrar o código
 * cru fazia parecer que o padrão de numeração estava errado.
 */
const rotuloEnsaio = (ensaio: EnsaioAberto) =>
  `Ensaio de ${new Date(ensaio.dataCriacao).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })}`

/** Etapa que vale para o laudo: última tentativa concluída da câmara. */
const etapaValida = (ensaio: EnsaioAberto | null, camara: Camara): Etapa | null => {
  if (!ensaio) return null
  const concluidas = ensaio.etapas.filter(e => e.camara === camara && e.status === 'Concluida')
  return concluidas.length > 0
    ? concluidas.reduce((a, b) => (b.tentativa > a.tentativa ? b : a))
    : null
}

const Ensaio = () => {
  const navigate = useNavigate()
  const { podeOperar } = useAuth()

  const [ensaio, setEnsaio] = useState<EnsaioAberto | null>(null)
  const [pendentes, setPendentes] = useState<EnsaioAberto[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  // Equipamento sob teste — escolhido aqui, não numa tela de configuração à parte
  const [clientes, setClientes] = useState<OpcaoCliente[]>([])
  const [cilindros, setCilindros] = useState<OpcaoCilindro[]>([])
  const [clienteId, setClienteId] = useState<number | ''>('')
  const [cilindroId, setCilindroId] = useState<number | ''>('')

  // Identificação do documento — preenchida uma vez, no passo 1
  const [vessel, setVessel] = useState('')
  const [localTeste, setLocalTeste] = useState('')
  const [departamento, setDepartamento] = useState('')
  const [ordemServico, setOrdemServico] = useState('')

  // Parâmetros por câmara (o operador confirma antes de cada corrida)
  const [parametros, setParametros] = useState<Record<Camara, { pressao: string; tempo: string }>>({
    A: { pressao: '', tempo: '' },
    B: { pressao: '', tempo: '' },
  })

  const [dados, setDados] = useState<DataPoint[]>([])
  const [totalPontosColetados, setTotalPontosColetados] = useState(0)
  const [logEventos, setLogEventos] = useState<LogEvento[]>([])
  const [tempoAtual, setTempoAtual] = useState(Date.now())

  const [pressoesBancada, setPressoesBancada] = useState<PressoesBancada | null>(null)
  const [bloqueioPressao, setBloqueioPressao] = useState<BloqueioPressao | null>(null)

  // Câmara aguardando o CLP confirmar a partida (REGISTRO_RODANDO) — segura o spinner
  const [iniciando, setIniciando] = useState<Camara | null>(null)

  const [modalEncerrar, setModalEncerrar] = useState<Etapa | null>(null)
  const [modalCancelar, setModalCancelar] = useState(false)
  const [pendenteADescartar, setPendenteADescartar] = useState<EnsaioAberto | null>(null)
  const [ocupado, setOcupado] = useState(false)

  const etapaAtiva = ensaio?.etapas.find(e => e.id === ensaio.etapaEmExecucaoId) ?? null
  const etapaAtivaAnteriorRef = useRef<number | null>(null)

  const registrarLog = useCallback((texto: string, tipo: 'normal' | 'desvio' = 'normal') => {
    setLogEventos(prev => [
      { id: Date.now() + Math.floor(Math.random() * 1000), texto: `[${new Date().toLocaleTimeString('pt-BR')}] ${texto}`, tipo, comentarios: 0 },
      ...prev,
    ])
  }, [])

  const mensagemDeErro = (err: any, padrao: string) =>
    err?.response?.data?.message || padrao

  // ── Carga inicial: o backend é a fonte da verdade do que está aberto ──────
  const carregarPendentes = useCallback(async () => {
    try {
      const { data } = await api.get('/ensaio/pendentes')
      setPendentes(Array.isArray(data) ? data : [])
    } catch (err) {
      console.error('Erro ao listar ensaios pendentes:', err)
    }
  }, [])

  useEffect(() => {
    const carregar = async () => {
      try {
        const { data } = await api.get('/ensaio/ativo')
        if (data?.ativo && data.ensaio) {
          setEnsaio(data.ensaio)
          registrarLog(`${rotuloEnsaio(data.ensaio)} retomado`)
        } else {
          await carregarPendentes()
        }
      } catch (err) {
        console.error('Erro ao retomar ensaio ativo:', err)
        setErro('Não foi possível carregar o ensaio em andamento.')
      } finally {
        setCarregando(false)
      }
    }

    carregar()
  }, [carregarPendentes, registrarLog])

  // Clientes disponíveis + pré-seleção pelo último ensaio (o backend guarda a escolha)
  useEffect(() => {
    const carregarOpcoes = async () => {
      try {
        const { data } = await api.get('/Cliente')
        const lista: OpcaoCliente[] = Array.isArray(data) ? data : []
        setClientes(lista)

        const { data: config } = await api.get('/config')
        const padraoCliente = config?.sistema?.clienteId
        const padraoCilindro = config?.sistema?.cilindroId

        if (padraoCliente && lista.some(c => c.id === padraoCliente)) {
          setClienteId(padraoCliente)
          if (padraoCilindro) setCilindroId(padraoCilindro)
        }
      } catch (err) {
        console.error('Erro ao carregar clientes:', err)
      }
    }

    carregarOpcoes()
  }, [])

  // Cilindros do cliente escolhido
  useEffect(() => {
    if (!clienteId) {
      setCilindros([])
      setCilindroId('')
      return
    }

    const carregarCilindros = async () => {
      try {
        const { data } = await api.get(`/Cilindro/cliente/${clienteId}`)
        const lista: OpcaoCilindro[] = Array.isArray(data) ? data : []
        setCilindros(lista)
        // Mantém a pré-seleção só se o cilindro pertencer mesmo a este cliente
        setCilindroId(prev => (prev !== '' && lista.some(c => c.id === prev) ? prev : ''))
      } catch (err) {
        console.error('Erro ao carregar cilindros do cliente:', err)
        setCilindros([])
        setCilindroId('')
      }
    }

    carregarCilindros()
  }, [clienteId])

  // Reconstrói o gráfico da etapa em execução ao reentrar na tela
  useEffect(() => {
    if (!ensaio || !etapaAtiva) return
    if (etapaAtivaAnteriorRef.current === etapaAtiva.id) return

    etapaAtivaAnteriorRef.current = etapaAtiva.id

    const reconstruir = async () => {
      try {
        const { data } = await api.get(`/ensaio/${ensaio.id}/historico`, {
          params: { etapaId: etapaAtiva.id },
        })
        const pontos: DataPoint[] = data?.dados ?? []
        setTotalPontosColetados(pontos.length)
        setDados(pontos.slice(-100))
      } catch (err) {
        console.error('Erro ao reconstruir histórico da etapa:', err)
      }
    }

    reconstruir()
  }, [ensaio, etapaAtiva])

  // ── Coleta ao vivo (1 Hz) ────────────────────────────────────────────────
  useEffect(() => {
    if (!ensaio || !etapaAtiva) return

    const abortController = new AbortController()
    let montado = true
    let emVoo = false

    const interval = setInterval(async () => {
      if (emVoo) return
      emVoo = true

      try {
        const { data } = await api.get(`/ensaio/${ensaio.id}/pressao-atual`, {
          signal: abortController.signal,
        })

        if (montado) {
          setDados(prev => [...prev, { time: data.time, pressaoA: data.pressaoA, pressaoB: data.pressaoB }].slice(-100))
          setTotalPontosColetados(prev => prev + 1)
        }
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && montado) {
          console.error('Erro ao ler pressão atual do ensaio:', err)
        }
      } finally {
        emVoo = false
      }
    }, 1000)

    return () => {
      montado = false
      abortController.abort()
      clearInterval(interval)
    }
  }, [ensaio, etapaAtiva])

  // ── Pressão da bancada com tudo parado (2 s) ─────────────────────────────
  // Enquanto nenhuma câmara roda, a coleta ao vivo não acontece. O operador ainda
  // precisa enxergar a pressão residual: é ela que libera ou trava o início.
  useEffect(() => {
    if (etapaAtiva) {
      setPressoesBancada(null)
      return
    }

    const abortController = new AbortController()
    let montado = true
    let emVoo = false

    const ler = async () => {
      if (emVoo) return
      emVoo = true

      try {
        const { data } = await api.get('/ensaio/pressoes', { signal: abortController.signal })
        if (montado) setPressoesBancada(data)
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && montado) {
          console.error('Erro ao ler pressões da bancada:', err)
          setPressoesBancada(null)
        }
      } finally {
        emVoo = false
      }
    }

    ler()
    const interval = setInterval(ler, 2000)

    return () => {
      montado = false
      abortController.abort()
      clearInterval(interval)
    }
  }, [etapaAtiva])

  // Cronômetro da etapa em execução
  useEffect(() => {
    if (!etapaAtiva) return
    const interval = setInterval(() => setTempoAtual(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [etapaAtiva])

  // ── Sincronização com o CLP ──────────────────────────────────────────────
  // O backend fecha a etapa sozinho quando o REGISTRO_RODANDO cai; aqui só
  // observamos a mudança de estado do ensaio e refletimos na tela.
  useEffect(() => {
    if (!ensaio || !etapaAtiva) return

    const interval = setInterval(async () => {
      try {
        const { data } = await api.get('/ensaio/ativo')
        if (!data?.ativo || !data.ensaio) return

        const atualizado: EnsaioAberto = data.ensaio
        if (atualizado.etapaEmExecucaoId === null && etapaAtiva) {
          registrarLog(`CLP concluiu a câmara ${etapaAtiva.camara}`)
        }
        setEnsaio(atualizado)
      } catch (err) {
        console.error('Erro ao sincronizar estado do ensaio:', err)
      }
    }, 3000)

    return () => clearInterval(interval)
  }, [ensaio, etapaAtiva, registrarLog])

  // ── Ações ────────────────────────────────────────────────────────────────
  const criarEnsaio = async () => {
    setErro(null)

    if (clienteId === '' || cilindroId === '') {
      setErro('Selecione o cliente e o cilindro que serão ensaiados.')
      return
    }

    setOcupado(true)

    try {
      const { data } = await api.post('/ensaio', {
        clienteId,
        cilindroId,
        vessel: vessel || null,
        localTeste: localTeste || null,
        departamento: departamento || null,
        ordemServico: ordemServico || null,
      })

      setEnsaio(data.ensaio)
      setDados([])
      setTotalPontosColetados(0)
      registrarLog(`${rotuloEnsaio(data.ensaio)} criado — escolha por qual câmara começar`)

      // Ao criar o ensaio o backend manda ao CLP os limites do cilindro e a calibração
      // dos sensores; se algo não foi enviado, o operador precisa saber antes de rodar.
      if (data.avisosModbus?.length) {
        setErro(`Parâmetros enviados ao CLP com avisos: ${data.avisosModbus.join(' · ')}`)
        data.avisosModbus.forEach((aviso: string) => registrarLog(aviso, 'desvio'))
      }
    } catch (err: any) {
      console.error('Erro ao criar ensaio:', err)
      setErro(mensagemDeErro(err, 'Erro ao criar ensaio'))
    } finally {
      setOcupado(false)
    }
  }

  const retomarPendente = (pendente: EnsaioAberto) => {
    setEnsaio(pendente)
    setPendentes([])
    setDados([])
    setTotalPontosColetados(0)
    registrarLog(`${rotuloEnsaio(pendente)} retomado`)
  }

  const iniciarEtapa = async (camara: Camara) => {
    if (!ensaio) return

    setErro(null)

    const pressao = Number(parametros[camara].pressao.replace(',', '.'))
    const tempo = Number(parametros[camara].tempo.replace(',', '.'))

    if (!parametros[camara].pressao || isNaN(pressao) || pressao <= 0) {
      setErro(`Informe uma pressão de carga válida para a câmara ${camara}.`)
      return
    }

    if (!parametros[camara].tempo || isNaN(tempo) || tempo <= 0) {
      setErro(`Informe um tempo de carga válido para a câmara ${camara}.`)
      return
    }

    setOcupado(true)
    setIniciando(camara)

    try {
      // Pode demorar alguns segundos: o backend só responde depois de confirmar por
      // leitura que o CLP está mesmo rodando.
      const { data } = await api.post(`/ensaio/${ensaio.id}/etapa`, {
        camara,
        pressaoCarga: pressao,
        tempoCarga: tempo,
      })

      setEnsaio(data.ensaio)
      setDados([])
      setTotalPontosColetados(0)
      registrarLog(`Câmara ${camara} iniciada — ${pressao} bar por ${tempo} min`)

      if (data.avisosModbus?.length) {
        setErro(`Câmara iniciada com avisos: ${data.avisosModbus.join(' · ')}`)
        data.avisosModbus.forEach((aviso: string) => registrarLog(aviso, 'desvio'))
      }
    } catch (err: any) {
      console.error('Erro ao iniciar etapa:', err)
      const msg = mensagemDeErro(err, 'Erro ao iniciar a câmara')
      registrarLog(msg, 'desvio')

      // Cilindro ainda pressurizado: modal bloqueante, não uma tarja que passa batido.
      if (err?.response?.data?.bloqueioPressao) {
        const dados = err.response.data
        setBloqueioPressao({
          mensagem: msg,
          pressaoA: dados.pressaoA ?? null,
          pressaoB: dados.pressaoB ?? null,
          limite: dados.limite ?? 3,
        })
      } else {
        setErro(msg)
      }
    } finally {
      setIniciando(null)
      setOcupado(false)
    }
  }

  const encerrarEtapa = async (salvar: boolean) => {
    const etapa = modalEncerrar
    if (!etapa) return

    setModalEncerrar(null)
    setOcupado(true)
    setErro(null)

    try {
      const { data } = await api.post(`/ensaio/etapa/${etapa.id}/encerrar`, null, {
        params: { salvar },
      })

      setEnsaio(data.ensaio)
      registrarLog(`Câmara ${etapa.camara} ${salvar ? 'salva' : 'descartada'}`)
    } catch (err: any) {
      console.error('Erro ao encerrar etapa:', err)
      setErro(mensagemDeErro(err, 'Erro ao encerrar a câmara'))
    } finally {
      setOcupado(false)
    }
  }

  const aceitarEnsaio = async () => {
    if (!ensaio) return

    setOcupado(true)
    setErro(null)

    try {
      const { data } = await api.post(`/ensaio/${ensaio.id}/aceitar`)
      registrarLog(`Ensaio aceito — relatório ${data.relatorioNumero} gerado`)
      navigate(`/relatorios/${data.relatorioId}`)
    } catch (err: any) {
      console.error('Erro ao aceitar ensaio:', err)
      setErro(mensagemDeErro(err, 'Erro ao aceitar o ensaio'))
    } finally {
      setOcupado(false)
    }
  }

  const cancelarEnsaio = async () => {
    if (!ensaio) return

    setModalCancelar(false)
    setOcupado(true)
    setErro(null)

    try {
      await api.post(`/ensaio/${ensaio.id}/cancelar`)
      registrarLog(`${rotuloEnsaio(ensaio)} cancelado — nenhum relatório gerado`)
      setEnsaio(null)
      setDados([])
      setTotalPontosColetados(0)
      etapaAtivaAnteriorRef.current = null
      await carregarPendentes()
    } catch (err: any) {
      console.error('Erro ao cancelar ensaio:', err)
      setErro(mensagemDeErro(err, 'Erro ao cancelar o ensaio'))
    } finally {
      setOcupado(false)
    }
  }

  const descartarPendente = async () => {
    const alvo = pendenteADescartar
    if (!alvo) return

    setPendenteADescartar(null)
    setOcupado(true)
    setErro(null)

    try {
      await api.post(`/ensaio/${alvo.id}/cancelar`)
      registrarLog(`${rotuloEnsaio(alvo)} descartado`)
      await carregarPendentes()
    } catch (err: any) {
      console.error('Erro ao descartar ensaio pendente:', err)
      setErro(mensagemDeErro(err, 'Erro ao descartar o ensaio'))
    } finally {
      setOcupado(false)
    }
  }

  const abrirComentarios = (eventoId: number) => {
    navigate(`/ensaio/comentarios/${eventoId}`)
  }

  // ── Render ───────────────────────────────────────────────────────────────
  const ultimaLeitura = dados.length > 0 ? dados[dados.length - 1] : null
  const decorrido = etapaAtiva
    ? (tempoAtual - new Date(etapaAtiva.dataInicio).getTime()) / 1000
    : 0

  // O que vale para o teste é a contagem do CLP (INICIA_CONTAGEM), não o clique:
  // até ela subir, a bancada está na rampa.
  const contando = etapaAtiva?.dataInicioContagem != null && etapaAtiva.dataFimContagem == null
  const decorridoContagem = etapaAtiva?.dataInicioContagem
    ? ((etapaAtiva.dataFimContagem ? new Date(etapaAtiva.dataFimContagem).getTime() : tempoAtual) -
        new Date(etapaAtiva.dataInicioContagem).getTime()) / 1000
    : 0

  if (carregando) {
    return (
      <div className="ensaio">
        <div className="page-header">
          <h1>Ensaio</h1>
        </div>
        <div className="ensaio-vazio">Carregando…</div>
      </div>
    )
  }

  return (
    <div className="ensaio">
      <div className="page-header">
        <div>
          <h1>Ensaio Hidráulico</h1>
          <p className="page-subtitle">
            {ensaio
              ? `${ensaio.cilindroNome ?? 'cilindro'} · ${ensaio.clienteNome ?? ''} — o número REH-MPR do laudo sai no aceite`
              : 'Cada ensaio testa as duas câmaras e gera um único relatório'}
          </p>
        </div>
        {ensaio && podeOperar && (
          <button className="btn btn-outline-danger" onClick={() => setModalCancelar(true)} disabled={ocupado}>
            Cancelar ensaio
          </button>
        )}
      </div>

      {erro && (
        <div className="ensaio-aviso" role="alert">
          <span>{erro}</span>
          <button className="ensaio-aviso-fechar" onClick={() => setErro(null)} aria-label="Fechar aviso">×</button>
        </div>
      )}

      {/* Passo 1 — identificação, só enquanto não existe ensaio aberto */}
      {!ensaio && podeOperar && (
        <div className="ensaio-card">
          <h2>Novo ensaio</h2>
          <p className="ensaio-card-hint">
            Escolha o equipamento e preencha a identificação — vale para as duas câmaras.
          </p>
          <div className="ensaio-form-grid">
            <div className="config-field">
              <label htmlFor="cliente">Cliente *</label>
              <select
                id="cliente"
                value={clienteId}
                onChange={e => setClienteId(e.target.value ? Number(e.target.value) : '')}
              >
                <option value="">Selecione o cliente…</option>
                {clientes.map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
            <div className="config-field">
              <label htmlFor="cilindro">Cilindro *</label>
              <select
                id="cilindro"
                value={cilindroId}
                onChange={e => setCilindroId(e.target.value ? Number(e.target.value) : '')}
                disabled={clienteId === ''}
              >
                <option value="">
                  {clienteId === '' ? 'Escolha o cliente primeiro' : 'Selecione o cilindro…'}
                </option>
                {cilindros.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.codigoCliente ? `${c.nome} (${c.codigoCliente})` : c.nome}
                  </option>
                ))}
              </select>
            </div>
            <div className="config-field">
              <label htmlFor="vessel">Vessel / Frota</label>
              <input id="vessel" type="text" value={vessel} onChange={e => setVessel(e.target.value)} />
            </div>
            <div className="config-field">
              <label htmlFor="localTeste">Local do teste</label>
              <input id="localTeste" type="text" value={localTeste} onChange={e => setLocalTeste(e.target.value)} />
            </div>
            <div className="config-field">
              <label htmlFor="departamento">Departamento</label>
              <input id="departamento" type="text" value={departamento} onChange={e => setDepartamento(e.target.value)} />
            </div>
            <div className="config-field">
              <label htmlFor="ordemServico">Ordem de serviço</label>
              <input id="ordemServico" type="text" value={ordemServico} onChange={e => setOrdemServico(e.target.value)} />
            </div>
          </div>
          <button className="btn btn-primary" onClick={criarEnsaio} disabled={ocupado}>
            Criar ensaio
          </button>
        </div>
      )}

      {/* Ensaios que ficaram pela metade */}
      {!ensaio && pendentes.length > 0 && (
        <div className="ensaio-card">
          <h2>Ensaios pendentes</h2>
          <p className="ensaio-card-hint">Ficaram sem uma câmara ou sem o aceite.</p>
          <ul className="pendentes-lista">
            {pendentes.map(p => (
              <li key={p.id} className="pendente-item">
                <div>
                  <strong>{rotuloEnsaio(p)}</strong>
                  <span className="pendente-detalhe">
                    {p.cilindroNome ?? 'cilindro'} · {p.ordemServico ? `OS ${p.ordemServico}` : 'sem OS'} ·{' '}
                    {p.status === 'AguardandoAceite'
                      ? 'aguardando aceite'
                      : `falta câmara ${CAMARAS.filter(c => !etapaValida(p, c)).join(' e ')}`}
                  </span>
                </div>
                {podeOperar && (
                  <div className="pendente-acoes">
                    <button className="btn btn-secondary" onClick={() => retomarPendente(p)}>
                      Retomar
                    </button>
                    <button className="btn btn-link" onClick={() => setPendenteADescartar(p)}>
                      Descartar
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      {!ensaio && !podeOperar && pendentes.length === 0 && (
        <div className="ensaio-vazio">Nenhum ensaio em andamento.</div>
      )}

      {/* Ensaio aberto */}
      {ensaio && (
        <>
          <div className="ensaio-identificacao">
            {[
              ['Vessel / Frota', ensaio.vessel],
              ['Local', ensaio.localTeste],
              ['Departamento', ensaio.departamento],
              ['Ordem de serviço', ensaio.ordemServico],
            ].map(([rotulo, valor]) => (
              <div key={rotulo as string} className="identificacao-item">
                <span className="identificacao-label">{rotulo}</span>
                <span className="identificacao-valor">{valor || '—'}</span>
              </div>
            ))}
          </div>

          {/* Bancada pressurizada: avisa antes de o operador tentar iniciar */}
          {pressoesBancada && !pressoesBancada.podeIniciar && (
            <div className="ensaio-aviso" role="status">
              <span>
                {pressoesBancada.impedimento ??
                  `Cilindro pressurizado — as duas câmaras precisam estar abaixo de ${pressoesBancada.limite} bar.`}
                {' '}(A: {formatarBar(pressoesBancada.pressaoA)} · B: {formatarBar(pressoesBancada.pressaoB)})
              </span>
            </div>
          )}

          <div className="camaras-grid">
            {CAMARAS.map(camara => {
              const rodando = etapaAtiva?.camara === camara
              const concluida = etapaValida(ensaio, camara)
              const outraRodando = etapaAtiva != null && !rodando

              return (
                <div
                  key={camara}
                  className={`camara-card ${rodando ? 'camara-rodando' : concluida ? 'camara-concluida' : ''}`}
                >
                  <div className="camara-header">
                    <h2>Câmara {camara}</h2>
                    <span className="camara-estado">
                      {rodando
                        ? contando
                          ? '● Contando'
                          : '● Rampa — aguardando contagem'
                        : concluida
                          ? '✔ Concluída'
                          : '○ Não iniciada'}
                    </span>
                  </div>

                  {rodando && etapaAtiva && (
                    <>
                      <dl className="camara-dados">
                        <div>
                          <dt>Setpoint</dt>
                          <dd>{etapaAtiva.pressaoCargaConfigurada} bar</dd>
                        </div>
                        <div>
                          <dt>Tempo de carga</dt>
                          <dd>{etapaAtiva.tempoCargaConfigurado} min</dd>
                        </div>
                        <div>
                          <dt>Decorrido</dt>
                          <dd>{formatarDuracao(decorrido)}</dd>
                        </div>
                        <div>
                          <dt>Contagem (CLP)</dt>
                          <dd>
                            {etapaAtiva.dataInicioContagem
                              ? formatarDuracao(decorridoContagem)
                              : 'aguardando'}
                          </dd>
                        </div>
                      </dl>
                      {podeOperar && (
                        <button
                          className="btn btn-danger"
                          onClick={() => setModalEncerrar(etapaAtiva)}
                          disabled={ocupado}
                        >
                          Encerrar câmara {camara}
                        </button>
                      )}
                    </>
                  )}

                  {!rodando && concluida && (
                    <>
                      <dl className="camara-dados">
                        <div>
                          <dt>Setpoint</dt>
                          <dd>{concluida.pressaoCargaConfigurada} bar</dd>
                        </div>
                        <div>
                          <dt>Duração</dt>
                          <dd>
                            {concluida.dataFim
                              ? formatarDuracao(
                                  (new Date(concluida.dataFim).getTime() - new Date(concluida.dataInicio).getTime()) / 1000
                                )
                              : '—'}
                          </dd>
                        </div>
                        <div>
                          <dt>Tentativa</dt>
                          <dd>{concluida.tentativa}</dd>
                        </div>
                      </dl>
                      {podeOperar && (
                        <button
                          className="btn btn-secondary"
                          onClick={() => iniciarEtapa(camara)}
                          disabled={ocupado || outraRodando}
                          title={outraRodando ? 'Encerre a outra câmara antes' : undefined}
                        >
                          {iniciando === camara ? (
                            <>
                              <span className="spinner" aria-hidden="true" /> Confirmando no CLP…
                            </>
                          ) : (
                            `Repetir câmara ${camara}`
                          )}
                        </button>
                      )}
                    </>
                  )}

                  {!rodando && !concluida && (
                    <>
                      <div className="camara-parametros">
                        <div className="config-field">
                          <label htmlFor={`pressao-${camara}`}>Pressão de carga (bar)</label>
                          <input
                            id={`pressao-${camara}`}
                            type="number"
                            step="0.01"
                            value={parametros[camara].pressao}
                            onChange={e =>
                              setParametros(prev => ({ ...prev, [camara]: { ...prev[camara], pressao: e.target.value } }))
                            }
                            disabled={!podeOperar}
                          />
                        </div>
                        <div className="config-field">
                          <label htmlFor={`tempo-${camara}`}>Tempo de carga (min)</label>
                          <input
                            id={`tempo-${camara}`}
                            type="number"
                            step="0.01"
                            value={parametros[camara].tempo}
                            onChange={e =>
                              setParametros(prev => ({ ...prev, [camara]: { ...prev[camara], tempo: e.target.value } }))
                            }
                            disabled={!podeOperar}
                          />
                        </div>
                      </div>
                      {podeOperar && (
                        <button
                          className="btn btn-primary"
                          onClick={() => iniciarEtapa(camara)}
                          disabled={ocupado || outraRodando}
                          title={outraRodando ? 'Encerre a outra câmara antes' : undefined}
                        >
                          {iniciando === camara ? (
                            <>
                              <span className="spinner" aria-hidden="true" /> Confirmando no CLP…
                            </>
                          ) : (
                            `▶ Iniciar câmara ${camara}`
                          )}
                        </button>
                      )}
                    </>
                  )}
                </div>
              )
            })}
          </div>

          {podeOperar && (
            <div className="ensaio-aceite">
              <button
                className="btn btn-primary btn-aceite"
                onClick={aceitarEnsaio}
                disabled={!ensaio.podeAceitar || ocupado}
              >
                Aceitar ensaio e gerar relatório
              </button>
              {!ensaio.podeAceitar && (
                <span className="ensaio-aceite-hint">
                  {etapaAtiva
                    ? 'Encerre a câmara em execução para poder aceitar.'
                    : `Falta concluir a câmara ${CAMARAS.filter(c => !etapaValida(ensaio, c)).join(' e ')}.`}
                </span>
              )}
            </div>
          )}

          <div className="ensaio-stats">
            <div className="stat-mini">
              <span className="stat-mini-label">Pressão A</span>
              <span className="stat-mini-value">
                {Math.round(ultimaLeitura?.pressaoA ?? pressoesBancada?.pressaoA ?? 0)} bar
              </span>
            </div>
            <div className="stat-mini">
              <span className="stat-mini-label">Pressão B</span>
              <span className="stat-mini-value">
                {Math.round(ultimaLeitura?.pressaoB ?? pressoesBancada?.pressaoB ?? 0)} bar
              </span>
            </div>
            <div className="stat-mini">
              <span className="stat-mini-label">Tempo Decorrido</span>
              <span className="stat-mini-value">{formatarDuracao(decorrido)}</span>
            </div>
            <div className="stat-mini">
              <span className="stat-mini-label">Pontos Coletados</span>
              <span className="stat-mini-value">{totalPontosColetados}</span>
            </div>
            <div className="stat-mini">
              <span className="stat-mini-label">Registro</span>
              <span className={`stat-mini-value ${etapaAtiva ? 'status-active' : 'status-inactive'}`}>
                {etapaAtiva
                  ? `● Câmara ${etapaAtiva.camara} · ${contando ? 'contando' : 'rampa'}`
                  : '○ Parado'}
              </span>
            </div>
          </div>

          <div className="ensaio-main-content">
            <div className="grafico-container">
              <div className="grafico-card">
                <h2>
                  {etapaAtiva
                    ? `Curva de Pressão — Câmara ${etapaAtiva.camara} em tempo real`
                    : 'Curva de Pressão'}
                </h2>
                <ResponsiveContainer width="100%" height={280}>
                  <LineChart data={dados}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#E0E0E0" />
                    <XAxis dataKey="time" stroke="#666" tick={{ fill: '#666', fontSize: 11 }} />
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
                        borderRadius: '8px',
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
                    logEventos.map(evento => (
                      <div key={evento.id} className={`log-entry ${evento.tipo === 'desvio' ? 'log-desvio' : ''}`}>
                        <span className="log-texto">{evento.texto}</span>
                        {evento.tipo === 'desvio' && (
                          <button
                            className="log-comentario-btn"
                            onClick={() => abrirComentarios(evento.id)}
                            title="Adicionar comentário sobre o desvio"
                          >
                            <span className="comentario-icon-empty">💬</span>
                          </button>
                        )}
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>
        </>
      )}

      {/* Cilindro pressurizado — impede o início; sem ação possível além de aliviar */}
      {bloqueioPressao && (
        <div className="modal-overlay" role="alertdialog" aria-modal="true" aria-labelledby="titulo-bloqueio-pressao">
          <div className="modal-card">
            <h2 id="titulo-bloqueio-pressao">Não é possível iniciar o ensaio</h2>
            <p>{bloqueioPressao.mensagem}</p>
            <dl className="camara-dados">
              <div>
                <dt>Câmara A</dt>
                <dd>{formatarBar(bloqueioPressao.pressaoA)}</dd>
              </div>
              <div>
                <dt>Câmara B</dt>
                <dd>{formatarBar(bloqueioPressao.pressaoB)}</dd>
              </div>
              <div>
                <dt>Limite</dt>
                <dd>abaixo de {bloqueioPressao.limite} bar</dd>
              </div>
            </dl>
            <div className="modal-acoes">
              <button className="btn btn-primary" onClick={() => setBloqueioPressao(null)}>
                Entendi
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Encerrar câmara — três saídas explícitas, inclusive voltar sem fazer nada */}
      {modalEncerrar && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="titulo-encerrar">
          <div className="modal-card">
            <h2 id="titulo-encerrar">Encerrar a câmara {modalEncerrar.camara}?</h2>
            <p>
              O registro no CLP será desligado. Salvar mantém esta corrida para o laudo;
              descartar apaga as leituras dela.
            </p>
            <div className="modal-acoes">
              <button className="btn btn-primary" onClick={() => encerrarEtapa(true)}>
                Salvar etapa
              </button>
              <button className="btn btn-outline-danger" onClick={() => encerrarEtapa(false)}>
                Descartar etapa
              </button>
              <button className="btn btn-link" onClick={() => setModalEncerrar(null)}>
                Voltar ao ensaio
              </button>
            </div>
          </div>
        </div>
      )}

      {pendenteADescartar && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="titulo-descartar">
          <div className="modal-card">
            <h2 id="titulo-descartar">Descartar o {rotuloEnsaio(pendenteADescartar).toLowerCase()}?</h2>
            <p>
              As corridas já feitas são perdidas e as leituras apagadas. Nenhum relatório é
              gerado. Use para limpar ensaios que ficaram pela metade.
            </p>
            <div className="modal-acoes">
              <button className="btn btn-outline-danger" onClick={descartarPendente}>
                Descartar ensaio
              </button>
              <button className="btn btn-link" onClick={() => setPendenteADescartar(null)}>
                Voltar
              </button>
            </div>
          </div>
        </div>
      )}

      {modalCancelar && ensaio && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="titulo-cancelar">
          <div className="modal-card">
            <h2 id="titulo-cancelar">Cancelar o {rotuloEnsaio(ensaio).toLowerCase()}?</h2>
            <p>
              As duas câmaras são descartadas e as leituras apagadas. Nenhum relatório é gerado
              e nenhum número é consumido.
            </p>
            <div className="modal-acoes">
              <button className="btn btn-outline-danger" onClick={cancelarEnsaio}>
                Cancelar ensaio
              </button>
              <button className="btn btn-link" onClick={() => setModalCancelar(false)}>
                Voltar ao ensaio
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default Ensaio
