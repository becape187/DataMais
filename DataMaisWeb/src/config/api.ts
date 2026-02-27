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

// Interceptor para tratamento de erros
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      // Erro com resposta do servidor
      console.error('Erro da API:', error.response.data)
    } else if (error.request) {
      // Erro de rede
      console.error('Erro de rede:', error.request)
    } else {
      // Outro erro
      console.error('Erro:', error.message)
    }
    return Promise.reject(error)
  }
)

export default api
