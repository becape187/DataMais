using DataMais.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataMais.Services;

/// <summary>
/// Gera o número oficial do relatório no padrão <c>REH-MPR-0000001-2026</c>:
/// prefixo fixo + sequencial de 7 dígitos + ano. O sequencial reinicia em 1 a cada ano.
/// </summary>
public static class NumeroRelatorioService
{
    /// <summary>Prefixo fixo do número de relatório.</summary>
    public const string Prefixo = "REH-MPR";

    /// <summary>Quantidade de dígitos do sequencial (0000001).</summary>
    private const int DigitosSequencial = 7;

    /// <summary>
    /// Reserva o próximo sequencial do ano e devolve o número formatado.
    /// O incremento é um UPSERT atômico (<c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c>),
    /// então duas conclusões simultâneas nunca recebem o mesmo número.
    /// </summary>
    /// <param name="context">DbContext da requisição/escopo.</param>
    /// <param name="dataRelatorio">Data do relatório (UTC); o ano é o ano local dessa data.</param>
    public static async Task<string> GerarProximoAsync(
        DataMaisDbContext context,
        DateTime dataRelatorio,
        CancellationToken ct = default)
    {
        var ano = ObterAno(dataRelatorio);
        var sequencial = await ReservarSequencialAsync(context, ano, ct);

        return Formatar(sequencial, ano);
    }

    /// <summary>Monta o número a partir do sequencial e do ano, sem tocar no banco.</summary>
    public static string Formatar(int sequencial, int ano)
        => $"{Prefixo}-{sequencial.ToString().PadLeft(DigitosSequencial, '0')}-{ano}";

    /// <summary>
    /// Ano local da data do relatório. Usa o fuso do servidor (mesmo critério do
    /// <c>ToLocalTime()</c> usado na montagem do laudo), para que um ensaio concluído
    /// às 21h de 31/12 não caia no ano seguinte por causa do UTC.
    /// </summary>
    private static int ObterAno(DateTime dataRelatorio)
    {
        var data = dataRelatorio.Kind == DateTimeKind.Utc
            ? dataRelatorio.ToLocalTime()
            : dataRelatorio;

        return data.Year;
    }

    private static async Task<int> ReservarSequencialAsync(
        DataMaisDbContext context,
        int ano,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "ContadoresRelatorio" ("Ano", "UltimoNumero")
            VALUES (@ano, 1)
            ON CONFLICT ("Ano")
            DO UPDATE SET "UltimoNumero" = "ContadoresRelatorio"."UltimoNumero" + 1
            RETURNING "UltimoNumero";
            """;

        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(ct);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            var parametro = command.CreateParameter();
            parametro.ParameterName = "ano";
            parametro.Value = ano;
            command.Parameters.Add(parametro);

            var resultado = await command.ExecuteScalarAsync(ct);

            if (resultado == null || resultado == DBNull.Value)
            {
                throw new InvalidOperationException(
                    $"Não foi possível reservar o sequencial do relatório para o ano {ano}.");
            }

            return Convert.ToInt32(resultado);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
