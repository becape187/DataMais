using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class RespostaCampoRelatorio
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RelatorioId { get; set; }
    public virtual Relatorio Relatorio { get; set; } = null!;

    [Required]
    public int CampoRelatorioId { get; set; }
    public virtual CampoRelatorio CampoRelatorio { get; set; } = null!;

    [MaxLength(2000)]
    public string? Valor { get; set; } // Armazena a resposta (Sim/Não ou texto)

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
