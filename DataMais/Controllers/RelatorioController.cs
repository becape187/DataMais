using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using DataMais.Configuration;
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
                    .ThenInclude(e => e!.Etapas)
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
                    secao = campo.Secao,
                    reprovaSeSim = campo.ReprovaSeSim,
                    ordem = campo.Ordem,
                    respostaId = resposta?.Id,
                    valor = resposta?.Valor,
                    excluido = campo.DataExclusao != null
                };
            }).ToList();

            // Uma análise por câmara: cada etapa é avaliada na SUA janela de tempo,
            // pressurizando a própria câmara e medindo a passagem para a oposta.
            var analises = ensaio != null
                ? await AnalisarEtapasAsync(ensaio, relatorio.Cilindro)
                : new List<AnaliseEtapa>();

            var resultado = CombinarResultados(analises);

            // Campos de câmara única, mantidos para os laudos anteriores ao modelo de
            // duas câmaras (que têm exatamente uma etapa). Código novo lê de "etapas".
            var primeira = analises.FirstOrDefault();

            // Override rev02: qualquer checklist marcado como "reprova se Sim" e respondido "Sim"
            // força o veredito para Reprovado (ex.: "Vazamentos visíveis" na inspeção visual),
            // sobrepondo o veredito automático por passagem entre câmaras.
            var reprovadoPorChecklist = todosCampos.Any(campo =>
                campo.ReprovaSeSim &&
                relatorio.RespostasCampos.Any(r =>
                    r.CampoRelatorioId == campo.Id &&
                    string.Equals(r.Valor?.Trim(), "Sim", StringComparison.OrdinalIgnoreCase)));

            if (reprovadoPorChecklist)
            {
                resultado = "Reprovado";
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
                // Análise por câmara — é isto que o laudo apresenta
                etapas = analises.Select(a => new
                {
                    etapaId = a.EtapaId,
                    camara = a.Camara,
                    tentativa = a.Tentativa,
                    dataInicio = a.DataInicio,
                    dataFim = a.DataFim,
                    pressaoCargaConfigurada = a.PressaoCargaConfigurada,
                    tempoCargaConfigurado = a.TempoCargaConfigurado,
                    pressaoMinima = a.PressaoMinima,
                    pressaoMaxima = a.PressaoMaxima,
                    pressaoMedia = a.PressaoMedia,
                    pressaoMaximaCamaraOposta = a.PressaoMaximaCamaraOposta,
                    limitePassagem = a.LimitePassagem,
                    resultado = a.Resultado
                }),
                // Compatibilidade com laudos de câmara única (uma etapa só)
                camaraTestada = primeira?.Camara,
                pressaoCargaConfigurada = primeira?.PressaoCargaConfigurada,
                tempoCargaConfigurado = primeira?.TempoCargaConfigurado,
                pressaoMinima = primeira?.PressaoMinima,
                pressaoMaxima = primeira?.PressaoMaxima,
                pressaoMedia = primeira?.PressaoMedia,
                pressaoMaximaCamaraOposta = primeira?.PressaoMaximaCamaraOposta,
                limitePassagem = primeira?.LimitePassagem,
                resultado = resultado,
                situacao = relatorio.Situacao,
                versao = relatorio.Versao,
                concluidoPorNome = relatorio.ConcluidoPorNome,
                dataConclusao = relatorio.DataConclusao,
                // Identificação do documento (Ensaio) e do equipamento (Cilindro) — relatório rev02
                ensaioVessel = ensaio?.Vessel,
                ensaioLocalTeste = ensaio?.LocalTeste,
                ensaioDepartamento = ensaio?.Departamento,
                ensaioOrdemServico = ensaio?.OrdemServico,
                cilindroCodigoSap = relatorio.Cilindro?.CodigoInterno,
                cilindroTagId = relatorio.Cilindro?.CodigoCliente,
                cilindroFabricante = relatorio.Cilindro?.Fabricante,
                cilindroNumeroSerie = relatorio.Cilindro?.NumeroSerie,
                cilindroPartNumber = relatorio.Cilindro?.PartNumber,
                cilindroFluido = relatorio.Cilindro?.FluidoUtilizado,
                cilindroDiametroInterno = relatorio.Cilindro?.DiametroInterno,
                cilindroDiametroHaste = relatorio.Cilindro?.DiametroHaste,
                cilindroMaximaPressaoA = relatorio.Cilindro?.MaximaPressaoA,
                cilindroMaximaPressaoB = relatorio.Cilindro?.MaximaPressaoB,
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

    /// <summary>Resultado da avaliação de uma câmara do ensaio.</summary>
    private sealed record AnaliseEtapa(
        int EtapaId,
        string Camara,
        int Tentativa,
        DateTime DataInicio,
        DateTime? DataFim,
        DateTime? DataInicioContagem,
        DateTime? DataFimContagem,
        decimal PressaoCargaConfigurada,
        decimal TempoCargaConfigurado,
        double? PressaoMinima,
        double? PressaoMaxima,
        double? PressaoMedia,
        double? PressaoMaximaCamaraOposta,
        double? LimitePassagem,
        string? Resultado);

    /// <summary>
    /// Etapas que valem para o laudo: a ÚLTIMA tentativa concluída de cada câmara.
    /// As tentativas anteriores ficam no banco como histórico (Status=Repetida),
    /// mas não entram no documento.
    /// </summary>
    private static List<EnsaioEtapa> EtapasValidas(Ensaio ensaio) =>
        ensaio.Etapas
            .Where(e => e.Status == StatusEtapa.Concluida)
            .GroupBy(e => e.Camara)
            .Select(g => g.OrderByDescending(e => e.Tentativa).First())
            .OrderBy(e => e.Camara)
            .ToList();

    /// <summary>
    /// Veredito do ensaio a partir das câmaras: basta UMA reprovar para reprovar tudo.
    /// Se nenhuma reprovou mas alguma não pôde ser avaliada, o ensaio fica sem veredito.
    /// </summary>
    private static string? CombinarResultados(IEnumerable<AnaliseEtapa> analises)
    {
        var lista = analises.ToList();

        if (lista.Count == 0) return null;
        if (lista.Any(a => a.Resultado == "Reprovado")) return "Reprovado";
        if (lista.Any(a => a.Resultado == null)) return null;

        return "Aprovado";
    }

    private async Task<List<AnaliseEtapa>> AnalisarEtapasAsync(Ensaio ensaio, Cilindro? cilindro)
    {
        var etapas = EtapasValidas(ensaio);
        var analises = new List<AnaliseEtapa>();

        if (etapas.Count == 0)
        {
            return analises;
        }

        var appConfig = _configService.GetConfig();

        if (!InfluxConfigurado(appConfig))
        {
            _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível avaliar o ensaio {EnsaioId}.", ensaio.Id);
            return etapas.Select(e => SemAnalise(e)).ToList();
        }

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var queryApi = influxClient.GetQueryApi();

        foreach (var etapa in etapas)
        {
            try
            {
                analises.Add(await AnalisarEtapaAsync(queryApi, appConfig, etapa, cilindro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao avaliar a câmara {Camara} do ensaio {EnsaioId}", etapa.Camara, ensaio.Id);
                analises.Add(SemAnalise(etapa));
            }
        }

        return analises;
    }

    /// <summary>
    /// Avalia uma câmara na sua janela de tempo. A partir do instante em que a câmara
    /// pressurizada atinge o setpoint, mede-se o PICO da câmara OPOSTA: se ultrapassar
    /// o limite do cilindro, o cilindro está dando passagem → Reprovado.
    /// As estatísticas da própria câmara são informativas — a pressão dela cai por
    /// vários motivos, então não servem como critério de aprovação.
    /// </summary>
    private async Task<AnaliseEtapa> AnalisarEtapaAsync(
        QueryApi queryApi, AppConfig appConfig, EnsaioEtapa etapa, Cilindro? cilindro)
    {
        if (etapa.PressaoCargaConfigurada <= 0)
        {
            _logger.LogWarning("Etapa {EtapaId} (câmara {Camara}) não tem pressão de carga configurada.", etapa.Id, etapa.Camara);
            return SemAnalise(etapa);
        }

        var setpoint = (double)etapa.PressaoCargaConfigurada;
        var camara = etapa.Camara.Trim().ToUpperInvariant();
        var campoTestada = camara == "B" ? "pressaoB" : "pressaoA";
        var campoOposta = camara == "B" ? "pressaoA" : "pressaoB";

        var ensaioId = etapa.EnsaioId.ToString();

        // Janela de contagem sinalizada pelo CLP (INICIA_CONTAGEM). Quando existe, ela
        // é a fonte da verdade do patamar de teste: recorta exato, sem margem, para não
        // pegar a rampa nem vazar para a etapa vizinha.
        var contagemInicio = etapa.DataInicioContagem?.ToUniversalTime();
        var contagemFim = (etapa.DataFimContagem ?? etapa.DataFim)?.ToUniversalTime();

        // Janela invertida (t0 depois do fim — dado corrompido por corrida antiga entre
        // o monitor e o encerramento manual): descarta e cai na regra do setpoint, em
        // vez de mandar um range start > stop para o Flux e deixar a câmara sem veredito.
        if (contagemInicio.HasValue && contagemFim.HasValue && contagemInicio.Value >= contagemFim.Value)
        {
            _logger.LogWarning(
                "Etapa {EtapaId}: janela de contagem invertida ({Inicio:o} >= {Fim:o}) — ignorando e usando a regra do setpoint.",
                etapa.Id, contagemInicio.Value, contagemFim.Value);
            contagemInicio = null;
            contagemFim = null;
        }

        var from = contagemInicio ?? etapa.DataInicio.ToUniversalTime().AddMinutes(-1);
        var to = contagemInicio.HasValue
            ? (contagemFim ?? DateTime.UtcNow)
            : (etapa.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

        var serieTestada = await FetchSeriePressaoAsync(queryApi, appConfig, ensaioId, campoTestada, from, to);

        DateTime t0;

        if (contagemInicio.HasValue)
        {
            // O CLP disse quando a contagem começou — é esse o t0.
            t0 = contagemInicio.Value;
        }
        else
        {
            // Ensaios anteriores ao INICIA_CONTAGEM: t0 é o primeiro ponto em que a
            // câmara testada alcança o setpoint.
            var indiceT0 = serieTestada.FindIndex(p => p.pressao >= setpoint);
            if (indiceT0 < 0)
            {
                _logger.LogWarning("Câmara {Camara} nunca atingiu o setpoint ({Setpoint}) na etapa {EtapaId}.",
                    camara, setpoint, etapa.Id);
                return SemAnalise(etapa);
            }

            t0 = serieTestada[indiceT0].time;
        }

        var valoresTestada = serieTestada.Where(p => p.time >= t0).Select(p => p.pressao).ToList();

        if (valoresTestada.Count == 0)
        {
            _logger.LogWarning("Nenhuma leitura da câmara {Camara} na janela de contagem da etapa {EtapaId}.",
                camara, etapa.Id);
            return SemAnalise(etapa);
        }

        double? min = valoresTestada.Count > 0 ? valoresTestada.Min() : null;
        double? max = valoresTestada.Count > 0 ? valoresTestada.Max() : null;
        double? avg = valoresTestada.Count > 0 ? valoresTestada.Average() : null;

        var serieOposta = await FetchSeriePressaoAsync(queryApi, appConfig, ensaioId, campoOposta, from, to);
        var valoresOposta = serieOposta.Where(p => p.time >= t0).Select(p => p.pressao).ToList();

        double? maxOposta = valoresOposta.Count > 0 ? valoresOposta.Max() : null;
        var limite = ObterLimitePassagem(cilindro, camara);

        string? resultado = maxOposta.HasValue
            ? (maxOposta.Value > limite ? "Reprovado" : "Aprovado")
            : null;

        return new AnaliseEtapa(
            etapa.Id, camara, etapa.Tentativa, etapa.DataInicio, etapa.DataFim,
            etapa.DataInicioContagem, etapa.DataFimContagem,
            etapa.PressaoCargaConfigurada, etapa.TempoCargaConfigurado,
            min, max, avg, maxOposta, limite, resultado);
    }

    private static AnaliseEtapa SemAnalise(EnsaioEtapa etapa) => new(
        etapa.Id, etapa.Camara, etapa.Tentativa, etapa.DataInicio, etapa.DataFim,
        etapa.DataInicioContagem, etapa.DataFimContagem,
        etapa.PressaoCargaConfigurada, etapa.TempoCargaConfigurado,
        null, null, null, null, null, null);

    private static bool InfluxConfigurado(AppConfig appConfig) =>
        !string.IsNullOrWhiteSpace(appConfig.Influx.Url) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Token) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Organization) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Bucket);

    /// <summary>
    /// Limite de pressão (bar) admitido na câmara OPOSTA durante o ensaio.
    /// Vem do cilindro conforme a câmara testada; se não configurado, assume 1 bar.
    /// </summary>
    private static double ObterLimitePassagem(Cilindro? cilindro, string? camaraTestada)
    {
        const double padrao = 1.0; // 1 bar: qualquer coisa acima já indica passagem

        if (cilindro == null)
            return padrao;

        var camara = camaraTestada?.Trim().ToUpperInvariant();
        var limite = camara == "B" ? cilindro.LimitePassagemCamaraB : cilindro.LimitePassagemCamaraA;
        return limite.HasValue ? (double)limite.Value : padrao;
    }

    /// <summary>
    /// Busca uma série temporal de pressão (campo A ou B) do InfluxDB para um ensaio,
    /// ordenada cronologicamente.
    /// </summary>
    private async Task<List<(DateTime time, double pressao)>> FetchSeriePressaoAsync(
        QueryApi queryApi, AppConfig appConfig, string ensaioId, string campo, DateTime from, DateTime to)
    {
        var flux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaioId}"" and r._field == ""{campo}"")
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value""])";

        var dados = new List<(DateTime time, double pressao)>();
        var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                var value = record.GetValue();
                var time = record.GetTime();
                if (value is IConvertible && time != null)
                {
                    try
                    {
                        dados.Add((time.Value.ToDateTimeUtc(), Convert.ToDouble(value)));
                    }
                    catch { }
                }
            }
        }

        return dados.OrderBy(d => d.time).ToList();
    }

    /// <summary>
    /// Séries de pressão do laudo, UMA POR CÂMARA — cada etapa recorta a sua janela
    /// de tempo no InfluxDB. Cada série traz pressaoA e pressaoB juntas, porque é o
    /// par que mostra a passagem entre as câmaras.
    /// </summary>
    [HttpGet("{id:int}/dados-grafico")]
    public async Task<IActionResult> GetDadosGrafico(int id)
    {
        try
        {
            var relatorio = await _context.Relatorios
                .Include(r => r.Ensaio)
                    .ThenInclude(e => e!.Etapas)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            var ensaio = relatorio.Ensaio;
            if (ensaio == null)
            {
                return Ok(new { etapas = Array.Empty<object>() });
            }

            var appConfig = _configService.GetConfig();

            if (!InfluxConfigurado(appConfig))
            {
                _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível buscar dados do gráfico para o relatório {RelatorioId}.", id);
                return Ok(new { etapas = Array.Empty<object>() });
            }

            using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
            var queryApi = influxClient.GetQueryApi();

            var series = new List<object>();

            foreach (var etapa in EtapasValidas(ensaio))
            {
                var from = etapa.DataInicio.ToUniversalTime().AddMinutes(-1);
                var to = (etapa.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);
                var ensaioId = etapa.EnsaioId.ToString();

                var serieA = await FetchSeriePressaoAsync(queryApi, appConfig, ensaioId, "pressaoA", from, to);
                var serieB = await FetchSeriePressaoAsync(queryApi, appConfig, ensaioId, "pressaoB", from, to);

                // Combina A e B pelo instante da leitura (são gravadas no mesmo ponto)
                var porInstante = new SortedDictionary<DateTime, (double? a, double? b)>();

                foreach (var (time, pressao) in serieA)
                {
                    porInstante[time] = (Math.Round(pressao, 2), porInstante.TryGetValue(time, out var atual) ? atual.b : null);
                }

                foreach (var (time, pressao) in serieB)
                {
                    var atual = porInstante.TryGetValue(time, out var v) ? v : (null, null);
                    porInstante[time] = (atual.a, Math.Round(pressao, 2));
                }

                series.Add(new
                {
                    etapaId = etapa.Id,
                    camara = etapa.Camara,
                    tentativa = etapa.Tentativa,
                    dados = porInstante.Select(kv => new
                    {
                        time = kv.Key.ToLocalTime().ToString("HH:mm:ss"),
                        pressaoA = kv.Value.a,
                        pressaoB = kv.Value.b
                    }).ToList()
                });
            }

            return Ok(new { etapas = series });
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
    [Authorize(Roles = "Admin,Operador")]
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

            // Editar um relatório JÁ CONCLUÍDO invalida a conclusão: reabre como Rascunho
            // e registra a reabertura no histórico (com o usuário logado). O PDF volta a ficar
            // bloqueado até nova conclusão.
            if (relatorio.Situacao == "Concluido")
            {
                var (uid, unome) = UsuarioAtual();
                relatorio.Situacao = "Rascunho";
                relatorio.DataAtualizacao = DateTime.UtcNow;
                _context.RelatorioVersoes.Add(new RelatorioVersao
                {
                    RelatorioId = relatorio.Id,
                    NumeroVersao = relatorio.Versao,
                    Acao = "Reaberto",
                    UsuarioId = uid,
                    UsuarioNome = unome,
                    DataHora = DateTime.UtcNow,
                    Observacoes = relatorio.Observacoes
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Respostas salvas com sucesso", situacao = relatorio.Situacao });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar respostas dos campos do relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao salvar respostas", error = ex.Message });
        }
    }

    /// <summary>
    /// Conclui (assina) o relatório: fixa Situacao=Concluido, incrementa a versão,
    /// registra o assinante (usuário logado) e grava um snapshot no histórico. Libera o PDF.
    /// </summary>
    [HttpPost("{id:int}/concluir")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> Concluir(int id)
    {
        try
        {
            var relatorio = await _context.Relatorios
                .Include(r => r.RespostasCampos)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            var (uid, unome) = UsuarioAtual();
            var agora = DateTime.UtcNow;

            var resultado = await AvaliarResultadoAsync(relatorio);
            var respostasJson = SerializarRespostas(relatorio);

            relatorio.Versao += 1;
            relatorio.Situacao = "Concluido";
            relatorio.ConcluidoPorUsuarioId = uid;
            relatorio.ConcluidoPorNome = unome;
            relatorio.DataConclusao = agora;
            relatorio.DataAtualizacao = agora;

            _context.RelatorioVersoes.Add(new RelatorioVersao
            {
                RelatorioId = relatorio.Id,
                NumeroVersao = relatorio.Versao,
                Acao = "Concluido",
                UsuarioId = uid,
                UsuarioNome = unome,
                DataHora = agora,
                Resultado = resultado,
                Observacoes = relatorio.Observacoes,
                RespostasJson = respostasJson
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Relatório concluído com sucesso",
                situacao = relatorio.Situacao,
                versao = relatorio.Versao,
                concluidoPorNome = relatorio.ConcluidoPorNome,
                dataConclusao = relatorio.DataConclusao,
                resultado
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao concluir relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao concluir relatório", error = ex.Message });
        }
    }

    /// <summary>
    /// Exclui um laudo em RASCUNHO e descarta o ensaio que o gerou: o ensaio vai para
    /// Cancelado e as leituras dele saem do InfluxDB. Laudo já concluído (assinado) é
    /// documento emitido e não pode ser excluído.
    /// O número REH-MPR consumido NÃO volta para o contador — a sequência fica com um
    /// buraco, e é assim de propósito: número emitido não é reaproveitado.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            var relatorio = await _context.Relatorios
                .Include(r => r.Ensaio)
                    .ThenInclude(e => e!.Etapas)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (relatorio == null)
            {
                return NotFound(new { message = "Relatório não encontrado" });
            }

            if (relatorio.Situacao == "Concluido")
            {
                return Conflict(new
                {
                    message = $"O laudo {relatorio.Numero} está concluído e assinado — não pode ser excluído."
                });
            }

            var numero = relatorio.Numero;
            var ensaio = relatorio.Ensaio;
            var agora = DateTime.UtcNow;

            // As respostas dos campos e o histórico de versões saem por cascade
            // (ver DataMaisDbContext.OnModelCreating).
            _context.Relatorios.Remove(relatorio);

            if (ensaio != null)
            {
                foreach (var etapa in ensaio.Etapas.Where(e => e.Status == StatusEtapa.EmExecucao))
                {
                    etapa.Status = StatusEtapa.Descartada;
                    etapa.DataFim = agora;
                    etapa.DataAtualizacao = agora;
                }

                ensaio.Status = StatusEnsaio.Cancelado;
                ensaio.DataFim ??= agora;
                ensaio.DataAtualizacao = agora;
            }

            await _context.SaveChangesAsync();

            if (ensaio != null)
            {
                try
                {
                    await RemoverLeiturasInfluxAsync(ensaio.Id, ensaio.DataInicio ?? ensaio.DataCriacao, agora);
                }
                catch (Exception ex)
                {
                    // O laudo já saiu do banco: falhar aqui deixa lixo no Influx, não inconsistência.
                    _logger.LogError(ex, "Erro ao remover leituras do ensaio {EnsaioId} após excluir o laudo {Numero}",
                        ensaio.Id, numero);
                }
            }

            _logger.LogWarning("Laudo {Numero} (id {RelatorioId}) excluído; ensaio {EnsaioId} cancelado.",
                numero, id, ensaio?.Id);

            return Ok(new
            {
                message = $"Laudo {numero} excluído e ensaio descartado.",
                numero
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao excluir relatório", error = ex.Message });
        }
    }

    /// <summary>Remove as leituras do ensaio no InfluxDB (janela inteira do ensaio).</summary>
    private async Task RemoverLeiturasInfluxAsync(int ensaioId, DateTime de, DateTime ate)
    {
        var appConfig = _configService.GetConfig();

        if (!InfluxConfigurado(appConfig))
        {
            _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível remover leituras do ensaio {EnsaioId}.", ensaioId);
            return;
        }

        var from = de.ToUniversalTime().AddMinutes(-1);
        var to = ate.ToUniversalTime().AddMinutes(1);
        var predicate = $"_measurement=\"ensaio_pressao\" AND ensaioId=\"{ensaioId}\"";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var deleteApi = influxClient.GetDeleteApi();

        await deleteApi.Delete(from, to, predicate, appConfig.Influx.Bucket, appConfig.Influx.Organization);

        _logger.LogInformation("Leituras do ensaio {EnsaioId} removidas do InfluxDB no intervalo {From} - {To}",
            ensaioId, from, to);
    }

    /// <summary>Histórico de versões (conclusões/reaberturas) do relatório.</summary>
    [HttpGet("{id:int}/versoes")]
    public async Task<IActionResult> GetVersoes(int id)
    {
        try
        {
            var versoes = await _context.RelatorioVersoes
                .Where(v => v.RelatorioId == id)
                .OrderByDescending(v => v.DataHora)
                .Select(v => new
                {
                    v.Id,
                    v.NumeroVersao,
                    v.Acao,
                    v.UsuarioNome,
                    v.DataHora,
                    v.Resultado
                })
                .ToListAsync();

            return Ok(versoes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar versões do relatório {RelatorioId}", id);
            return StatusCode(500, new { message = "Erro ao listar versões", error = ex.Message });
        }
    }

    // ── Helpers de ciclo de vida / versionamento ────────────────────────────
    private (int? id, string nome) UsuarioAtual()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var nome = User.FindFirst(ClaimTypes.Name)?.Value ?? "—";
        return (int.TryParse(idStr, out var id) ? id : (int?)null, nome);
    }

    private static string SerializarRespostas(Relatorio relatorio)
    {
        var dict = relatorio.RespostasCampos
            .ToDictionary(r => r.CampoRelatorioId.ToString(), r => r.Valor);
        return System.Text.Json.JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Recalcula o veredito do laudo (mesma regra do GetById): cada câmara é avaliada
    /// pela passagem para a oposta, uma reprovada reprova o ensaio, e o checklist
    /// "reprova se Sim" sobrepõe tudo. Usado no snapshot da conclusão.
    /// </summary>
    private async Task<string?> AvaliarResultadoAsync(Relatorio relatorio)
    {
        var ensaio = relatorio.EnsaioId.HasValue
            ? await _context.Ensaios
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == relatorio.EnsaioId.Value)
            : null;

        string? resultado = null;

        if (ensaio != null)
        {
            try
            {
                var cilindro = await _context.Cilindros.FindAsync(relatorio.CilindroId);
                resultado = CombinarResultados(await AnalisarEtapasAsync(ensaio, cilindro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao avaliar resultado do relatório {RelatorioId}", relatorio.Id);
            }
        }

        // Override rev02: checklist "reprova se Sim" respondido "Sim" força Reprovado.
        var camposReprovam = await _context.CamposRelatorio
            .Where(c => c.ReprovaSeSim)
            .Select(c => c.Id)
            .ToListAsync();

        if (camposReprovam.Count > 0)
        {
            var reprovado = relatorio.RespostasCampos.Any(r =>
                camposReprovam.Contains(r.CampoRelatorioId) &&
                string.Equals(r.Valor?.Trim(), "Sim", StringComparison.OrdinalIgnoreCase));
            if (reprovado) resultado = "Reprovado";
        }

        return resultado;
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

