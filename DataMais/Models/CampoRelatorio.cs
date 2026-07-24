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

    /// <summary>
    /// Seção do relatório rev02 a que o campo pertence
    /// (ex.: "Inspeção Visual", "Testes Funcionais", "Condições Finais"). Nulo = "Perguntas Adicionais".
    /// </summary>
    [MaxLength(100)]
    public string? Secao { get; set; }

    /// <summary>
    /// Se true, uma resposta "Sim" neste campo (tipo SimOuNao) força o veredito do laudo para Reprovado.
    /// Usado pela regra do rev02 (ex.: "Vazamentos visíveis" = Sim → Reprovado).
    /// </summary>
    public bool ReprovaSeSim { get; set; } = false;

    [Required]
    public int Ordem { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataExclusao { get; set; } // Soft delete

    // Relacionamento com respostas
    public virtual ICollection<RespostaCampoRelatorio> Respostas { get; set; } = new List<RespostaCampoRelatorio>();
}
