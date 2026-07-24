using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    // Identificação do documento (relatório rev02) — preenchidos no setup do ensaio.
    /// <summary>Embarcação / unidade testada (ex.: MV29 / Frota).</summary>
    [MaxLength(100)]
    public string? Vessel { get; set; }

    /// <summary>Local do teste (ex.: Macaé).</summary>
    [MaxLength(200)]
    public string? LocalTeste { get; set; }

    /// <summary>Departamento responsável (ex.: ONSHORE PRESERVATION).</summary>
    [MaxLength(100)]
    public string? Departamento { get; set; }

    /// <summary>Ordem de Serviço / Work Order.</summary>
    [MaxLength(100)]
    public string? OrdemServico { get; set; }

    /// <summary>
    /// Câmara testada no ensaio (ex: "A" / "B").
    /// </summary>
    [MaxLength(20)]
    public string? CamaraTestada { get; set; }

    /// <summary>
    /// Pressão de carga configurada para o ensaio (bar).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PressaoCargaConfigurada { get; set; }

    /// <summary>
    /// Tempo de carga configurado para o ensaio (segundos).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoCargaConfigurado { get; set; }

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
