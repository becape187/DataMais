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
  /** Relógio do servidor no momento da resposta — referência dos cronômetros. */
  agoraServidor?: string
  id: number
  numero: string
  status: 'EmAndamento' | 'AguardandoAceite' | 'Aceito' | 'Cancelado'
  dataCriacao: string
  localTeste: string | null
  departamento: string | null
  ordemServico: string | null
  clienteNome: string | null
  cilindroNome: string | null
  etapas: Etapa[]
  etapaEmExecucaoId: number | null
  /** Câmaras que este ensaio testa — as duas por padrão. Desmarcada não é exigida no aceite. */
  camaraAHabilitada: boolean
  camaraBHabilitada: boolean
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

/** Última leitura de um sinal de ciclo do CLP, feita pelo monitor no backend. */
interface SinalClp {
  ligado: boolean | null
  lidoEm: string | null
  erro: string | null
}

interface SinaisClp {
  registroRodando: SinalClp
  iniciaContagem: SinalClp
}

/** Estado das pressões da bancada com o ensaio parado — quem libera o início. */
interface PressoesBancada {
  agoraServidor?: string
  pressaoA: number | null
  pressaoB: number | null
  limite: number
  podeIniciar: boolean
  impedimento: string | null
  sinais?: SinaisClp
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
/**
 * Renderização de um sinal do CLP: ligado / desligado / erro / sem leitura.
 * Leitura velha (> 15 s — acima do ciclo de 1 s do monitor e do backoff ocioso de
 * 10 s) é denunciada como tal: um "Ligado" congelado de minutos atrás enganaria o
 * operador exatamente no momento em que o CLP caiu.
 */
const descreverSinal = (sinal: SinalClp | undefined, agora: number) => {
  if (!sinal || (sinal.ligado == null && !sinal.erro && !sinal.lidoEm)) {
    return { texto: '— sem leitura', classe: 'status-inactive' }
  }

  const idadeSeg = sinal.lidoEm ? (agora - new Date(sinal.lidoEm).getTime()) / 1000 : null
  if (idadeSeg != null && idadeSeg > 15) {
    return { texto: `⚠ leitura de ${Math.round(idadeSeg)}s atrás`, classe: 'sinal-erro' }
  }

  if (sinal.ligado == null) return { texto: '⚠ falha na leitura', classe: 'sinal-erro' }
  return sinal.ligado
    ? { texto: '● Ligado', classe: 'status-active' }
    : { texto: '○ Desligado', classe: 'status-inactive' }
}

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

/** A câmara faz parte deste ensaio? Ensaio antigo (sem o campo) conta como as duas. */
const camaraHabilitada = (ensaio: EnsaioAberto | null, camara: Camara): boolean => {
  if (!ensaio) return true
  const flag = camara === 'A' ? ensaio.camaraAHabilitada : ensaio.camaraBHabilitada
  return flag !== false
}

/** Câmaras que este ensaio ainda precisa concluir para poder ser aceito. */
const camarasPendentes = (ensaio: EnsaioAberto | null): Camara[] =>
  CAMARAS.filter(c => camaraHabilitada(ensaio, c) && !etapaValida(ensaio, c))

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

  // Identificação do documento — preenchida uma vez, no passo 1.
  // Vessel/Frota não entra aqui: é o próprio cadastro escolhido acima (tabela Clientes).
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

  // Estado real dos sinais do CLP, lido pelo monitor do backend (cache, sem custo
  // Modbus): é a conferência ao vivo de que a leitura está funcionando.
  const [sinaisClp, setSinaisClp] = useState<SinaisClp | null>(null)

  // Câmara aguardando o CLP confirmar a partida (REGISTRO_RODANDO) — segura o spinner
  const [iniciando, setIniciando] = useState<Camara | null>(null)

  const [modalEncerrar, setModalEncerrar] = useState<Etapa | null>(null)
  const [modalCancelar, setModalCancelar] = useState(false)
  // Desmarcar câmara que já rodou joga fora a corrida dela — confirma antes.
  const [modalDesmarcar, setModalDesmarcar] = useState<{ camara: Camara; corridas: number } | null>(null)
  // Aceite com uma câmara só: o laudo sai sem a outra, e isso precisa ser dito.
  const [modalAceiteParcial, setModalAceiteParcial] = useState(false)
  const [pendenteADescartar, setPendenteADescartar] = useState<EnsaioAberto | null>(null)
  const [ocupado, setOcupado] = useState(false)

  const etapaAtiva = ensaio?.etapas.find(e => e.id === ensaio.etapaEmExecucaoId) ?? null
  const etapaAtivaAnteriorRef = useRef<number | null>(null)

  // Diferença entre o relógio do servidor e o do PC da bancada (ms). Os cronômetros
  // comparam timestamps do SERVIDOR entre si — o relógio local só fornece o tique.
  // Sem isso, um PC com fuso/hora errados (comum em bancada) deixava a subtração
  // negativa e TODOS os tempos travados em 00:00:00, parecendo bug de contagem.
  const offsetServidorRef = useRef(0)

  const sincronizarRelogio = useCallback((agoraServidor?: string | null) => {
    if (!agoraServidor) return
    const t = new Date(agoraServidor).getTime()
    if (Number.isFinite(t)) offsetServidorRef.current = t - Date.now()
  }, [])

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
          sincronizarRelogio(data.ensaio.agoraServidor)
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
          sincronizarRelogio(data.agoraServidor)
          setDados(prev => [...prev, { time: data.time, pressaoA: data.pressaoA, pressaoB: data.pressaoB }].slice(-100))
          setTotalPontosColetados(prev => prev + 1)
          if (data.sinais) setSinaisClp(data.sinais)
        }
      } catch (err: any) {
        if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED' && montado) {
          console.error('Erro ao ler pressão atual do ensaio:', err)
          // O 500 de "CLP fora" ainda carrega o estado dos sinais no corpo — é
          // justamente nessa hora que o painel precisa refletir a falha.
          const sinais = err.response?.data?.sinais
          if (sinais) setSinaisClp(sinais)
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
        if (montado) {
          sincronizarRelogio(data.agoraServidor)
          setPressoesBancada(data)
          if (data.sinais) setSinaisClp(data.sinais)
        }
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

  // Relógio de 1 s: cronômetros da etapa E idade das leituras dos sinais do CLP.
  // Roda sempre — se o polling morrer junto com o CLP, é este tick que faz o painel
  // de sinais envelhecer na tela em vez de congelar num "Ligado" antigo.
  useEffect(() => {
    const interval = setInterval(() => setTempoAtual(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [])

  // ── Sincronização com o CLP ──────────────────────────────────────────────
  // O backend fecha a etapa sozinho quando o REGISTRO_RODANDO cai. ATENÇÃO ao
  // detalhe que já mordeu: no instante em que o monitor conclui a etapa, o ensaio
  // DEIXA de ter câmara em execução e /ensaio/ativo passa a responder
  // { ativo: false } — durante meses o guard abaixo fazia `return` nesse caso e a
  // tela ficava presa em "Rodando" para sempre, obrigando o encerramento manual
  // (o backend tinha encerrado; a tela é que nunca ficava sabendo). Quando o ativo
  // sumir, é preciso buscar o estado final do ensaio pelo id.
  useEffect(() => {
    if (!ensaio || !etapaAtiva) return

    const interval = setInterval(async () => {
      try {
        const { data } = await api.get('/ensaio/ativo')

        if (data?.ativo && data.ensaio) {
          sincronizarRelogio(data.ensaio.agoraServidor)
          setEnsaio(data.ensaio)
          return
        }

        // Nada em execução no backend, mas a tela ainda mostra a câmara rodando:
        // o monitor concluiu (ou alguém encerrou por fora). Busca o estado final.
        const { data: detalhe } = await api.get(`/ensaio/${ensaio.id}`)
        sincronizarRelogio(detalhe?.agoraServidor)
        registrarLog(`CLP concluiu a câmara ${etapaAtiva.camara}`)
        setEnsaio(detalhe)
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
      setErro('Selecione o vessel/frota e o cilindro que serão ensaiados.')
      return
    }

    setOcupado(true)

    try {
      const { data } = await api.post('/ensaio', {
        clienteId,
        cilindroId,
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

  // ── Câmaras do ensaio ────────────────────────────────────────────────────
  // O ensaio nasce com as duas. Desmarcar uma libera o aceite sem ela — e, se ela
  // já rodou, descarta a corrida (é o caso de não levar aquele resultado adiante).
  const aplicarCamaras = async (camaraAHabilitada: boolean, camaraBHabilitada: boolean) => {
    if (!ensaio) return

    setModalDesmarcar(null)
    setOcupado(true)
    setErro(null)

    try {
      const { data } = await api.put(`/ensaio/${ensaio.id}/camaras`, {
        camaraAHabilitada,
        camaraBHabilitada,
      })

      setEnsaio(data.ensaio)

      const habilitadas = CAMARAS.filter(c => (c === 'A' ? camaraAHabilitada : camaraBHabilitada))
      registrarLog(
        habilitadas.length === 1
          ? `Ensaio segue somente com a câmara ${habilitadas[0]}` +
            (data.corridasDescartadas ? ` — ${data.corridasDescartadas} corrida(s) descartada(s)` : '')
          : 'Ensaio segue com as duas câmaras'
      )
    } catch (err: any) {
      console.error('Erro ao definir as câmaras do ensaio:', err)
      setErro(mensagemDeErro(err, 'Erro ao alterar as câmaras do ensaio'))
    } finally {
      setOcupado(false)
    }
  }

  const alternarCamara = (camara: Camara, marcar: boolean) => {
    if (!ensaio) return

    const novoA = camara === 'A' ? marcar : camaraHabilitada(ensaio, 'A')
    const novoB = camara === 'B' ? marcar : camaraHabilitada(ensaio, 'B')

    if (!novoA && !novoB) {
      setErro('O ensaio precisa de ao menos uma câmara. Marque a outra antes de desmarcar esta.')
      return
    }

    // Já rodou: sair do ensaio significa jogar fora a corrida — pergunta antes.
    const corridas = marcar
      ? 0
      : ensaio.etapas.filter(e => e.camara === camara && (e.status === 'Concluida' || e.status === 'Repetida')).length

    if (corridas > 0) {
      setModalDesmarcar({ camara, corridas })
      return
    }

    aplicarCamaras(novoA, novoB)
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

    setModalAceiteParcial(false)
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

  // Fechar o ensaio com uma câmara só não pode passar em branco: o laudo sai sem a
  // outra e o número REH-MPR é queimado assim mesmo.
  const confirmarAceite = () => {
    if (!ensaio) return

    if (CAMARAS.filter(c => camaraHabilitada(ensaio, c)).length < 2) {
      setModalAceiteParcial(true)
      return
    }

    aceitarEnsaio()
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

  // "Agora" no relógio DO SERVIDOR: tique local + offset medido a cada polling.
  // Nunca comparar timestamp do servidor com Date.now() cru — PC de bancada com
  // fuso errado deixava a conta negativa e os cronômetros travados em 00:00:00.
  const agoraCorrigido = tempoAtual + offsetServidorRef.current

  const decorrido = etapaAtiva
    ? (agoraCorrigido - new Date(etapaAtiva.dataInicio).getTime()) / 1000
    : 0

  // O que vale para o teste é a contagem do CLP (INICIA_CONTAGEM), não o clique:
  // até ela subir, a bancada está na rampa.
  const contando = etapaAtiva?.dataInicioContagem != null && etapaAtiva.dataFimContagem == null
  const decorridoContagem = etapaAtiva?.dataInicioContagem
    ? ((etapaAtiva.dataFimContagem ? new Date(etapaAtiva.dataFimContagem).getTime() : agoraCorrigido) -
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
              : 'Cada ensaio testa as duas câmaras — ou só uma, se você desmarcar a outra — e gera um único relatório'}
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
              <label htmlFor="cliente">Vessel/Frota *</label>
              <select
                id="cliente"
                value={clienteId}
                onChange={e => setClienteId(e.target.value ? Number(e.target.value) : '')}
              >
                <option value="">Selecione o vessel/frota…</option>
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
                  {clienteId === '' ? 'Escolha o vessel/frota primeiro' : 'Selecione o cilindro…'}
                </option>
                {cilindros.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.codigoCliente ? `${c.nome} (${c.codigoCliente})` : c.nome}
                  </option>
                ))}
              </select>
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
                      : `falta câmara ${camarasPendentes(p).join(' e ')}`}
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
              ['Vessel/Frota', ensaio.clienteNome],
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

          <div className="camaras-grid">
            {CAMARAS.map(camara => {
              const rodando = etapaAtiva?.camara === camara
              const concluida = etapaValida(ensaio, camara)
              const outraRodando = etapaAtiva != null && !rodando
              const habilitada = camaraHabilitada(ensaio, camara)
              // Última câmara marcada não pode sair: o ensaio ficaria sem nenhuma.
              const unicaMarcada = habilitada && CAMARAS.filter(c => camaraHabilitada(ensaio, c)).length === 1

              return (
                <div
                  key={camara}
                  className={`camara-card ${
                    !habilitada ? 'camara-fora' : rodando ? 'camara-rodando' : concluida ? 'camara-concluida' : ''
                  }`}
                >
                  <div className="camara-header">
                    <h2>Câmara {camara}</h2>
                    <span className="camara-estado">
                      {!habilitada
                        ? '— Fora deste ensaio'
                        : rodando
                          ? contando
                            ? '● Contando'
                            : '● Rampa — aguardando contagem'
                          : concluida
                            ? '✔ Concluída'
                            : '○ Não iniciada'}
                    </span>
                  </div>

                  {/* Marcada = a câmara faz parte do ensaio e é exigida no aceite.
                      Desmarcar é como o operador fecha um laudo de uma câmara só. */}
                  <label
                    className="camara-inclusao"
                    title={
                      rodando
                        ? 'Encerre a câmara antes de tirá-la do ensaio'
                        : unicaMarcada
                          ? 'O ensaio precisa de ao menos uma câmara'
                          : undefined
                    }
                  >
                    <input
                      type="checkbox"
                      checked={habilitada}
                      disabled={!podeOperar || ocupado || rodando || unicaMarcada}
                      onChange={e => alternarCamara(camara, e.target.checked)}
                    />
                    <span>Incluir a câmara {camara} neste ensaio</span>
                  </label>

                  {!habilitada && (
                    <p className="camara-fora-hint">
                      Não será ensaiada e não entra no laudo. Marque de volta para rodar.
                    </p>
                  )}

                  {habilitada && rodando && etapaAtiva && (
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
                          {/* O tempo do teste só corre com o INICIA_CONTAGEM ligado */}
                          <dt>Tempo de teste</dt>
                          <dd>
                            {etapaAtiva.dataInicioContagem
                              ? formatarDuracao(decorridoContagem)
                              : 'aguardando INICIA_CONTAGEM'}
                          </dd>
                        </div>
                        <div>
                          <dt>Desde a partida</dt>
                          <dd className="camara-dado-secundario">{formatarDuracao(decorrido)}</dd>
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

                  {habilitada && !rodando && concluida && (
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

                  {habilitada && !rodando && !concluida && (
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
                onClick={confirmarAceite}
                disabled={!ensaio.podeAceitar || ocupado}
              >
                Aceitar ensaio e gerar relatório
              </button>
              {!ensaio.podeAceitar && (
                <span className="ensaio-aceite-hint">
                  {etapaAtiva
                    ? 'Encerre a câmara em execução para poder aceitar.'
                    : `Falta concluir a câmara ${camarasPendentes(ensaio).join(' e ')}.`}
                </span>
              )}
              {ensaio.podeAceitar && CAMARAS.filter(c => camaraHabilitada(ensaio, c)).length < 2 && (
                <span className="ensaio-aceite-hint">
                  Só a câmara {CAMARAS.filter(c => camaraHabilitada(ensaio, c)).join('')} entra neste laudo.
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
              <span className="stat-mini-label">Tempo de Teste</span>
              <span className="stat-mini-value">
                {etapaAtiva?.dataInicioContagem ? formatarDuracao(decorridoContagem) : '—'}
              </span>
            </div>
            <div className="stat-mini">
              <span className="stat-mini-label">Pontos Coletados</span>
              <span className="stat-mini-value">{totalPontosColetados}</span>
            </div>
          </div>

          {/* Estado REAL dos sinais do CLP, lido pelo monitor do backend — é a conferência
              de que a leitura Modbus está funcionando. Fica numa faixa fina: como card
              grande, empurrava o gráfico para fora da tela. */}
          <div className="sinais-clp-faixa">
            {(() => {
              const rodando = descreverSinal(sinaisClp?.registroRodando, agoraCorrigido)
              const contagem = descreverSinal(sinaisClp?.iniciaContagem, agoraCorrigido)
              return (
                <>
                  <span className={`sinal-item ${rodando.classe}`} title={sinaisClp?.registroRodando?.erro ?? undefined}>
                    {rodando.texto} REGISTRO_RODANDO
                  </span>
                  <span className={`sinal-item ${contagem.classe}`} title={sinaisClp?.iniciaContagem?.erro ?? undefined}>
                    {contagem.texto} INICIA_CONTAGEM
                  </span>
                  <span className={`sinal-item ${etapaAtiva ? 'status-active' : 'status-inactive'}`}>
                    {etapaAtiva
                      ? `● Câmara ${etapaAtiva.camara} · ${contando ? 'contando' : 'rampa'}`
                      : '○ Registro parado'}
                  </span>
                </>
              )
            })()}
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

      {/* Tirar do ensaio uma câmara que já rodou = jogar fora a corrida dela */}
      {modalDesmarcar && ensaio && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="titulo-desmarcar">
          <div className="modal-card">
            <h2 id="titulo-desmarcar">Tirar a câmara {modalDesmarcar.camara} deste ensaio?</h2>
            <p>
              {modalDesmarcar.corridas === 1 ? 'A corrida já feita' : `As ${modalDesmarcar.corridas} corridas já feitas`}{' '}
              na câmara {modalDesmarcar.camara} {modalDesmarcar.corridas === 1 ? 'será descartada' : 'serão descartadas'} e
              as leituras apagadas. O laudo sai só com a câmara {modalDesmarcar.camara === 'A' ? 'B' : 'A'}.
            </p>
            <div className="modal-acoes">
              <button
                className="btn btn-outline-danger"
                onClick={() =>
                  aplicarCamaras(
                    modalDesmarcar.camara !== 'A' && camaraHabilitada(ensaio, 'A'),
                    modalDesmarcar.camara !== 'B' && camaraHabilitada(ensaio, 'B')
                  )
                }
              >
                Descartar e seguir sem ela
              </button>
              <button className="btn btn-link" onClick={() => setModalDesmarcar(null)}>
                Voltar ao ensaio
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Fechar o laudo com uma câmara só — o operador precisa ver isso escrito */}
      {modalAceiteParcial && ensaio && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="titulo-aceite-parcial">
          <div className="modal-card">
            <h2 id="titulo-aceite-parcial">Fechar o laudo com uma câmara só?</h2>
            <p>
              Este ensaio será aceito somente com a câmara{' '}
              <strong>{CAMARAS.filter(c => camaraHabilitada(ensaio, c)).join('')}</strong>. A câmara{' '}
              <strong>{CAMARAS.filter(c => !camaraHabilitada(ensaio, c)).join('')}</strong> não foi ensaiada e não
              aparece no relatório — o veredito sai só da câmara testada.
            </p>
            <p>O número REH-MPR é queimado agora e o laudo nasce assim.</p>
            <div className="modal-acoes">
              <button className="btn btn-primary" onClick={aceitarEnsaio} disabled={ocupado}>
                Aceitar assim e gerar relatório
              </button>
              <button className="btn btn-link" onClick={() => setModalAceiteParcial(false)}>
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
