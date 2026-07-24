using System.ComponentModel.DataAnnotations;

namespace DataMais.Models;

/// <summary>
/// Registro de auditoria de cada conclusão/reabertura de um relatório (rev02).
/// Guarda quem assinou, quando, a ação e um snapshot do estado no momento.
/// </summary>
public class RelatorioVersao
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RelatorioId { get; set; }
    public virtual Relatorio Relatorio { get; set; } = null!;

    /// <summary>Número da versão associada ao evento.</summary>
    public int NumeroVersao { get; set; }

    /// <summary>"Concluido" ou "Reaberto".</summary>
    [Required]
    [MaxLength(20)]
    public string Acao { get; set; } = string.Empty;

    /// <summary>Usuário que executou a ação (assinatura via login).</summary>
    public int? UsuarioId { get; set; }

    [MaxLength(100)]
    public string? UsuarioNome { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;

    // Snapshot do estado no momento do evento
    [MaxLength(20)]
    public string? Resultado { get; set; }

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    /// <summary>Respostas do checklist serializadas em JSON no momento do evento.</summary>
    public string? RespostasJson { get; set; }
}
