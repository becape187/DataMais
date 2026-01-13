using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

public class Usuario
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SenhaHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Role { get; set; } = "Usuario"; // Admin, Usuario, Operador

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public DateTime? UltimoLogin { get; set; }
}
