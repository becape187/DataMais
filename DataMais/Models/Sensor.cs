using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

public class Sensor
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tipo { get; set; } = string.Empty; // "pressao", "carga", etc.

    [MaxLength(20)]
    public string? Unidade { get; set; }

    // Referência à configuração Modbus
    public int? ModbusConfigId { get; set; }
    public virtual ModbusConfig? ModbusConfig { get; set; }

    // Calibração linear: InputMin/OutputMin (ponto 1) e InputMax/OutputMax (ponto 2)
    // Conversão: Output = ((OutputMax - OutputMin) / (InputMax - InputMin)) * (Input - InputMin) + OutputMin
    [Column(TypeName = "decimal(10,6)")]
    public decimal? InputMin { get; set; } // Valor AD mínimo (x1)

    [Column(TypeName = "decimal(10,6)")]
    public decimal? OutputMin { get; set; } // Valor medido mínimo correspondente (y1)

    [Column(TypeName = "decimal(10,6)")]
    public decimal? InputMax { get; set; } // Valor AD máximo (x2)

    [Column(TypeName = "decimal(10,6)")]
    public decimal? OutputMax { get; set; } // Valor medido máximo correspondente (y2)

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
