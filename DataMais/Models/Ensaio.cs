using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class Ensaio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Numero { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "Pendente"; // Pendente, EmExecucao, Concluido, Cancelado

    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    // Relacionamentos
    [Required]
    public int ClienteId { get; set; }
    public virtual Cliente Cliente { get; set; } = null!;

    [Required]
    public int CilindroId { get; set; }
    public virtual Cilindro Cilindro { get; set; } = null!;

    public virtual ICollection<Relatorio> Relatorios { get; set; } = new List<Relatorio>();

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
