import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import './ComentariosDesvio.css'

interface Comentario {
  id: number
  usuario: string
  data: string
  comentario: string
}

const ComentariosDesvio = () => {
  const params = useParams<{ eventoId: string }>()
  const navigate = useNavigate()
  
  // TODO: Usar params.eventoId para buscar dados do evento específico da API
  // Por enquanto usando dados mockados
  
  const [comentarios, setComentarios] = useState<Comentario[]>([
    {
      id: 1,
      usuario: 'João Silva',
      data: '22/12/2024 14:35',
      comentario: 'Verificado problema no sensor. Pressão estava acima do limite esperado.'
    },
    {
      id: 2,
      usuario: 'Maria Santos',
      data: '22/12/2024 14:40',
      comentario: 'Sensor recalibrado. Teste realizado novamente com sucesso.'
    }
  ])
  
  const [novoComentario, setNovoComentario] = useState('')
  const evento = 'Desvio detectado: Pressão 245.8 bar'

  const adicionarComentario = () => {
    if (!novoComentario.trim()) return

    const comentario: Comentario = {
      id: comentarios.length + 1,
      usuario: 'Usuário Atual', // Em produção viria do contexto de autenticação
      data: new Date().toLocaleString('pt-BR'),
      comentario: novoComentario
    }

    setComentarios([...comentarios, comentario])
    setNovoComentario('')
  }

  return (
    <div className="comentarios-desvio">
      <div className="page-header">
        <div>
          <button onClick={() => navigate(-1)} className="back-link">← Voltar</button>
          <h1>Comentários do Desvio</h1>
          <p className="page-subtitle">{evento}</p>
        </div>
      </div>

      <div className="comentarios-container">
        <div className="evento-card">
          <div className="evento-header">
            <span className="evento-icon">⚠️</span>
            <div>
              <h3>Evento de Desvio</h3>
              <p className="evento-texto">{evento}</p>
              <span className="evento-data">22/12/2024 14:30</span>
            </div>
          </div>
        </div>

        <div className="comentarios-list">
          <h2>Comentários ({comentarios.length})</h2>
          {comentarios.length === 0 ? (
            <div className="sem-comentarios">
              <p>Nenhum comentário adicionado ainda.</p>
            </div>
          ) : (
            <div className="comentarios-items">
              {comentarios.map(comentario => (
                <div key={comentario.id} className="comentario-item">
                  <div className="comentario-header">
                    <div>
                      <strong>{comentario.usuario}</strong>
                      <span className="comentario-data">{comentario.data}</span>
                    </div>
                  </div>
                  <p className="comentario-texto">{comentario.comentario}</p>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="adicionar-comentario">
          <h3>Adicionar Comentário</h3>
          <textarea
            value={novoComentario}
            onChange={(e) => setNovoComentario(e.target.value)}
            placeholder="Descreva o motivo da avaria ou observações sobre este desvio..."
            rows={4}
            className="comentario-input"
          />
          <button onClick={adicionarComentario} className="btn btn-primary">
            💬 Adicionar Comentário
          </button>
        </div>
      </div>
    </div>
  )
}

export default ComentariosDesvio


