import { useParams, Link } from 'react-router-dom'
import './VisualizarRelatorio.css'

const VisualizarRelatorio = () => {
  const { id } = useParams<{ id: string }>()

  // Dados mockados do relatório
  const relatorio = {
    id: id,
    numero: 'REL-2024-001',
    cliente: 'MODEC Brasil',
    data: '22/12/2024 14:30',
    ensaioId: '#1234',
    tecnico: 'João Silva',
    sensor: 'Sensor de Pressão 01 (PT-350)',
    pressaoMaxima: '245.8 bar',
    pressaoMinima: '0.0 bar',
    duracao: '15 minutos',
    observacoes: 'Ensaio realizado com sucesso. Todos os parâmetros dentro dos limites especificados.',
    resultado: 'Aprovado'
  }

  return (
    <div className="visualizar-relatorio">
      <div className="page-header">
        <div>
          <Link to="/relatorios" className="back-link">← Voltar para Relatórios</Link>
          <h1>Relatório {relatorio.numero}</h1>
          <p className="page-subtitle">Ensaio {relatorio.ensaioId} - {relatorio.cliente}</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary">📥 Download PDF</button>
          <button className="btn btn-secondary">🖨️ Imprimir</button>
        </div>
      </div>

      <div className="relatorio-container">
        <div className="relatorio-header-card">
          <div className="relatorio-logo">
            <img src="/modec-logo.png" alt="MODEC Logo" />
          </div>
          <div className="relatorio-info">
            <h2>Relatório de Ensaio Hidráulico</h2>
            <div className="relatorio-meta">
              <div className="meta-item">
                <span className="meta-label">Número:</span>
                <span className="meta-value">{relatorio.numero}</span>
              </div>
              <div className="meta-item">
                <span className="meta-label">Data:</span>
                <span className="meta-value">{relatorio.data}</span>
              </div>
              <div className="meta-item">
                <span className="meta-label">Cliente:</span>
                <span className="meta-value">{relatorio.cliente}</span>
              </div>
              <div className="meta-item">
                <span className="meta-label">Ensaio:</span>
                <span className="meta-value">{relatorio.ensaioId}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Informações do Ensaio</h3>
          <div className="info-grid">
            <div className="info-card">
              <span className="info-label">Técnico Responsável</span>
              <span className="info-value">{relatorio.tecnico}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Sensor Utilizado</span>
              <span className="info-value">{relatorio.sensor}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Duração</span>
              <span className="info-value">{relatorio.duracao}</span>
            </div>
            <div className="info-card">
              <span className="info-label">Resultado</span>
              <span className={`info-value resultado ${relatorio.resultado.toLowerCase()}`}>
                {relatorio.resultado}
              </span>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Dados de Pressão</h3>
          <div className="pressao-grid">
            <div className="pressao-card">
              <span className="pressao-label">Pressão Máxima</span>
              <span className="pressao-value">{relatorio.pressaoMaxima}</span>
            </div>
            <div className="pressao-card">
              <span className="pressao-label">Pressão Mínima</span>
              <span className="pressao-value">{relatorio.pressaoMinima}</span>
            </div>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Gráfico de Pressão</h3>
          <div className="grafico-placeholder">
            <p>Gráfico de pressão em tempo real do ensaio</p>
            <p className="placeholder-note">Aqui será exibido o gráfico completo do ensaio</p>
          </div>
        </div>

        <div className="relatorio-section">
          <h3>Observações</h3>
          <div className="observacoes-box">
            <p>{relatorio.observacoes}</p>
          </div>
        </div>

        <div className="relatorio-footer">
          <div className="footer-signature">
            <div className="signature-line"></div>
            <p>Assinatura do Técnico Responsável</p>
          </div>
          <div className="footer-date">
            <p>Data de Emissão: {relatorio.data}</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export default VisualizarRelatorio


