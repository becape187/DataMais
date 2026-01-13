using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class Cliente
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(18)]
    public string? Cnpj { get; set; }

    [MaxLength(100)]
    public string? Contato { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }

    // Relacionamentos
    public virtual ICollection<Cilindro> Cilindros { get; set; } = new List<Cilindro>();
    public virtual ICollection<Ensaio> Ensaios { get; set; } = new List<Ensaio>();
}
