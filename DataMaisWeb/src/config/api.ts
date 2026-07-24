import axios from 'axios'

// Configuração da URL base da API
// Em produção, usa o domínio HTTPS, em desenvolvimento usa localhost
const API_BASE_URL = import.meta.env.PROD
  ? 'https://modec.automais.cloud/api'
  : import.meta.env.VITE_API_URL || 'https://modec.automais.cloud/api'

// Cria instância do axios com configurações padrão
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 45000, // 45 segundos (maior que o timeout do Modbus de 10s + margem)
})

// Interceptor de request: injeta o token JWT (se houver) em toda chamada
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('datamais_token')
  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Interceptor para tratamento de erros
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const url: string = error.config?.url ?? ''

    // Sessão expirada / não autorizado: limpa credenciais e volta ao login.
    // Não redireciona quando o próprio login falha (mostra o erro na tela).
    if (status === 401 && !url.includes('/auth/login')) {
      localStorage.removeItem('datamais_token')
      localStorage.removeItem('datamais_usuario')
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }

    if (error.response) {
      console.error('Erro da API:', error.response.data)
    } else if (error.request) {
      console.error('Erro de rede:', error.request)
    } else {
      console.error('Erro:', error.message)
    }
    return Promise.reject(error)
  }
)

export default api
