using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using DataMais.Configuration;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnsaioController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ModbusService _modbusService;
    private readonly ConfigService _configService;
    private readonly ILogger<EnsaioController> _logger;

    public EnsaioController(
        DataMaisDbContext context,
        ModbusService modbusService,
        ConfigService configService,
        ILogger<EnsaioController> logger)
    {
        _context = context;
        _modbusService = modbusService;
        _configService = configService;
        _logger = logger;
    }

    [HttpPost("iniciar")]
    public async Task<IActionResult> IniciarEnsaio()
    {
        try
        {
            var appConfig = _configService.GetConfig();
            var sistema = appConfig.Sistema;

            if (!sistema.ClienteId.HasValue || !sistema.CilindroId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Cliente e cilindro do sistema não configurados. Configure na tela de Dashboard antes de iniciar o ensaio."
                });
            }

            var cliente = await _context.Clientes.FindAsync(sistema.ClienteId.Value);
            var cilindro = await _context.Cilindros.FindAsync(sistema.CilindroId.Value);

            if (cliente == null || cilindro == null)
            {
                return BadRequest(new
                {
                    message = "Cliente ou cilindro configurado não encontrado no banco de dados."
                });
            }

            var agora = DateTime.UtcNow;
            var numero = $"ENSAIO-{agora:yyyyMMdd-HHmmss}";

            var ensaio = new Ensaio
            {
                Numero = numero,
                Status = "EmExecucao",
                DataInicio = agora,
                ClienteId = cliente.Id,
                CilindroId = cilindro.Id,
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.Ensaios.Add(ensaio);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = ensaio.Id,
                numero = ensaio.Numero,
                status = ensaio.Status,
                dataInicio = ensaio.DataInicio
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar ensaio");
            return StatusCode(500, new { message = "Erro ao iniciar ensaio", error = ex.Message });
        }
    }

    [HttpPost("interromper/{id:int}")]
    public async Task<IActionResult> InterromperEnsaio(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            if (ensaio.Status == "Concluido" || ensaio.Status == "Cancelado")
            {
                return Ok(new
                {
                    message = "Ensaio já finalizado",
                    status = ensaio.Status,
                    dataFim = ensaio.DataFim
                });
            }

            // Finaliza ensaio
            ensaio.Status = "Concluido";
            ensaio.DataFim = DateTime.UtcNow;
            ensaio.DataAtualizacao = DateTime.UtcNow;

            // Cria relatório vinculado ao ensaio
            var dataRelatorio = ensaio.DataFim ?? DateTime.UtcNow;
            var numeroRelatorio = $"REL-{ensaio.Numero}";

            var relatorio = new Relatorio
            {
                Numero = numeroRelatorio,
                Data = dataRelatorio,
                Observacoes = $"Relatório gerado automaticamente a partir do ensaio {ensaio.Numero}.",
                ClienteId = ensaio.ClienteId,
                CilindroId = ensaio.CilindroId,
                EnsaioId = ensaio.Id,
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow
            };

            _context.Relatorios.Add(relatorio);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Ensaio interrompido e relatório gerado com sucesso",
                status = ensaio.Status,
                dataFim = ensaio.DataFim,
                relatorio = new
                {
                    relatorio.Id,
                    relatorio.Numero,
                    relatorio.Data,
                    relatorio.ClienteId,
                    relatorio.CilindroId,
                    relatorio.EnsaioId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao interromper ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao interromper ensaio", error = ex.Message });
        }
    }

    [HttpPost("cancelar/{id:int}")]
    public async Task<IActionResult> CancelarEnsaio(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            // Marca como cancelado (não salvo pelo usuário)
            ensaio.Status = "Cancelado";
            ensaio.DataFim = DateTime.UtcNow;
            ensaio.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Ensaio cancelado (não salvo)",
                status = ensaio.Status,
                dataFim = ensaio.DataFim
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao cancelar ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Lê a pressão atual via Modbus, grava no InfluxDB e retorna o ponto para o frontend.
    /// </summary>
    [HttpGet("{id:int}/pressao-atual")]
    public async Task<IActionResult> LerPressaoAtual(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            if (ensaio.Status != "EmExecucao")
            {
                return BadRequest(new { message = $"Ensaio não está em execução (status atual: {ensaio.Status})" });
            }

            // Procura registro de pressão convertida (preferencialmente pressão geral)
            var pressaoRegistro = await _context.ModbusConfigs
                .Where(m => m.Ativo)
                .Where(m => m.Nome == "PRESSAO_GERAL_CONV" 
                            || m.Nome == "PRESSAO_GERAL" 
                            || m.Nome == "PRESSAO_A_CONV" 
                            || m.Nome == "PRESSAO_B_CONV")
                .OrderBy(m => m.Nome) // GERAL_CONV primeiro, depois alternativas
                .FirstOrDefaultAsync();

            if (pressaoRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus de pressão não encontrado (PRESSAO_GERAL_CONV / PRESSAO_GERAL / PRESSAO_A_CONV / PRESSAO_B_CONV)" });
            }

            var valorObj = await _modbusService.LerRegistroAsync(pressaoRegistro.Id);
            if (valorObj == null)
            {
                return StatusCode(500, new { message = "Falha ao ler pressão do Modbus" });
            }

            double pressao;
            try
            {
                pressao = Convert.ToDouble(valorObj);
            }
            catch
            {
                _logger.LogWarning("Valor de pressão inválido retornado do Modbus: {Valor}", valorObj);
                return StatusCode(500, new { message = "Valor de pressão inválido retornado do Modbus" });
            }

            // Garante que a pressão fique em um intervalo razoável (0 a 1000 bar)
            if (double.IsNaN(pressao) || double.IsInfinity(pressao))
            {
                return StatusCode(500, new { message = "Valor de pressão inválido (NaN ou infinito)" });
            }

            pressao = Math.Max(0, Math.Min(1000, pressao));

            var timestamp = DateTime.UtcNow;
            var timeLabel = DateTime.Now.ToString("HH:mm:ss");

            // Grava no InfluxDB (melhor para séries temporais)
            try
            {
                var appConfig = _configService.GetConfig();

                if (!string.IsNullOrWhiteSpace(appConfig.Influx.Url) &&
                    !string.IsNullOrWhiteSpace(appConfig.Influx.Token) &&
                    !string.IsNullOrWhiteSpace(appConfig.Influx.Organization) &&
                    !string.IsNullOrWhiteSpace(appConfig.Influx.Bucket))
                {
                    using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
                    var writeApi = influxClient.GetWriteApiAsync();

                    var point = PointData
                        .Measurement("ensaio_pressao")
                        .Tag("ensaioId", ensaio.Id.ToString())
                        .Tag("clienteId", ensaio.ClienteId.ToString())
                        .Tag("cilindroId", ensaio.CilindroId.ToString())
                        .Field("pressao", pressao)
                        .Timestamp(timestamp, WritePrecision.Ns);

                    await writeApi.WritePointAsync(point, appConfig.Influx.Bucket, appConfig.Influx.Organization);
                }
                else
                {
                    _logger.LogWarning("Configuração do InfluxDB incompleta. Leituras de ensaio não serão persistidas no InfluxDB.");
                }
            }
            catch (Exception ex)
            {
                // Não falha o endpoint se apenas a persistência der erro
                _logger.LogError(ex, "Erro ao gravar leitura de ensaio no InfluxDB");
            }

            return Ok(new
            {
                time = timeLabel,
                pressao
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler pressão atual do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao ler pressão atual do ensaio", error = ex.Message });
        }
    }
}

