using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

/// <summary>
/// Contador sequencial dos números de relatório, um registro por ano.
/// O incremento é feito por UPSERT atômico no PostgreSQL (ver <c>NumeroRelatorioService</c>),
/// garantindo que dois relatórios nunca recebam o mesmo número.
/// </summary>
public class ContadorRelatorio
{
    /// <summary>Ano do sequencial (ex.: 2026). O contador reinicia em 1 a cada ano.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Ano { get; set; }

    /// <summary>Último sequencial emitido no ano.</summary>
    public int UltimoNumero { get; set; }
}
