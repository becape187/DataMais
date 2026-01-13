using System.ComponentModel.DataAnnotations;

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

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
