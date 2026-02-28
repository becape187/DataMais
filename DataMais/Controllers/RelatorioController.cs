using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using InfluxDB.Client;
using InfluxDB.Client.Core.Flux.Domain;
using System.Linq;

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
    public async Task<IActionResult> GetAll([FromQuery] int? clienteId = null, [FromQuery] int? cilindroId = null, [FromQuery] DateTime? dataInicio = null, [FromQuery] DateTime? dataFim = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
    {
        try
        {
            var query = _context.Relatorios
                .Include(r => r.Cliente)
                .Include(r => r.Cilindro)
                .Include(r => r.Ensaio)
                .AsQueryable();

            // Aplica filtros
            if (clienteId.HasValue)
            {
                query = query.Where(r => r.ClienteId == clienteId.Value);
            }

            if (cilindroId.HasValue)
            {
                query = query.Where(r => r.CilindroId == cilindroId.Value);
            }

            if (dataInicio.HasValue)
            {
                query = query.Where(r => r.Data >= dataInicio.Value);
            }

            if (dataFim.HasValue)
            {
                query = query.Where(r => r.Data <= dataFim.Value);
            }

            // Ordena por data
            query = query.OrderByDescending(r => r.Data);

            // Conta total antes da paginação
            var total = await query.CountAsync();

            // Aplica paginação
            var relatorios = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            return Ok(new
            {
                dados = result,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
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
                .Include(r => r.RespostasCampos)
                    .ThenInclude(resp => resp.CampoRelatorio)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            var ensaio = relatorio.Ensaio;

            // Busca campos ativos (não excluídos) para novos relatórios
            // Para relatórios antigos, busca todos os campos (incluindo excluídos) que têm resposta
            var camposAtivos = await _context.CamposRelatorio
                .Where(c => c.DataExclusao == null)
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            // Busca campos excluídos que têm resposta neste relatório (para relatórios antigos)
            var camposIdsComResposta = relatorio.RespostasCampos
                .Select(r => r.CampoRelatorioId)
                .Distinct()
                .ToList();

            var camposExcluidosComResposta = await _context.CamposRelatorio
                .Where(c => c.DataExclusao != null && camposIdsComResposta.Contains(c.Id))
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            // Combina campos ativos e excluídos (se tiverem resposta)
            var todosCampos = camposAtivos
                .Concat(camposExcluidosComResposta)
                .OrderBy(c => c.Ordem)
                .ToList();

            var camposComRespostas = todosCampos.Select(campo =>
            {
                var resposta = relatorio.RespostasCampos.FirstOrDefault(r => r.CampoRelatorioId == campo.Id);
                return new
                {
                    id = campo.Id,
                    nome = campo.Nome,
                    tipoResposta = campo.TipoResposta,
                    ordem = campo.Ordem,
                    respostaId = resposta?.Id,
                    valor = resposta?.Valor,
                    excluido = campo.DataExclusao != null
                };
            }).ToList();

            double? pressaoMin = null;
            double? pressaoMax = null;
            double? pressaoMedia = null;
            string? resultado = null;

            if (ensaio != null)
            {
                try
                {
                    (pressaoMin, pressaoMax, pressaoMedia) = await CalcularEstatisticasPressaoAsync(ensaio);
                    
                    // Calcula o resultado: APROVADO se pressão mínima >= 95% do setpoint, senão REPROVADO
                    if (ensaio.PressaoCargaConfigurada.HasValue && pressaoMin.HasValue)
                    {
                        var setpoint = (double)ensaio.PressaoCargaConfigurada.Value;
                        var limiteMinimo = setpoint * 0.95; // 95% do setpoint (5% de tolerância)
                        
                        if (pressaoMin.Value >= limiteMinimo)
                        {
                            resultado = "Aprovado";
                        }
                        else
                        {
                            resultado = "Reprovado";
                        }
                    }
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
                pressaoMedia = pressaoMedia,
                resultado = resultado,
                campos = camposComRespostas
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

        // Verifica se há setpoint configurado
        if (!ensaio.PressaoCargaConfigurada.HasValue || ensaio.PressaoCargaConfigurada.Value <= 0)
        {
            _logger.LogWarning("Ensaio {EnsaioId} não possui pressão de carga configurada. Não será possível calcular estatísticas.", ensaio.Id);
            return (null, null, null);
        }

        var setpoint = (double)ensaio.PressaoCargaConfigurada.Value;

        // Determina qual campo de pressão usar baseado na câmara testada
        // Se câmara A, usa pressaoA; se câmara B, usa pressaoB; se não especificado, tenta ambos
        string campoPressao = "pressaoA"; // padrão
        if (!string.IsNullOrWhiteSpace(ensaio.CamaraTestada))
        {
            var camara = ensaio.CamaraTestada.Trim().ToUpperInvariant();
            campoPressao = camara == "B" ? "pressaoB" : "pressaoA";
        }

        var from = (ensaio.DataInicio ?? ensaio.DataCriacao).ToUniversalTime().AddMinutes(-1);
        var to = (ensaio.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

        // Busca todos os dados de pressão ordenados por tempo
        var flux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and r._field == ""{campoPressao}"")
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value""])";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var queryApi = influxClient.GetQueryApi();

        try
        {
            var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);
            
            // Primeiro, coleta todos os dados ordenados por tempo
            var todosDados = new List<(DateTime time, double pressao)>();
            
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    var value = record.GetValue();
                    var time = record.GetTime();
                    if (value != null && value is IConvertible && time != null)
                    {
                        try
                        {
                            var pressao = Convert.ToDouble(value);
                            var dateTime = time.Value.ToDateTimeUtc();
                            todosDados.Add((dateTime, pressao));
                        }
                        catch { }
                    }
                }
            }

            // Ordena por tempo para garantir ordem cronológica
            todosDados = todosDados.OrderBy(d => d.time).ToList();

            // Encontra o primeiro ponto onde a pressão >= setpoint
            int inicioAnalise = -1;
            for (int i = 0; i < todosDados.Count; i++)
            {
                if (todosDados[i].pressao >= setpoint)
                {
                    inicioAnalise = i;
                    break; // Encontrou o primeiro ponto que alcança o setpoint
                }
            }

            // Se nunca alcançou o setpoint, retorna null
            if (inicioAnalise == -1)
            {
                _logger.LogWarning("Nenhum dado de pressão >= setpoint ({Setpoint}) encontrado para o ensaio {EnsaioId}.", setpoint, ensaio.Id);
                return (null, null, null);
            }

            // A partir do momento que alcançou o setpoint, coleta TODOS os valores
            // (mesmo que depois caia abaixo do setpoint)
            var valoresFiltrados = new List<double>();
            for (int i = inicioAnalise; i < todosDados.Count; i++)
            {
                valoresFiltrados.Add(todosDados[i].pressao);
            }

            if (valoresFiltrados.Count == 0)
            {
                return (null, null, null);
            }

            var min = valoresFiltrados.Min();
            var max = valoresFiltrados.Max();
            var avg = valoresFiltrados.Average();

            return (min, max, avg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular estatísticas de pressão para o ensaio {EnsaioId}", ensaio.Id);
            return (null, null, null);
        }
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

            // Busca ambas as pressões A e B
            var fluxA = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and r._field == ""pressaoA"")
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value""])";

            var fluxB = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and r._field == ""pressaoB"")
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value""])";

            using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
            var queryApi = influxClient.GetQueryApi();

            var dados = new Dictionary<string, Dictionary<string, double>>();

            try
            {
                // Busca pressão A
                var tablesA = await queryApi.QueryAsync(fluxA, appConfig.Influx.Organization);
                foreach (var table in tablesA)
                {
                    foreach (var record in table.Records)
                    {
                        var time = record.GetTime();
                        var value = record.GetValue();

                        if (time != null && value != null)
                        {
                            var dateTime = time.Value.ToDateTimeUtc().ToLocalTime();
                            var timeStr = dateTime.ToString("HH:mm:ss");
                            
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

                            if (!dados.ContainsKey(timeStr))
                            {
                                dados[timeStr] = new Dictionary<string, double>();
                            }
                            dados[timeStr]["pressaoA"] = Math.Round(pressao, 2);
                        }
                    }
                }

                // Busca pressão B
                var tablesB = await queryApi.QueryAsync(fluxB, appConfig.Influx.Organization);
                foreach (var table in tablesB)
                {
                    foreach (var record in table.Records)
                    {
                        var time = record.GetTime();
                        var value = record.GetValue();

                        if (time != null && value != null)
                        {
                            var dateTime = time.Value.ToDateTimeUtc().ToLocalTime();
                            var timeStr = dateTime.ToString("HH:mm:ss");
                            
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

                            if (!dados.ContainsKey(timeStr))
                            {
                                dados[timeStr] = new Dictionary<string, double>();
                            }
                            dados[timeStr]["pressaoB"] = Math.Round(pressao, 2);
                        }
                    }
                }

                // Converte o dicionário para lista de objetos
                var dadosLista = dados.Select(kvp => new
                {
                    time = kvp.Key,
                    pressaoA = kvp.Value.ContainsKey("pressaoA") ? (double?)kvp.Value["pressaoA"] : null,
                    pressaoB = kvp.Value.ContainsKey("pressaoB") ? (double?)kvp.Value["pressaoB"] : null
                }).OrderBy(d => d.time).ToList();

                return Ok(new { dados = dadosLista });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar dados do InfluxDB para o gráfico do relatório {RelatorioId}", id);
                return StatusCode(500, new { message = "Erro ao buscar dados do gráfico", error = ex.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados do gráfico do relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao obter dados do gráfico", error = ex.Message });
        }
    }

    /// <summary>
    /// Salva ou atualiza as respostas dos campos do relatório.
    /// </summary>
    [HttpPost("{id:int}/respostas-campos")]
    public async Task<IActionResult> SalvarRespostasCampos(int id, [FromBody] SalvarRespostasCamposRequest request)
    {
        try
        {
            var relatorio = await _context.Relatorios.FindAsync(id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            if (request.Respostas == null || request.Respostas.Count == 0)
            {
                return BadRequest(new { message = "Lista de respostas é obrigatória" });
            }

            // Valida se todos os campos existem
            var camposIds = request.Respostas.Select(r => r.CampoRelatorioId).Distinct().ToList();
            var camposExistentes = await _context.CamposRelatorio
                .Where(c => camposIds.Contains(c.Id))
                .ToListAsync();

            if (camposExistentes.Count != camposIds.Count)
            {
                return BadRequest(new { message = "Um ou mais campos não foram encontrados" });
            }

            foreach (var respostaRequest in request.Respostas)
            {
                var campo = camposExistentes.FirstOrDefault(c => c.Id == respostaRequest.CampoRelatorioId);
                if (campo == null) continue;

                // Valida o valor conforme o tipo
                if (campo.TipoResposta == "SimOuNao")
                {
                    if (respostaRequest.Valor != "Sim" && respostaRequest.Valor != "Não" && !string.IsNullOrWhiteSpace(respostaRequest.Valor))
                    {
                        return BadRequest(new { message = $"Campo '{campo.Nome}' deve ter valor 'Sim' ou 'Não'" });
                    }
                }

                // Busca resposta existente
                var respostaExistente = await _context.RespostasCampoRelatorio
                    .FirstOrDefaultAsync(r => r.RelatorioId == id && r.CampoRelatorioId == respostaRequest.CampoRelatorioId);

                if (respostaExistente != null)
                {
                    // Atualiza resposta existente
                    respostaExistente.Valor = string.IsNullOrWhiteSpace(respostaRequest.Valor) ? null : respostaRequest.Valor.Trim();
                    respostaExistente.DataAtualizacao = DateTime.UtcNow;
                }
                else
                {
                    // Cria nova resposta
                    var novaResposta = new RespostaCampoRelatorio
                    {
                        RelatorioId = id,
                        CampoRelatorioId = respostaRequest.CampoRelatorioId,
                        Valor = string.IsNullOrWhiteSpace(respostaRequest.Valor) ? null : respostaRequest.Valor.Trim(),
                        DataCriacao = DateTime.UtcNow
                    };
                    _context.RespostasCampoRelatorio.Add(novaResposta);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Respostas salvas com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar respostas dos campos do relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao salvar respostas", error = ex.Message });
        }
    }
}

// DTOs
public class SalvarRespostasCamposRequest
{
    public List<RespostaCampoRequest> Respostas { get; set; } = new();
}

public class RespostaCampoRequest
{
    public int CampoRelatorioId { get; set; }
    public string? Valor { get; set; }
}

