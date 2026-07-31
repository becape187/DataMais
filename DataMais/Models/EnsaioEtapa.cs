using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

/// <summary>
/// Uma corrida do ensaio em uma das câmaras. Um ensaio completo tem sempre as duas
/// câmaras (A e B), em qualquer ordem, e cada uma pode ser repetida — cada repetição
/// é uma nova etapa com <see cref="Tentativa"/> incrementada.
/// A janela [DataInicio, DataFim] é o que recorta as leituras da etapa no InfluxDB,
/// já que todas as etapas do ensaio compartilham a mesma tag <c>ensaioId</c>.
/// Dentro dela, [DataInicioContagem, DataFimContagem] é o patamar de teste sinalizado
/// pelo CLP — é essa janela menor que o laudo analisa.
/// </summary>
public class EnsaioEtapa
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EnsaioId { get; set; }
    public virtual Ensaio Ensaio { get; set; } = null!;

    /// <summary>Câmara pressurizada nesta etapa: "A" (avança) ou "B" (recua).</summary>
    [Required]
    [MaxLength(1)]
    public string Camara { get; set; } = string.Empty;

    /// <summary>Número da tentativa nesta câmara (1 = primeira corrida).</summary>
    public int Tentativa { get; set; } = 1;

    /// <summary>Ver <see cref="StatusEtapa"/>.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = StatusEtapa.EmExecucao;

    [Required]
    public DateTime DataInicio { get; set; }

    public DateTime? DataFim { get; set; }

    /// <summary>
    /// Instante em que o CLP ligou o INICIA_CONTAGEM — o começo real do patamar de teste,
    /// depois da rampa. É o t0 da análise do laudo. Nulo enquanto o CLP não sinalizou
    /// (e nos ensaios anteriores a este registro, que caem na regra antiga do setpoint).
    /// </summary>
    public DateTime? DataInicioContagem { get; set; }

    /// <summary>Instante em que o INICIA_CONTAGEM caiu — fim do patamar de teste.</summary>
    public DateTime? DataFimContagem { get; set; }

    /// <summary>Setpoint de pressão desta etapa (bar).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal PressaoCargaConfigurada { get; set; }

    /// <summary>Tempo de carga desta etapa (minutos).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal TempoCargaConfigurado { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}

/// <summary>Estados de uma <see cref="EnsaioEtapa"/>.</summary>
public static class StatusEtapa
{
    /// <summary>Rodando agora no CLP.</summary>
    public const string EmExecucao = "EmExecucao";

    /// <summary>Encerrada e salva — é o que vale para o laudo.</summary>
    public const string Concluida = "Concluida";

    /// <summary>Encerrada e descartada pelo operador; leituras removidas do Influx.</summary>
    public const string Descartada = "Descartada";

    /// <summary>Foi concluída, mas uma tentativa posterior da mesma câmara a substituiu.</summary>
    public const string Repetida = "Repetida";
}
