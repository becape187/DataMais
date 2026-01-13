using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

public class ModbusConfig
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    // Configuração de conexão
    [Required]
    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    [Required]
    public int Port { get; set; } = 502;

    [Required]
    public byte SlaveId { get; set; }

    // Tipo de função Modbus
    [Required]
    [MaxLength(20)]
    public string FuncaoModbus { get; set; } = string.Empty; // "ReadHoldingRegisters", "ReadInputRegisters", "WriteSingleRegister", etc.

    // Endereço do registro
    [Required]
    public ushort EnderecoRegistro { get; set; }

    // Quantidade de registros (para leitura múltipla)
    public ushort QuantidadeRegistros { get; set; } = 1;

    // Tipo de dado
    [MaxLength(20)]
    public string TipoDado { get; set; } = "UInt16"; // UInt16, Int16, UInt32, Int32, Float, etc.

    // Ordem dos bytes (para tipos maiores que 16 bits)
    [MaxLength(10)]
    public string ByteOrder { get; set; } = "BigEndian"; // BigEndian, LittleEndian

    // Fator de conversão (opcional)
    [Column(TypeName = "decimal(10,6)")]
    public decimal? FatorConversao { get; set; }

    // Offset (opcional)
    [Column(TypeName = "decimal(10,6)")]
    public decimal? Offset { get; set; }

    // Unidade de medida
    [MaxLength(20)]
    public string? Unidade { get; set; }

    // Ordem de leitura (para otimizar pooling - agrupar por IP)
    public int OrdemLeitura { get; set; }

    // Ativo/Inativo
    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
