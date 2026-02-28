using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
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
    public async Task<IActionResult> IniciarEnsaio([FromBody] IniciarEnsaioRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var camara = request.Camara?.Trim().ToUpperInvariant();
            if (camara != "A" && camara != "B")
            {
                return BadRequest(new { message = "Câmara inválida. Use 'A' ou 'B'." });
            }

            if (request.PressaoCarga <= 0)
            {
                return BadRequest(new { message = "Pressão de carga deve ser maior que zero." });
            }

            if (request.TempoCarga <= 0)
            {
                return BadRequest(new { message = "Tempo de carga deve ser maior que zero." });
            }

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
                CamaraTestada = camara,
                PressaoCargaConfigurada = request.PressaoCarga,
                TempoCargaConfigurado = request.TempoCarga,
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.Ensaios.Add(ensaio);
            await _context.SaveChangesAsync();

            // Executa comandos Modbus para iniciar o ensaio
            var errosModbus = new List<string>();

            try
            {
                // 1. Liga a câmara correspondente (Avança para Câmara A, Recua para Câmara B)
                var nomeBotaoCamara = camara == "A" ? "BOTAO_AVANCA_IHM" : "BOTAO_RECUA_IHM";
                var botaoCamara = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == nomeBotaoCamara && m.Ativo);

                if (botaoCamara != null)
                {
                    string funcaoEscrita = botaoCamara.TipoDado == "Boolean" || botaoCamara.FuncaoModbus == "ReadCoils" 
                        ? "WriteSingleCoil" 
                        : "WriteSingleRegister";

                    var configTemp = new ModbusConfig
                    {
                        Id = botaoCamara.Id,
                        Nome = botaoCamara.Nome,
                        IpAddress = botaoCamara.IpAddress,
                        Port = botaoCamara.Port,
                        SlaveId = botaoCamara.SlaveId,
                        FuncaoModbus = funcaoEscrita,
                        EnderecoRegistro = botaoCamara.EnderecoRegistro,
                        QuantidadeRegistros = botaoCamara.QuantidadeRegistros,
                        TipoDado = botaoCamara.TipoDado,
                        Ativo = botaoCamara.Ativo
                    };

                    var camaraAtivada = await _modbusService.EscreverRegistroAsync(configTemp, true);
                    if (!camaraAtivada)
                    {
                        errosModbus.Add($"Erro ao ativar {nomeBotaoCamara}");
                    }
                    else
                    {
                        _logger.LogInformation("Câmara {Camara} ({Botao}) ativada com sucesso", camara, nomeBotaoCamara);
                    }
                }
                else
                {
                    errosModbus.Add($"Registro '{nomeBotaoCamara}' não encontrado");
                }

                // 2. Escreve Pressão de Carga
                var pressaoRegistro = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == "PRESSAO_CARGA" && m.Ativo);

                if (pressaoRegistro != null)
                {
                    string funcaoEscrita = pressaoRegistro.TipoDado == "Boolean" || pressaoRegistro.FuncaoModbus == "ReadCoils" 
                        ? "WriteSingleCoil" 
                        : "WriteSingleRegister";

                    var configTemp = new ModbusConfig
                    {
                        Id = pressaoRegistro.Id,
                        Nome = pressaoRegistro.Nome,
                        IpAddress = pressaoRegistro.IpAddress,
                        Port = pressaoRegistro.Port,
                        SlaveId = pressaoRegistro.SlaveId,
                        FuncaoModbus = funcaoEscrita,
                        EnderecoRegistro = pressaoRegistro.EnderecoRegistro,
                        QuantidadeRegistros = pressaoRegistro.QuantidadeRegistros,
                        TipoDado = pressaoRegistro.TipoDado,
                        Ativo = pressaoRegistro.Ativo
                    };

                    object valorPressao = funcaoEscrita == "WriteSingleRegister" 
                        ? (ushort)Math.Round(request.PressaoCarga) 
                        : (object)(request.PressaoCarga > 0);

                    var pressaoEscrita = await _modbusService.EscreverRegistroAsync(configTemp, valorPressao);
                    if (!pressaoEscrita)
                    {
                        errosModbus.Add("Erro ao escrever PRESSAO_CARGA");
                    }
                    else
                    {
                        _logger.LogInformation("Pressão de carga {Pressao} escrita com sucesso", request.PressaoCarga);
                    }
                }
                else
                {
                    errosModbus.Add("Registro 'PRESSAO_CARGA' não encontrado");
                }

                // 3. Escreve Tempo de Carga
                var tempoRegistro = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == "TEMPO_CARGA" && m.Ativo);

                if (tempoRegistro != null)
                {
                    string funcaoEscrita = tempoRegistro.TipoDado == "Boolean" || tempoRegistro.FuncaoModbus == "ReadCoils" 
                        ? "WriteSingleCoil" 
                        : "WriteSingleRegister";

                    var configTemp = new ModbusConfig
                    {
                        Id = tempoRegistro.Id,
                        Nome = tempoRegistro.Nome,
                        IpAddress = tempoRegistro.IpAddress,
                        Port = tempoRegistro.Port,
                        SlaveId = tempoRegistro.SlaveId,
                        FuncaoModbus = funcaoEscrita,
                        EnderecoRegistro = tempoRegistro.EnderecoRegistro,
                        QuantidadeRegistros = tempoRegistro.QuantidadeRegistros,
                        TipoDado = tempoRegistro.TipoDado,
                        Ativo = tempoRegistro.Ativo
                    };

                    object valorTempo = funcaoEscrita == "WriteSingleRegister" 
                        ? (ushort)Math.Round(request.TempoCarga) 
                        : (object)(request.TempoCarga > 0);

                    var tempoEscrito = await _modbusService.EscreverRegistroAsync(configTemp, valorTempo);
                    if (!tempoEscrito)
                    {
                        errosModbus.Add("Erro ao escrever TEMPO_CARGA");
                    }
                    else
                    {
                        _logger.LogInformation("Tempo de carga {Tempo} escrito com sucesso", request.TempoCarga);
                    }
                }
                else
                {
                    errosModbus.Add("Registro 'TEMPO_CARGA' não encontrado");
                }

                // 4. Inicia o registro e verifica se está rodando (seguindo o padrão do motor)
                var iniciaRegistro = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

                var registroRodando = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs");

                if (iniciaRegistro != null && registroRodando != null)
                {
                    string funcaoEscrita = iniciaRegistro.TipoDado == "Boolean" || iniciaRegistro.FuncaoModbus == "ReadCoils" 
                        ? "WriteSingleCoil" 
                        : "WriteSingleRegister";

                    var configTemp = new ModbusConfig
                    {
                        Id = iniciaRegistro.Id,
                        Nome = iniciaRegistro.Nome,
                        IpAddress = iniciaRegistro.IpAddress,
                        Port = iniciaRegistro.Port,
                        SlaveId = iniciaRegistro.SlaveId,
                        FuncaoModbus = funcaoEscrita,
                        EnderecoRegistro = iniciaRegistro.EnderecoRegistro,
                        QuantidadeRegistros = iniciaRegistro.QuantidadeRegistros,
                        TipoDado = iniciaRegistro.TipoDado,
                        Ativo = iniciaRegistro.Ativo
                    };

                    // 1. Ativa o botão (mantém ativado até receber confirmação)
                    var iniciado = await _modbusService.EscreverRegistroAsync(configTemp, true);
                    if (!iniciado)
                    {
                        errosModbus.Add("Erro ao ativar INICIA_REGISTRO");
                    }
                    else
                    {
                        // 2. Aguarda um tempo inicial para o CLP processar o comando
                        await Task.Delay(300);

                        // 3. Verifica se o status mudou (aguarda confirmação via REGISTRO_RODANDO)
                        // O botão permanece ativado enquanto aguarda a confirmação
                        var timeout = TimeSpan.FromSeconds(2);
                        var intervalo = TimeSpan.FromMilliseconds(200);
                        var inicioVerificacao = DateTime.UtcNow;
                        bool rodando = false;
                        int tentativasLeitura = 0;

                        while (DateTime.UtcNow - inicioVerificacao < timeout)
                        {
                            await Task.Delay(intervalo);
                            tentativasLeitura++;

                            try
                            {
                                var status = await _modbusService.LerRegistroAsync(registroRodando.Id);
                                bool statusBool = status is bool boolVal ? boolVal : (status?.ToString() == "1" || status?.ToString() == "True");
                                
                                if (statusBool)
                                {
                                    rodando = true;
                                    _logger.LogInformation("REGISTRO_RODANDO confirmado após {Tentativas} tentativas", tentativasLeitura);
                                    break;
                                }
                            }
                            catch (Exception exLeitura)
                            {
                                _logger.LogWarning(exLeitura, "Erro ao ler REGISTRO_RODANDO na tentativa {Tentativa}", tentativasLeitura);
                            }
                        }

                        // 4. Desativa o botão após receber confirmação ou timeout
                        await _modbusService.EscreverRegistroAsync(configTemp, false);

                        if (!rodando)
                        {
                            errosModbus.Add("Registro não iniciou após 2 segundos. Verifique REGISTRO_RODANDO.");
                        }
                    }
                }
                else
                {
                    if (iniciaRegistro == null)
                        errosModbus.Add("Registro 'INICIA_REGISTRO' não encontrado");
                    if (registroRodando == null)
                        errosModbus.Add("Registro 'REGISTRO_RODANDO' (ReadInputs) não encontrado");
                }
            }
            catch (Exception exModbus)
            {
                _logger.LogError(exModbus, "Erro ao executar comandos Modbus ao iniciar ensaio");
                errosModbus.Add($"Erro ao executar comandos Modbus: {exModbus.Message}");
            }

            // Retorna resposta com avisos se houver erros Modbus, mas o ensaio foi criado
            var resposta = new
            {
                id = ensaio.Id,
                numero = ensaio.Numero,
                status = ensaio.Status,
                dataInicio = ensaio.DataInicio,
                avisosModbus = errosModbus.Any() ? errosModbus : null
            };

            if (errosModbus.Any())
            {
                _logger.LogWarning("Ensaio {EnsaioId} criado, mas com avisos Modbus: {Avisos}", ensaio.Id, string.Join(", ", errosModbus));
                return Ok(resposta);
            }

            return Ok(resposta);
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

            // Remove as leituras desse ensaio no InfluxDB (período do ensaio)
            try
            {
                await RemoverLeiturasInfluxAsync(ensaio);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover leituras do ensaio {EnsaioId} no InfluxDB", id);
            }

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
            DataMais.Models.ModbusConfig? pressaoRegistro = null;

            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            if (ensaio.Status != "EmExecucao")
            {
                return BadRequest(new { message = $"Ensaio não está em execução (status atual: {ensaio.Status})" });
            }

            // Define registro de pressão preferencial com base na câmara testada
            string? registroPreferencial = null;
            if (string.Equals(ensaio.CamaraTestada, "A", StringComparison.OrdinalIgnoreCase))
            {
                registroPreferencial = "PRESSAO_A_CONV";
            }
            else if (string.Equals(ensaio.CamaraTestada, "B", StringComparison.OrdinalIgnoreCase))
            {
                registroPreferencial = "PRESSAO_B_CONV";
            }

            // Procura registro de pressão convertida (preferencialmente da câmara configurada, depois geral)
            var query = _context.ModbusConfigs
                .Where(m => m.Ativo);

            if (!string.IsNullOrWhiteSpace(registroPreferencial))
            {
                var preferencial = await query
                    .Where(m => m.Nome == registroPreferencial)
                    .FirstOrDefaultAsync();

                if (preferencial != null)
                {
                    pressaoRegistro = preferencial;
                }
            }

            if (pressaoRegistro == null)
            {
                pressaoRegistro = await _context.ModbusConfigs
                    .Where(m => m.Ativo)
                    .Where(m => m.Nome == "PRESSAO_GERAL_CONV"
                                || m.Nome == "PRESSAO_GERAL"
                                || m.Nome == "PRESSAO_A_CONV"
                                || m.Nome == "PRESSAO_B_CONV")
                    .OrderBy(m => m.Nome) // GERAL_CONV primeiro, depois alternativas
                    .FirstOrDefaultAsync();
            }

            if (pressaoRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus de pressão não encontrado (PRESSAO_GERAL_CONV / PRESSAO_GERAL / PRESSAO_A_CONV / PRESSAO_B_CONV)" });
            }

            var valorObj = await _modbusService.LerRegistroAsync(pressaoRegistro!.Id);
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

    /// <summary>
    /// Remove do InfluxDB todas as leituras de pressão associadas a um ensaio cancelado.
    /// </summary>
    private async Task RemoverLeiturasInfluxAsync(Ensaio ensaio)
    {
        var appConfig = _configService.GetConfig();

        if (string.IsNullOrWhiteSpace(appConfig.Influx.Url) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Token) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Organization) ||
            string.IsNullOrWhiteSpace(appConfig.Influx.Bucket))
        {
            _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível remover leituras do ensaio {EnsaioId}.", ensaio.Id);
            return;
        }

        // Define intervalo de tempo para remoção (do início ao fim do ensaio, com pequena margem)
        var from = (ensaio.DataInicio ?? DateTime.UtcNow.AddHours(-1)).ToUniversalTime().AddMinutes(-1);
        var to = (ensaio.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

        var predicate = $"_measurement=\"ensaio_pressao\" AND ensaioId=\"{ensaio.Id}\"";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var deleteApi = influxClient.GetDeleteApi();

        await deleteApi.Delete(from, to, predicate, appConfig.Influx.Bucket, appConfig.Influx.Organization);

        _logger.LogInformation("Leituras do ensaio {EnsaioId} removidas do InfluxDB no intervalo {From} - {To}", ensaio.Id, from, to);
    }
}

public class IniciarEnsaioRequest
{
    /// <summary>
    /// Câmara a ser testada: "A" (avança) ou "B" (recua)
    /// </summary>
    [Required]
    public string Camara { get; set; } = string.Empty;

    /// <summary>
    /// Pressão de carga configurada para o ensaio (bar)
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Pressão de carga deve ser maior que zero.")]
    public decimal PressaoCarga { get; set; }

    /// <summary>
    /// Tempo de carga configurado para o ensaio (segundos)
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Tempo de carga deve ser maior que zero.")]
    public decimal TempoCarga { get; set; }
}
