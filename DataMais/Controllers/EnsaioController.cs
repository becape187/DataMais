using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// Retorna o ensaio atualmente em execução (se houver), para a tela retomar o registro
    /// ao reentrar — o backend é a fonte da verdade do que está rodando.
    /// </summary>
    [HttpGet("ativo")]
    public async Task<IActionResult> GetEnsaioAtivo()
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Cliente)
                .Include(e => e.Cilindro)
                .Where(e => e.Status == "EmExecucao")
                .OrderByDescending(e => e.DataInicio)
                .FirstOrDefaultAsync();

            if (ensaio == null)
            {
                return Ok(new { ativo = false });
            }

            return Ok(new
            {
                ativo = true,
                id = ensaio.Id,
                numero = ensaio.Numero,
                status = ensaio.Status,
                dataInicio = ensaio.DataInicio,
                camara = ensaio.CamaraTestada,
                pressaoCarga = ensaio.PressaoCargaConfigurada,
                tempoCarga = ensaio.TempoCargaConfigurado,
                clienteNome = ensaio.Cliente != null ? ensaio.Cliente.Nome : null,
                cilindroNome = ensaio.Cilindro != null ? ensaio.Cilindro.Nome : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ensaio ativo");
            return StatusCode(500, new { message = "Erro ao obter ensaio ativo", error = ex.Message });
        }
    }

    [HttpPost("iniciar")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> IniciarEnsaio([FromBody] IniciarEnsaioRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Idempotência: se já existe um ensaio em execução, retorna ele em vez de criar
            // outro (e sem reenviar os comandos Modbus de início).
            var ensaioEmExecucao = await _context.Ensaios
                .Where(e => e.Status == "EmExecucao")
                .OrderByDescending(e => e.DataInicio)
                .FirstOrDefaultAsync();

            if (ensaioEmExecucao != null)
            {
                _logger.LogInformation("Iniciar ensaio idempotente: já há ensaio {EnsaioId} em execução", ensaioEmExecucao.Id);
                return Ok(new
                {
                    id = ensaioEmExecucao.Id,
                    numero = ensaioEmExecucao.Numero,
                    status = ensaioEmExecucao.Status,
                    dataInicio = ensaioEmExecucao.DataInicio,
                    jaExistia = true
                });
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
                Vessel = string.IsNullOrWhiteSpace(request.Vessel) ? null : request.Vessel.Trim(),
                LocalTeste = string.IsNullOrWhiteSpace(request.LocalTeste) ? null : request.LocalTeste.Trim(),
                Departamento = string.IsNullOrWhiteSpace(request.Departamento) ? null : request.Departamento.Trim(),
                OrdemServico = string.IsNullOrWhiteSpace(request.OrdemServico) ? null : request.OrdemServico.Trim(),
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.Ensaios.Add(ensaio);
            await _context.SaveChangesAsync();

            // Executa comandos Modbus para iniciar o ensaio
            var errosModbus = new List<string>();

            try
            {
                // 1. Seleciona a câmara (Avança para Câmara A, Recua para Câmara B).
                //    Desliga o botão OPOSTO antes de ligar o escolhido — se o operador deixou
                //    o outro coil retido pela IHM, os dois ficariam ligados ao mesmo tempo.
                //    Depois relê os dois coils para confirmar o estado no CLP (double-check).
                var nomeBotaoCamara = camara == "A" ? "BOTAO_AVANCA_IHM" : "BOTAO_RECUA_IHM";
                var nomeBotaoOposto = camara == "A" ? "BOTAO_RECUA_IHM" : "BOTAO_AVANCA_IHM";

                var botaoCamara = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == nomeBotaoCamara && m.Ativo);
                var botaoOposto = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == nomeBotaoOposto && m.Ativo);

                if (botaoCamara == null)
                {
                    return await AbortarEnsaioPorFalhaCamaraAsync(ensaio, botaoCamara, botaoOposto,
                        $"Registro '{nomeBotaoCamara}' não encontrado no cadastro Modbus.");
                }

                // 1a. Desliga o botão oposto primeiro
                if (botaoOposto != null)
                {
                    var opostoDesligado = await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoOposto), false);
                    if (!opostoDesligado)
                    {
                        return await AbortarEnsaioPorFalhaCamaraAsync(ensaio, botaoCamara, botaoOposto,
                            $"Não foi possível desligar {nomeBotaoOposto} antes de ativar a câmara {camara}.");
                    }
                }
                else
                {
                    errosModbus.Add($"Registro '{nomeBotaoOposto}' não encontrado — não foi possível garantir que o botão oposto está desligado");
                }

                // 1b. Liga o botão da câmara escolhida
                var camaraAtivada = await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoCamara), true);
                if (!camaraAtivada)
                {
                    return await AbortarEnsaioPorFalhaCamaraAsync(ensaio, botaoCamara, botaoOposto,
                        $"Não foi possível ativar {nomeBotaoCamara}.");
                }

                // 1c. Double-check: relê os dois coils e confirma escolhido=ON, oposto=OFF.
                //     Se não bater, reaplica os comandos e confere mais uma vez.
                bool confirmado = false;
                bool? estadoCamara = null, estadoOposto = null;

                for (int tentativa = 1; tentativa <= 2 && !confirmado; tentativa++)
                {
                    await Task.Delay(200);
                    estadoCamara = await LerCoilAsync(botaoCamara.Id);
                    estadoOposto = botaoOposto != null ? await LerCoilAsync(botaoOposto.Id) : false;

                    if (estadoCamara == true && estadoOposto != true)
                    {
                        confirmado = true;
                    }
                    else if (tentativa < 2)
                    {
                        _logger.LogWarning(
                            "Estado dos botões de câmara divergente ({BotaoCamara}={EstadoCamara}, {BotaoOposto}={EstadoOposto}), reaplicando comandos",
                            nomeBotaoCamara, estadoCamara, nomeBotaoOposto, estadoOposto);

                        if (botaoOposto != null && estadoOposto == true)
                            await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoOposto), false);
                        if (estadoCamara != true)
                            await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoCamara), true);
                    }
                }

                if (!confirmado)
                {
                    return await AbortarEnsaioPorFalhaCamaraAsync(ensaio, botaoCamara, botaoOposto,
                        $"Estado dos botões de câmara não confirmado após reaplicar: {nomeBotaoCamara}={FormatarEstadoCoil(estadoCamara)}, {nomeBotaoOposto}={FormatarEstadoCoil(estadoOposto)} (esperado: ligado / desligado). Verifique se o botão Avança/Recua não está pressionado na tela.");
                }

                _logger.LogInformation("Câmara {Camara} ({Botao}) ativada e confirmada por leitura ({Oposto} desligado)",
                    camara, nomeBotaoCamara, nomeBotaoOposto);

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

                        // INICIA_REGISTRO agora é um coil de NÍVEL (não pulso): permanece LIGADO
                        // durante todo o registro. Só será desligado ao interromper/cancelar o ensaio.
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
    [Authorize(Roles = "Admin,Operador")]
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

            // Desliga INICIA_REGISTRO no CLP — esse coil define "parar" na unidade hidráulica.
            var desligou = await SetIniciaRegistroAsync(false);
            if (!desligou)
            {
                _logger.LogWarning("Não foi possível desligar INICIA_REGISTRO ao interromper o ensaio {EnsaioId}", id);
            }

            // Desliga explicitamente os dois botões de câmara da IHM (avança e recua),
            // independente da câmara testada, para não deixar coil retido no CLP.
            var falhasBotoes = await DesligarBotoesCamaraAsync();
            if (falhasBotoes.Any())
            {
                _logger.LogWarning("Falhas ao desligar botões de câmara ao interromper o ensaio {EnsaioId}: {Falhas}",
                    id, string.Join(", ", falhasBotoes));
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
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> CancelarEnsaio(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            // Desliga INICIA_REGISTRO no CLP — parar o registro na unidade hidráulica.
            var desligou = await SetIniciaRegistroAsync(false);
            if (!desligou)
            {
                _logger.LogWarning("Não foi possível desligar INICIA_REGISTRO ao cancelar o ensaio {EnsaioId}", id);
            }

            // Desliga explicitamente os dois botões de câmara da IHM (avança e recua),
            // independente da câmara testada, para não deixar coil retido no CLP.
            var falhasBotoes = await DesligarBotoesCamaraAsync();
            if (falhasBotoes.Any())
            {
                _logger.LogWarning("Falhas ao desligar botões de câmara ao cancelar o ensaio {EnsaioId}: {Falhas}",
                    id, string.Join(", ", falhasBotoes));
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
    /// Lê as pressões A e B via Modbus, grava no InfluxDB e retorna os pontos para o frontend.
    /// Sempre lê e salva ambas as pressões, independente da câmara selecionada.
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

            // Busca registros Modbus para pressão A e B
            var pressaoARegistro = await _context.ModbusConfigs
                .Where(m => m.Ativo && m.Nome == "PRESSAO_A_CONV")
                .FirstOrDefaultAsync();

            var pressaoBRegistro = await _context.ModbusConfigs
                .Where(m => m.Ativo && m.Nome == "PRESSAO_B_CONV")
                .FirstOrDefaultAsync();

            // Se não encontrar os registros específicos, tenta alternativas
            if (pressaoARegistro == null)
            {
                pressaoARegistro = await _context.ModbusConfigs
                    .Where(m => m.Ativo && (m.Nome == "PRESSAO_A" || m.Nome == "PRESSAO_GERAL_CONV" || m.Nome == "PRESSAO_GERAL"))
                    .FirstOrDefaultAsync();
            }

            if (pressaoBRegistro == null)
            {
                pressaoBRegistro = await _context.ModbusConfigs
                    .Where(m => m.Ativo && (m.Nome == "PRESSAO_B" || m.Nome == "PRESSAO_GERAL_CONV" || m.Nome == "PRESSAO_GERAL"))
                    .FirstOrDefaultAsync();
            }

            double? pressaoA = null;
            double? pressaoB = null;

            // Lê pressão A
            if (pressaoARegistro != null)
            {
                try
                {
                    var valorAObj = await _modbusService.LerRegistroAsync(pressaoARegistro.Id);
                    if (valorAObj != null)
                    {
                        var valorA = Convert.ToDouble(valorAObj);
                        if (!double.IsNaN(valorA) && !double.IsInfinity(valorA))
                        {
                            pressaoA = Math.Max(0, Math.Min(1000, valorA));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao ler pressão A do Modbus para ensaio {EnsaioId}", id);
                }
            }

            // Lê pressão B
            if (pressaoBRegistro != null)
            {
                try
                {
                    var valorBObj = await _modbusService.LerRegistroAsync(pressaoBRegistro.Id);
                    if (valorBObj != null)
                    {
                        var valorB = Convert.ToDouble(valorBObj);
                        if (!double.IsNaN(valorB) && !double.IsInfinity(valorB))
                        {
                            pressaoB = Math.Max(0, Math.Min(1000, valorB));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao ler pressão B do Modbus para ensaio {EnsaioId}", id);
                }
            }

            // Se não conseguiu ler nenhuma pressão, retorna erro
            if (!pressaoA.HasValue && !pressaoB.HasValue)
            {
                return StatusCode(500, new { message = "Falha ao ler pressões A e B do Modbus" });
            }

            var timestamp = DateTime.UtcNow;
            var timeLabel = DateTime.Now.ToString("HH:mm:ss");

            // Grava ambas as pressões no InfluxDB
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
                        .Timestamp(timestamp, WritePrecision.Ns);

                    // Adiciona campo pressaoA se disponível
                    if (pressaoA.HasValue)
                    {
                        point = point.Field("pressaoA", pressaoA.Value);
                    }

                    // Adiciona campo pressaoB se disponível
                    if (pressaoB.HasValue)
                    {
                        point = point.Field("pressaoB", pressaoB.Value);
                    }

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
                pressaoA = pressaoA,
                pressaoB = pressaoB
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler pressão atual do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao ler pressão atual do ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Retorna o histórico de pressões (A e B) do ensaio a partir do InfluxDB,
    /// para RECONSTRUIR o gráfico ao reentrar na tela (sair e voltar não perde os dados).
    /// </summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> GetHistoricoPressao(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios.FindAsync(id);
            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            var appConfig = _configService.GetConfig();

            if (string.IsNullOrWhiteSpace(appConfig.Influx.Url) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Token) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Organization) ||
                string.IsNullOrWhiteSpace(appConfig.Influx.Bucket))
            {
                _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível reconstruir o histórico do ensaio {EnsaioId}.", id);
                return Ok(new { dados = Array.Empty<object>(), totalPontos = 0 });
            }

            var from = (ensaio.DataInicio ?? ensaio.DataCriacao).ToUniversalTime().AddMinutes(-1);
            var to = (ensaio.DataFim ?? DateTime.UtcNow).ToUniversalTime().AddMinutes(1);

            var flux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaio.Id}"" and (r._field == ""pressaoA"" or r._field == ""pressaoB""))
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value"", ""_field""])";

            using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
            var queryApi = influxClient.GetQueryApi();
            var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);

            // Agrupa por timestamp (preserva cada leitura), combinando A e B do mesmo instante
            var pontos = new SortedDictionary<DateTime, Dictionary<string, double>>();
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    var time = record.GetTime();
                    var field = record.GetField();
                    var value = record.GetValue();
                    if (time == null || field == null || !(value is IConvertible)) continue;

                    try
                    {
                        var dt = time.Value.ToDateTimeUtc();
                        if (!pontos.ContainsKey(dt)) pontos[dt] = new Dictionary<string, double>();
                        pontos[dt][field] = Convert.ToDouble(value);
                    }
                    catch { }
                }
            }

            var dados = pontos.Select(kv => new
            {
                time = kv.Key.ToLocalTime().ToString("HH:mm:ss"),
                pressaoA = kv.Value.ContainsKey("pressaoA") ? (double?)Math.Round(kv.Value["pressaoA"], 2) : null,
                pressaoB = kv.Value.ContainsKey("pressaoB") ? (double?)Math.Round(kv.Value["pressaoB"], 2) : null
            }).ToList();

            return Ok(new { dados, totalPontos = dados.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reconstruir histórico do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao reconstruir histórico do ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Liga/desliga o coil INICIA_REGISTRO no CLP. Esse coil é de NÍVEL e define
    /// "começar" (true) e "parar" (false) o registro na unidade hidráulica.
    /// </summary>
    private async Task<bool> SetIniciaRegistroAsync(bool valor)
    {
        var iniciaRegistro = await _context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

        if (iniciaRegistro == null)
        {
            _logger.LogWarning("Registro 'INICIA_REGISTRO' não encontrado ao tentar definir {Valor}", valor);
            return false;
        }

        var configTemp = ConfigParaEscrita(iniciaRegistro);

        object v = configTemp.FuncaoModbus == "WriteSingleCoil"
            ? (object)valor
            : (object)(ushort)(valor ? 1 : 0);

        return await _modbusService.EscreverRegistroAsync(configTemp, v);
    }

    /// <summary>
    /// Monta uma cópia do registro com a função de ESCRITA correspondente — os registros
    /// de botão são cadastrados como ReadCoils (leitura de estado) e precisam virar
    /// WriteSingleCoil/WriteSingleRegister para escrever.
    /// </summary>
    private static ModbusConfig ConfigParaEscrita(ModbusConfig registro)
    {
        string funcaoEscrita = registro.TipoDado == "Boolean" || registro.FuncaoModbus == "ReadCoils"
            ? "WriteSingleCoil"
            : "WriteSingleRegister";

        return new ModbusConfig
        {
            Id = registro.Id,
            Nome = registro.Nome,
            IpAddress = registro.IpAddress,
            Port = registro.Port,
            SlaveId = registro.SlaveId,
            FuncaoModbus = funcaoEscrita,
            EnderecoRegistro = registro.EnderecoRegistro,
            QuantidadeRegistros = registro.QuantidadeRegistros,
            TipoDado = registro.TipoDado,
            Ativo = registro.Ativo
        };
    }

    /// <summary>
    /// Lê o estado atual de um coil pelo registro cadastrado (ReadCoils).
    /// Retorna null se a leitura falhar.
    /// </summary>
    private async Task<bool?> LerCoilAsync(int registroId)
    {
        try
        {
            var valor = await _modbusService.LerRegistroAsync(registroId);
            if (valor == null)
                return null;
            return valor is bool b ? b : valor.ToString() == "1" || valor.ToString() == "True";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler estado do coil {RegistroId} para confirmação", registroId);
            return null;
        }
    }

    private static string FormatarEstadoCoil(bool? estado) =>
        estado == null ? "leitura falhou" : (estado.Value ? "ligado" : "desligado");

    /// <summary>
    /// Desliga explicitamente os DOIS botões de câmara da IHM (BOTAO_AVANCA_IHM e
    /// BOTAO_RECUA_IHM) no CLP, independente da câmara do ensaio. Usado ao parar o
    /// ensaio (interromper/cancelar) para não deixar nenhum coil retido. Best-effort:
    /// falhas são logadas e devolvidas na lista, sem lançar exceção.
    /// </summary>
    private async Task<List<string>> DesligarBotoesCamaraAsync()
    {
        var falhas = new List<string>();

        foreach (var nome in new[] { "BOTAO_AVANCA_IHM", "BOTAO_RECUA_IHM" })
        {
            try
            {
                var botao = await _context.ModbusConfigs
                    .FirstOrDefaultAsync(m => m.Nome == nome && m.Ativo);

                if (botao == null)
                {
                    _logger.LogWarning("Registro '{Nome}' não encontrado ao desligar botões de câmara", nome);
                    falhas.Add($"Registro '{nome}' não encontrado");
                    continue;
                }

                var desligado = await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botao), false);
                if (!desligado)
                {
                    falhas.Add($"Erro ao desligar {nome}");
                }
                else
                {
                    _logger.LogInformation("{Nome} desligado ao parar o ensaio", nome);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao desligar {Nome} ao parar o ensaio", nome);
                falhas.Add($"Erro ao desligar {nome}: {ex.Message}");
            }
        }

        return falhas;
    }

    /// <summary>
    /// Aborta o início do ensaio quando a seleção da câmara não pôde ser garantida:
    /// desliga os dois botões por segurança (best-effort), remove o ensaio recém-criado
    /// do banco e devolve erro 500 com a causa (o frontend exibe a message no alert).
    /// </summary>
    private async Task<IActionResult> AbortarEnsaioPorFalhaCamaraAsync(
        Ensaio ensaio, ModbusConfig? botaoCamara, ModbusConfig? botaoOposto, string motivo)
    {
        _logger.LogError("Abortando início do ensaio {EnsaioId}: {Motivo}", ensaio.Id, motivo);

        foreach (var botao in new[] { botaoCamara, botaoOposto })
        {
            if (botao == null) continue;
            try
            {
                await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botao), false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao desligar {Botao} durante aborto do ensaio {EnsaioId}", botao.Nome, ensaio.Id);
            }
        }

        try
        {
            _context.Ensaios.Remove(ensaio);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao remover ensaio {EnsaioId} abortado do banco", ensaio.Id);
        }

        return StatusCode(500, new
        {
            message = $"Ensaio abortado: {motivo}",
            abortado = true
        });
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
    /// Tempo de carga configurado para o ensaio (minutos)
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Tempo de carga deve ser maior que zero.")]
    public decimal TempoCarga { get; set; }

    // Identificação do documento (relatório rev02) — opcionais, preenchidos no setup.
    public string? Vessel { get; set; }
    public string? LocalTeste { get; set; }
    public string? Departamento { get; set; }
    public string? OrdemServico { get; set; }
}
