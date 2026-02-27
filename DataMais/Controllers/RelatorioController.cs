using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using InfluxDB.Client;
using InfluxDB.Client.Core.Flux.Domain;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RelatorioController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<RelatorioController> _logger;
    private readonly ConfigService _configService;

    public RelatorioController(DataMaisDbContext context, ILogger<RelatorioController> logger, ConfigService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Lista todos os relatórios ordenados por data (mais recentes primeiro).
    /// Usado pela tela principal de Relatórios.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var relatorios = await _context.Relatorios
                .Include(r => r.Cliente)
                .Include(r => r.Cilindro)
                .Include(r => r.Ensaio)
                .OrderByDescending(r => r.Data)
                .ToListAsync();

            var result = relatorios.Select(r => new
            {
                id = r.Id,
                numero = r.Numero,
                data = r.Data,
                clienteId = r.ClienteId,
                clienteNome = r.Cliente?.Nome ?? string.Empty,
                cilindroId = r.CilindroId,
                cilindroNome = r.Cilindro != null ? r.Cilindro.Nome : string.Empty,
                ensaioId = r.EnsaioId,
                ensaioNumero = r.Ensaio != null ? r.Ensaio.Numero : null
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar relatórios");
            return StatusCode(500, new { message = "Erro ao listar relatórios" });
        }
    }

    /// <summary>
    /// Retorna os últimos relatórios (para o dashboard).
    /// </summary>
    [HttpGet("ultimos")]
    public async Task<IActionResult> GetUltimos([FromQuery] int top = 5)
    {
        try
        {
            if (top <= 0) top = 1;
            if (top > 50) top = 50;

            var relatorios = await _context.Relatorios
                .Include(r => r.Cliente)
                .Include(r => r.Cilindro)
                .Include(r => r.Ensaio)
                .OrderByDescending(r => r.Data)
                .Take(top)
                .ToListAsync();

            var result = relatorios.Select(r => new
            {
                id = r.Id,
                numero = r.Numero,
                data = r.Data,
                clienteId = r.ClienteId,
                clienteNome = r.Cliente?.Nome ?? string.Empty,
                cilindroId = r.CilindroId,
                cilindroNome = r.Cilindro != null ? r.Cilindro.Nome : string.Empty,
                ensaioId = r.EnsaioId,
                ensaioNumero = r.Ensaio != null ? r.Ensaio.Numero : null
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar últimos relatórios");
            return StatusCode(500, new { message = "Erro ao listar últimos relatórios" });
        }
    }

    /// <summary>
    /// Detalhe de um relatório específico (para tela VisualizarRelatorio).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var relatorio = await _context.Relatorios
                .Include(r => r.Cliente)
                .Include(r => r.Cilindro)
                .Include(r => r.Ensaio)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            var ensaio = relatorio.Ensaio;

            double? pressaoMin = null;
            double? pressaoMax = null;
            double? pressaoMedia = null;

            if (ensaio != null)
            {
                try
                {
                    (pressaoMin, pressaoMax, pressaoMedia) = await CalcularEstatisticasPressaoAsync(ensaio);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao calcular estatísticas de pressão para o ensaio {EnsaioId}", ensaio.Id);
                }
            }

            var result = new
            {
                id = relatorio.Id,
                numero = relatorio.Numero,
                data = relatorio.Data,
                observacoes = relatorio.Observacoes,
                clienteId = relatorio.ClienteId,
                clienteNome = relatorio.Cliente?.Nome ?? string.Empty,
                cilindroId = relatorio.CilindroId,
                cilindroNome = relatorio.Cilindro != null ? relatorio.Cilindro.Nome : string.Empty,
                ensaioId = relatorio.EnsaioId,
                ensaioNumero = ensaio != null ? ensaio.Numero : null,
                ensaioDataInicio = ensaio?.DataInicio,
                ensaioDataFim = ensaio?.DataFim,
                camaraTestada = ensaio?.CamaraTestada,
                pressaoCargaConfigurada = ensaio?.PressaoCargaConfigurada,
                tempoCargaConfigurado = ensaio?.TempoCargaConfigurado,
                pressaoMinima = pressaoMin,
                pressaoMaxima = pressaoMax,
                pressaoMedia = pressaoMedia
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao obter relatório", error = ex.Message });
        }
    }

    private async Task<(double? min, double? max, double? avg)> CalcularEstatisticasPressaoAsync(Ensaio ensaio)
    {
        var appConfig = _configService.GetConfig();

        if (string.IsNullOrWhiteSpace(appConfig.Influx.Url) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Token) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Organization) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Bucket))
        {
            _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível calcular estatísticas para o ensaio {EnsaioId}.", ensaio.Id);
            return (null, null, null);
        }

        var from = (ensaio.DataInicio ?? ensaio.DataCriacao).ToUniversalTime().AddMinutes(-1);
        var to = (ensaio.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

        var baseFlux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and r._field == ""pressao"")";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var queryApi = influxClient.GetQueryApi();

        async Task<double?> ExecQueryAsync(string flux)
        {
            var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);
            var record = tables.FirstOrDefault()?.Records.FirstOrDefault();
            if (record == null)
                return null;

            var value = record.GetValue();
            if (value is IConvertible)
            {
                try
                {
                    return Convert.ToDouble(value);
                }
                catch { }
            }
            return null;
        }

        var minFlux = baseFlux + " |> group() |> min()";
        var maxFlux = baseFlux + " |> group() |> max()";
        var meanFlux = baseFlux + " |> group() |> mean()";

        var min = await ExecQueryAsync(minFlux);
        var max = await ExecQueryAsync(maxFlux);
        var avg = await ExecQueryAsync(meanFlux);

        return (min, max, avg);
    }

    /// <summary>
    /// Busca os dados de pressão do InfluxDB para o gráfico do relatório.
    /// Retorna os dados de pressão do período do ensaio associado ao relatório.
    /// </summary>
    [HttpGet("{id:int}/dados-grafico")]
    public async Task<IActionResult> GetDadosGrafico(int id)
    {
        try
        {
            var relatorio = await _context.Relatorios
                .Include(r => r.Ensaio)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            var ensaio = relatorio.Ensaio;
            if (ensaio == null)
            {
                return Ok(new { dados = Array.Empty<object>() });
            }

            var appConfig = _configService.GetConfig();

            if (string.IsNullOrWhiteSpace(appConfig.Influx.Url) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Token) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Organization) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Bucket))
            {
                _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível buscar dados do gráfico para o relatório {RelatorioId}.", id);
                return Ok(new { dados = Array.Empty<object>() });
            }

            var from = (ensaio.DataInicio ?? ensaio.DataCriacao).ToUniversalTime().AddMinutes(-1);
            var to = (ensaio.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

            var flux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and r._field == ""pressao"")
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value""])";

            using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
            var queryApi = influxClient.GetQueryApi();

            var dados = new List<object>();

            try
            {
                var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);

                foreach (var table in tables)
                {
                    foreach (var record in table.Records)
                    {
                        var time = record.GetTime();
                        var value = record.GetValue();

                        if (time != null && value != null)
                        {
                            // Converte o timestamp para formato legível (HH:mm:ss)
                            var timeStr = time.Value.ToLocalTime().ToString("HH:mm:ss");
                            
                            double pressao = 0;
                            if (value is IConvertible)
                            {
                                try
                                {
                                    pressao = Convert.ToDouble(value);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                continue;
                            }

                            dados.Add(new
                            {
                                time = timeStr,
                                pressao = Math.Round(pressao, 2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar dados do InfluxDB para o gráfico do relatório {RelatorioId}", id);
                return StatusCode(500, new { message = "Erro ao buscar dados do gráfico", error = ex.Message });
            }

            return Ok(new { dados });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados do gráfico do relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao obter dados do gráfico", error = ex.Message });
        }
    }
}

