using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class CampoRelatorio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TipoResposta { get; set; } = string.Empty; // "SimOuNao", "TextoSimples", "MultiplasLinhas"

    [Required]
    public int Ordem { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataExclusao { get; set; } // Soft delete

    // Relacionamento com respostas
    public virtual ICollection<RespostaCampoRelatorio> Respostas { get; set; } = new List<RespostaCampoRelatorio>();
}
