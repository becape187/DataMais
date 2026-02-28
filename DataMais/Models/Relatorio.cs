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
}
