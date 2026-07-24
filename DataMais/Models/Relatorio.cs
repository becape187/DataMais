using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class Relatorio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Numero { get; set; } = string.Empty;

    [Required]
    public DateTime Data { get; set; }

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    [MaxLength(500)]
    public string? CaminhoArquivo { get; set; }

    // ── Ciclo de vida / versionamento (relatório rev02) ─────────────────────
    /// <summary>Rascunho (editável, sem PDF) ou Concluido (assinado, PDF liberado).</summary>
    [MaxLength(20)]
    public string Situacao { get; set; } = "Rascunho";

    /// <summary>Número de vezes que o relatório foi concluído/assinado (0 = nunca).</summary>
    public int Versao { get; set; } = 0;

    /// <summary>Usuário que concluiu a versão atual (assinatura via login).</summary>
    public int? ConcluidoPorUsuarioId { get; set; }

    [MaxLength(100)]
    public string? ConcluidoPorNome { get; set; }

    public DateTime? DataConclusao { get; set; }

    // Relacionamentos
    [Required]
    public int ClienteId { get; set; }
    public virtual Cliente Cliente { get; set; } = null!;

    [Required]
    public int CilindroId { get; set; }
    public virtual Cilindro Cilindro { get; set; } = null!;

    public int? EnsaioId { get; set; }
    public virtual Ensaio? Ensaio { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }

    // Relacionamento com respostas dos campos
    public virtual ICollection<RespostaCampoRelatorio> RespostasCampos { get; set; } = new List<RespostaCampoRelatorio>();

    // Histórico de versões (conclusões/reaberturas)
    public virtual ICollection<RelatorioVersao> Versoes { get; set; } = new List<RelatorioVersao>();
}
