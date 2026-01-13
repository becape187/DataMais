using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

public class Cilindro
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Required]
    [MaxLength(50)]
    public string CodigoCliente { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CodigoInterno { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? Fabricante { get; set; }

    public DateTime? DataFabricacao { get; set; }

    // Dimensões (em mm)
    [Column(TypeName = "decimal(10,2)")]
    public decimal? DiametroInterno { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ComprimentoHaste { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? DiametroHaste { get; set; }

    // Pressões Máximas (em bar)
    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaximaPressaoSuportadaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaximaPressaoSuportadaB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaximaPressaoSegurancaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaximaPressaoSegurancaB { get; set; }

    // Parâmetros de Ensaio - Câmara A
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PreCargaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? CargaNominalA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoRampaSubidaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoDuracaoCargaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoRampaDescidaA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PercentualVariacaoAlarmeA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? HistereseAlarmeA { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PercentualVariacaoDesligaProcessoA { get; set; }

    // Parâmetros de Ensaio - Câmara B
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PreCargaB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? CargaNominalB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoRampaSubidaB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoDuracaoCargaB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoRampaDescidaB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PercentualVariacaoAlarmeB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? HistereseAlarmeB { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PercentualVariacaoDesligaProcessoB { get; set; }

    // Relacionamentos
    [Required]
    public int ClienteId { get; set; }
    public virtual Cliente Cliente { get; set; } = null!;

    public virtual ICollection<Ensaio> Ensaios { get; set; } = new List<Ensaio>();
    public virtual ICollection<Relatorio> Relatorios { get; set; } = new List<Relatorio>();

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
