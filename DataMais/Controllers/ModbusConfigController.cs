using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModbusConfigController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<ModbusConfigController> _logger;
    private readonly ModbusService _modbusService;

    public ModbusConfigController(DataMaisDbContext context, ILogger<ModbusConfigController> logger, ModbusService modbusService)
    {
        _context = context;
        _logger = logger;
        _modbusService = modbusService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var configs = await _context.ModbusConfigs
                .OrderBy(c => c.IpAddress)
                .ThenBy(c => c.OrdemLeitura)
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter configurações Modbus");
            return StatusCode(500, new { message = "Erro ao obter configurações Modbus" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var config = await _context.ModbusConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { message = "Configuração Modbus não encontrada" });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter configuração Modbus");
            return StatusCode(500, new { message = "Erro ao obter configuração Modbus" });
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportFromJson([FromBody] ImportModbusRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.JsonContent))
            {
                return BadRequest(new { message = "Conteúdo JSON é obrigatório" });
            }

            // Parse do JSON
            var jsonDoc = JsonDocument.Parse(request.JsonContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("mapping", out var mappingProperty))
            {
                return BadRequest(new { message = "JSON inválido: propriedade 'mapping' não encontrada" });
            }

            var ipAddress = request.IpAddress ?? "modec.automais.cloud";
            var port = request.Port ?? 502;
            var slaveId = request.SlaveId ?? 1;
            var replaceExisting = request.ReplaceExisting ?? false;

            // Limpar registros existentes do mesmo IP (se solicitado)
            if (replaceExisting)
            {
                var existing = await _context.ModbusConfigs
                    .Where(m => m.IpAddress == ipAddress)
                    .ToListAsync();

                if (existing.Any())
                {
                    _context.ModbusConfigs.RemoveRange(existing);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Removidos {existing.Count} registros existentes para {ipAddress}");
                }
            }

            var mappings = mappingProperty.EnumerateArray();
            var ordemLeitura = 1;
            var novosRegistros = new List<ModbusConfig>();
            var registrosAtualizados = 0;
            var registrosIgnorados = 0;

            // Buscar registros existentes do mesmo IP para verificação de duplicatas
            var registrosExistentes = await _context.ModbusConfigs
                .Where(m => m.IpAddress == ipAddress)
                .ToListAsync();

            foreach (var mapping in mappings)
            {
                if (!mapping.TryGetProperty("address", out var addressProp) ||
                    !mapping.TryGetProperty("datatype", out var datatypeProp) ||
                    !mapping.TryGetProperty("variable", out var variableProp))
                {
                    continue;
                }

                var address = addressProp.GetUInt16();
                var datatype = datatypeProp.GetString();
                var variable = variableProp.GetString();

                if (string.IsNullOrEmpty(datatype) || string.IsNullOrEmpty(variable))
                    continue;

                // Mapear tipo de dado para função Modbus
                var funcaoModbus = datatype switch
                {
                    "coil" => "ReadCoils",
                    "discrete_input" => "ReadInputs",
                    "holding_register" => "ReadHoldingRegisters",
                    "input_register" => "ReadInputRegisters",
                    _ => "ReadHoldingRegisters"
                };

                // Determinar tipo de dado
                var tipoDado = datatype switch
                {
                    "coil" or "discrete_input" => "Boolean",
                    "holding_register" or "input_register" => "UInt16",
                    _ => "UInt16"
                };

                // Verificar se já existe registro com mesmo nome, função E endereço
                // Permite múltiplos registros com mesmo nome e função, desde que tenham endereços diferentes
                var registroExistente = registrosExistentes
                    .FirstOrDefault(r => r.Nome == variable && 
                                        r.FuncaoModbus == funcaoModbus && 
                                        r.EnderecoRegistro == address &&
                                        r.IpAddress == ipAddress);

                if (registroExistente != null)
                {
                    // Atualizar registro existente (mesmo nome, função e endereço)
                    registroExistente.Descricao = $"Registro Modbus {variable} (Address: {address}, Type: {datatype})";
                    registroExistente.TipoDado = tipoDado;
                    registroExistente.OrdemLeitura = ordemLeitura++;
                    registroExistente.DataAtualizacao = DateTime.UtcNow;
                    registroExistente.Ativo = true;
                    registrosAtualizados++;
                    _logger.LogInformation($"Atualizado registro existente: {variable} ({funcaoModbus}) no endereço {address}");
                }
                else
                {
                    // Verificar se existe registro com mesmo nome e função mas endereço diferente
                    var registroComMesmoNomeFuncao = registrosExistentes
                        .FirstOrDefault(r => r.Nome == variable && r.FuncaoModbus == funcaoModbus);
                    
                    if (registroComMesmoNomeFuncao != null)
                    {
                        _logger.LogInformation($"Nome '{variable}' com função '{funcaoModbus}' já existe no endereço {registroComMesmoNomeFuncao.EnderecoRegistro}, criando novo registro no endereço {address}");
                    }
                    else
                    {
                        // Verificar se existe registro com mesmo nome mas função diferente (permitido)
                        var registroComMesmoNome = registrosExistentes
                            .FirstOrDefault(r => r.Nome == variable && r.FuncaoModbus != funcaoModbus);
                        
                        if (registroComMesmoNome != null)
                        {
                            _logger.LogInformation($"Nome '{variable}' já existe com função diferente ({registroComMesmoNome.FuncaoModbus}), criando novo registro com função {funcaoModbus}");
                        }
                    }

                    // Criar novo registro (permite mesmo nome e função se endereço for diferente)
                    var registro = new ModbusConfig
                    {
                        Nome = variable,
                        Descricao = $"Registro Modbus {variable} (Address: {address}, Type: {datatype})",
                        IpAddress = ipAddress,
                        Port = port,
                        SlaveId = (byte)slaveId,
                        FuncaoModbus = funcaoModbus,
                        EnderecoRegistro = address,
                        QuantidadeRegistros = 1,
                        TipoDado = tipoDado,
                        ByteOrder = "BigEndian",
                        OrdemLeitura = ordemLeitura++,
                        Ativo = true,
                        DataCriacao = DateTime.UtcNow
                    };

                    novosRegistros.Add(registro);
                }
            }

            // Inserir novos registros no banco
            if (novosRegistros.Any())
            {
                await _context.ModbusConfigs.AddRangeAsync(novosRegistros);
            }
            
            await _context.SaveChangesAsync();

            var totalProcessados = novosRegistros.Count + registrosAtualizados;
            var resumo = new
            {
                total = totalProcessados,
                novos = novosRegistros.Count,
                atualizados = registrosAtualizados,
                coils = novosRegistros.Count(r => r.FuncaoModbus == "ReadCoils"),
                discreteInputs = novosRegistros.Count(r => r.FuncaoModbus == "ReadInputs"),
                holdingRegisters = novosRegistros.Count(r => r.FuncaoModbus == "ReadHoldingRegisters"),
                inputRegisters = novosRegistros.Count(r => r.FuncaoModbus == "ReadInputRegisters")
            };

            _logger.LogInformation($"Importados {novosRegistros.Count} novos registros e atualizados {registrosAtualizados} registros Modbus para {ipAddress}");

            return Ok(new
            {
                message = $"{totalProcessados} registros Modbus processados com sucesso! ({novosRegistros.Count} novos, {registrosAtualizados} atualizados)",
                resumo
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao processar JSON");
            return BadRequest(new { message = "JSON inválido", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar configurações Modbus");
            return StatusCode(500, new { message = "Erro ao importar configurações Modbus", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ModbusConfig config)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            config.DataCriacao = DateTime.UtcNow;
            _context.ModbusConfigs.Add(config);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = config.Id }, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar configuração Modbus");
            return StatusCode(500, new { message = "Erro ao criar configuração Modbus" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ModbusConfig configAtualizado)
    {
        try
        {
            var config = await _context.ModbusConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { message = "Configuração Modbus não encontrada" });
            }

            config.Nome = configAtualizado.Nome;
            config.Descricao = configAtualizado.Descricao;
            config.IpAddress = configAtualizado.IpAddress;
            config.Port = configAtualizado.Port;
            config.SlaveId = configAtualizado.SlaveId;
            config.FuncaoModbus = configAtualizado.FuncaoModbus;
            config.EnderecoRegistro = configAtualizado.EnderecoRegistro;
            config.QuantidadeRegistros = configAtualizado.QuantidadeRegistros;
            config.TipoDado = configAtualizado.TipoDado;
            config.ByteOrder = configAtualizado.ByteOrder;
            config.FatorConversao = configAtualizado.FatorConversao;
            config.Offset = configAtualizado.Offset;
            config.Unidade = configAtualizado.Unidade;
            config.OrdemLeitura = configAtualizado.OrdemLeitura;
            config.Ativo = configAtualizado.Ativo;
            config.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar configuração Modbus");
            return StatusCode(500, new { message = "Erro ao atualizar configuração Modbus" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var config = await _context.ModbusConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { message = "Configuração Modbus não encontrada" });
            }

            _context.ModbusConfigs.Remove(config);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar configuração Modbus");
            return StatusCode(500, new { message = "Erro ao deletar configuração Modbus" });
        }
    }

    [HttpPost("{id}/write")]
    public async Task<IActionResult> WriteRegister(int id, [FromBody] WriteModbusRequest request)
    {
        try
        {
            _logger.LogInformation("Recebida requisição para escrever registro {RegistroId}. Valor recebido: {Valor} (Tipo: {Tipo})", 
                id, request.Valor.ToString(), request.Valor.ValueKind);

            var config = await _context.ModbusConfigs.FindAsync(id);
            if (config == null)
            {
                _logger.LogWarning("Registro Modbus {RegistroId} não encontrado", id);
                return NotFound(new { message = "Configuração Modbus não encontrada" });
            }

            if (!config.Ativo)
            {
                _logger.LogWarning("Registro Modbus {RegistroId} ({Nome}) está inativo", id, config.Nome);
                return BadRequest(new { message = "Registro Modbus está inativo" });
            }

            _logger.LogInformation("Registro encontrado: {Nome}, Função: {FuncaoModbus}, Tipo: {TipoDado}, Endereço: {Endereco}", 
                config.Nome, config.FuncaoModbus, config.TipoDado, config.EnderecoRegistro);

            // Determina a função de escrita baseado no tipo de dado ou função atual
            string funcaoEscrita = config.FuncaoModbus;
            if (funcaoEscrita != "WriteSingleRegister" && funcaoEscrita != "WriteSingleCoil")
            {
                // Se não for função de escrita, determina baseado no tipo de dado
                if (config.TipoDado == "Boolean" || funcaoEscrita == "ReadCoils")
                {
                    funcaoEscrita = "WriteSingleCoil";
                    _logger.LogInformation("Convertendo função de {FuncaoOriginal} para WriteSingleCoil", config.FuncaoModbus);
                }
                else
                {
                    funcaoEscrita = "WriteSingleRegister";
                    _logger.LogInformation("Convertendo função de {FuncaoOriginal} para WriteSingleRegister", config.FuncaoModbus);
                }
            }

            // Converte o valor conforme o tipo de dado
            object valor;
            if (funcaoEscrita == "WriteSingleCoil")
            {
                // Converte JsonElement para bool
                if (request.Valor.ValueKind == JsonValueKind.True)
                {
                    valor = true;
                }
                else if (request.Valor.ValueKind == JsonValueKind.False)
                {
                    valor = false;
                }
                else if (request.Valor.ValueKind == JsonValueKind.String)
                {
                    valor = bool.Parse(request.Valor.GetString() ?? "false");
                }
                else if (request.Valor.ValueKind == JsonValueKind.Number)
                {
                    valor = request.Valor.GetInt32() != 0;
                }
                else
                {
                    valor = request.Valor.GetBoolean();
                }
                _logger.LogInformation("Valor convertido para bool: {Valor}", valor);
            }
            else
            {
                // Converte JsonElement para ushort
                if (request.Valor.ValueKind == JsonValueKind.Number)
                {
                    valor = (ushort)request.Valor.GetUInt16();
                }
                else if (request.Valor.ValueKind == JsonValueKind.String)
                {
                    valor = ushort.Parse(request.Valor.GetString() ?? "0");
                }
                else
                {
                    valor = (ushort)request.Valor.GetInt32();
                }
                _logger.LogInformation("Valor convertido para ushort: {Valor}", valor);
            }

            // Cria uma cópia temporária da config com a função de escrita correta
            var configTemp = new ModbusConfig
            {
                Id = config.Id,
                Nome = config.Nome,
                Descricao = config.Descricao,
                IpAddress = config.IpAddress,
                Port = config.Port,
                SlaveId = config.SlaveId,
                FuncaoModbus = funcaoEscrita,
                EnderecoRegistro = config.EnderecoRegistro,
                QuantidadeRegistros = config.QuantidadeRegistros,
                TipoDado = config.TipoDado,
                ByteOrder = config.ByteOrder,
                FatorConversao = config.FatorConversao,
                Offset = config.Offset,
                Unidade = config.Unidade,
                OrdemLeitura = config.OrdemLeitura,
                Ativo = config.Ativo
            };

            var sucesso = await _modbusService.EscreverRegistroAsync(configTemp, valor);

            if (sucesso)
            {
                _logger.LogInformation("Valor {Valor} escrito no registro Modbus {RegistroId} ({Nome})", valor, id, config.Nome);
                return Ok(new { message = "Valor escrito com sucesso", valor });
            }
            else
            {
                return StatusCode(500, new { message = "Erro ao escrever valor no registro Modbus" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever registro Modbus {RegistroId}", id);
            return StatusCode(500, new { message = "Erro ao escrever registro Modbus", error = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchByName([FromQuery] string nome)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                return BadRequest(new { message = "Parâmetro 'nome' é obrigatório" });
            }

            // Converte para minúsculas para busca case-insensitive compatível com EF Core
            var nomeLower = nome.ToLower();

            var configs = await _context.ModbusConfigs
                .Where(c => c.Ativo)
                .Where(c => c.Nome.ToLower().Contains(nomeLower) || 
                           (c.Descricao != null && c.Descricao.ToLower().Contains(nomeLower)))
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar configurações Modbus por nome: {Erro}", ex.Message);
            return StatusCode(500, new { message = "Erro ao buscar configurações Modbus", error = ex.Message });
        }
    }

    /// <summary>
    /// Lista variáveis HoldingRegister (para escrita de word)
    /// </summary>
    [HttpGet("holding-registers")]
    public async Task<IActionResult> GetHoldingRegisters()
    {
        try
        {
            var configs = await _context.ModbusConfigs
                .Where(c => c.Ativo && c.FuncaoModbus == "ReadHoldingRegisters")
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome, c.Descricao, c.EnderecoRegistro, c.TipoDado })
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar HoldingRegisters");
            return StatusCode(500, new { message = "Erro ao listar HoldingRegisters", error = ex.Message });
        }
    }

    /// <summary>
    /// Lista variáveis InputRegister (para leitura)
    /// </summary>
    [HttpGet("input-registers")]
    public async Task<IActionResult> GetInputRegisters()
    {
        try
        {
            var configs = await _context.ModbusConfigs
                .Where(c => c.Ativo && c.FuncaoModbus == "ReadInputRegisters")
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome, c.Descricao, c.EnderecoRegistro, c.TipoDado })
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar InputRegisters");
            return StatusCode(500, new { message = "Erro ao listar InputRegisters", error = ex.Message });
        }
    }

    /// <summary>
    /// Lista variáveis Coil (para saída coil)
    /// </summary>
    [HttpGet("coils")]
    public async Task<IActionResult> GetCoils()
    {
        try
        {
            var configs = await _context.ModbusConfigs
                .Where(c => c.Ativo && c.FuncaoModbus == "ReadCoils")
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome, c.Descricao, c.EnderecoRegistro, c.TipoDado })
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar Coils");
            return StatusCode(500, new { message = "Erro ao listar Coils", error = ex.Message });
        }
    }

    /// <summary>
    /// Lista variáveis InputDiscrete (para leitura de bit)
    /// </summary>
    [HttpGet("discrete-inputs")]
    public async Task<IActionResult> GetDiscreteInputs()
    {
        try
        {
            var configs = await _context.ModbusConfigs
                .Where(c => c.Ativo && c.FuncaoModbus == "ReadInputs")
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome, c.Descricao, c.EnderecoRegistro, c.TipoDado })
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar DiscreteInputs");
            return StatusCode(500, new { message = "Erro ao listar DiscreteInputs", error = ex.Message });
        }
    }

    [HttpGet("{id}/read")]
    public async Task<IActionResult> ReadRegister(int id)
    {
        try
        {
            var config = await _context.ModbusConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { message = "Configuração Modbus não encontrada" });
            }

            if (!config.Ativo)
            {
                return BadRequest(new { message = "Registro Modbus está inativo" });
            }

            var valor = await _modbusService.LerRegistroAsync(id);

            if (valor == null)
            {
                return StatusCode(500, new { message = "Erro ao ler registro Modbus" });
            }

            return Ok(new { valor });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler registro Modbus {RegistroId}", id);
            return StatusCode(500, new { message = "Erro ao ler registro Modbus", error = ex.Message });
        }
    }

    [HttpPost("motor/ligar")]
    public async Task<IActionResult> LigarMotor()
    {
        return await ExecutarComandoMotor("ligar");
    }

    [HttpPost("motor/desligar")]
    public async Task<IActionResult> DesligarMotor()
    {
        return await ExecutarComandoMotor("desligar");
    }

    [HttpPost("radiador/ligar")]
    public async Task<IActionResult> LigarRadiador()
    {
        return await ExecutarComandoRadiador("ligar");
    }

    [HttpPost("radiador/desligar")]
    public async Task<IActionResult> DesligarRadiador()
    {
        return await ExecutarComandoRadiador("desligar");
    }

    [HttpPost("camara/avanca/ligar")]
    public async Task<IActionResult> LigarAvanca()
    {
        return await ExecutarComandoCamara("avanca");
    }

    [HttpPost("camara/recua/ligar")]
    public async Task<IActionResult> LigarRecua()
    {
        return await ExecutarComandoCamara("recua");
    }

    [HttpGet("registro/rodando")]
    public async Task<IActionResult> VerificarRegistroRodando()
    {
        try
        {
            var registro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo);

            if (registro == null)
            {
                return NotFound(new { message = "Registro Modbus 'REGISTRO_RODANDO' não encontrado ou inativo" });
            }

            var valor = await _modbusService.LerRegistroAsync(registro.Id);
            bool rodando = valor is bool boolVal ? boolVal : (valor?.ToString() == "1" || valor?.ToString() == "True");
            string valorStr = valor?.ToString() ?? string.Empty;

            return Ok(new { 
                rodando,
                valor = valorStr
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar REGISTRO_RODANDO");
            return StatusCode(500, new { message = "Erro ao verificar REGISTRO_RODANDO", error = ex.Message });
        }
    }

    [HttpPost("registro/iniciar")]
    public async Task<IActionResult> IniciarRegistro()
    {
        try
        {
            // Busca os registros necessários
            var botaoRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

            // Busca o registro de LEITURA do status do registro (ReadInputs - Input Discrete)
            var statusRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs");

            if (botaoRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus 'INICIA_REGISTRO' não encontrado ou inativo" });
            }

            if (statusRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus 'REGISTRO_RODANDO' (ReadInputs) não encontrado ou inativo" });
            }

            _logger.LogInformation("Usando registro de leitura REGISTRO_RODANDO: ID={RegistroId}, Função={FuncaoModbus}, Endereço={Endereco}", 
                statusRegistro.Id, statusRegistro.FuncaoModbus, statusRegistro.EnderecoRegistro);

            // Lê o status atual do registro
            var statusAtual = await _modbusService.LerRegistroAsync(statusRegistro.Id);
            bool statusAtualBool = statusAtual is bool boolVal ? boolVal : (statusAtual?.ToString() == "1" || statusAtual?.ToString() == "True");

            bool statusEsperado = true; // Esperamos que REGISTRO_RODANDO fique true

            // Se já está rodando, retorna sucesso
            if (statusAtualBool == statusEsperado)
            {
                return Ok(new { 
                    message = "Registro já está rodando",
                    rodando = true,
                    sucesso = true
                });
            }

            // Determina função de escrita
            string funcaoEscrita = botaoRegistro.TipoDado == "Boolean" || botaoRegistro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            // Cria config temporária para escrita
            var configTemp = new ModbusConfig
            {
                Id = botaoRegistro.Id,
                Nome = botaoRegistro.Nome,
                IpAddress = botaoRegistro.IpAddress,
                Port = botaoRegistro.Port,
                SlaveId = botaoRegistro.SlaveId,
                FuncaoModbus = funcaoEscrita,
                EnderecoRegistro = botaoRegistro.EnderecoRegistro,
                QuantidadeRegistros = botaoRegistro.QuantidadeRegistros,
                TipoDado = botaoRegistro.TipoDado,
                Ativo = botaoRegistro.Ativo
            };

            // 1. Ativa o botão (mantém ativado até receber confirmação)
            _logger.LogInformation("Ativando INICIA_REGISTRO (registro {RegistroId})", botaoRegistro.Id);
            var ativado = await _modbusService.EscreverRegistroAsync(configTemp, true);
            if (!ativado)
            {
                return StatusCode(500, new { message = "Erro ao ativar INICIA_REGISTRO" });
            }

            // 2. Aguarda um tempo inicial para o CLP processar o comando
            await Task.Delay(300);

            // 3. Verifica se o status mudou (aguarda confirmação via REGISTRO_RODANDO)
            // O botão permanece ativado enquanto aguarda a confirmação
            var timeout = TimeSpan.FromSeconds(2);
            var intervalo = TimeSpan.FromMilliseconds(200);
            var inicio = DateTime.UtcNow;
            bool statusAlterado = false;
            object? ultimoStatusLido = null;
            int tentativasLeitura = 0;

            _logger.LogInformation("Aguardando confirmação do comando via REGISTRO_RODANDO. Status esperado: {StatusEsperado}", statusEsperado);

            while (DateTime.UtcNow - inicio < timeout)
            {
                await Task.Delay(intervalo);
                tentativasLeitura++;

                try
                {
                    var novoStatus = await _modbusService.LerRegistroAsync(statusRegistro.Id);
                    ultimoStatusLido = novoStatus;

                    if (novoStatus == null)
                    {
                        _logger.LogWarning("Leitura do status do registro retornou null na tentativa {Tentativa}", tentativasLeitura);
                        continue;
                    }

                    bool novoStatusBool = novoStatus is bool boolVal2 ? boolVal2 : (novoStatus?.ToString() == "1" || novoStatus?.ToString() == "True");

                    _logger.LogDebug("Tentativa {Tentativa}: Status lido = {StatusLido} (bool: {StatusBool}), Esperado: {StatusEsperado}", 
                        tentativasLeitura, novoStatus, novoStatusBool, statusEsperado);

                    // REGISTRO_RODANDO confirma que o comando foi executado
                    if (novoStatusBool == statusEsperado)
                    {
                        statusAlterado = true;
                        _logger.LogInformation("Comando confirmado via REGISTRO_RODANDO após {Tentativas} tentativas e {TempoTotal}ms", 
                            tentativasLeitura, (DateTime.UtcNow - inicio).TotalMilliseconds);
                        break;
                    }
                }
                catch (Exception exLeitura)
                {
                    _logger.LogWarning(exLeitura, "Erro ao ler status do registro na tentativa {Tentativa}", tentativasLeitura);
                    // Continua tentando mesmo se houver erro na leitura
                }
            }

            // 4. Desativa o botão após receber confirmação ou timeout
            _logger.LogInformation("Desativando INICIA_REGISTRO (registro {RegistroId})", botaoRegistro.Id);
            var desativado = await _modbusService.EscreverRegistroAsync(configTemp, false);
            if (!desativado)
            {
                _logger.LogWarning("Aviso: Não foi possível desativar INICIA_REGISTRO");
            }

            // 5. Retorna resultado baseado na confirmação do REGISTRO_RODANDO
            if (statusAlterado)
            {
                _logger.LogInformation("Registro iniciado com sucesso");
                return Ok(new { 
                    message = "Registro iniciado com sucesso!",
                    rodando = true,
                    sucesso = true
                });
            }
            else
            {
                var tempoDecorrido = (DateTime.UtcNow - inicio).TotalSeconds;
                _logger.LogWarning("Timeout: Registro não respondeu ao comando de iniciar após {TempoDecorrido}s e {Tentativas} tentativas. Status inicial: {StatusInicial}, Último status lido: {UltimoStatus}", 
                    tempoDecorrido, tentativasLeitura, statusAtualBool, ultimoStatusLido);
                
                return StatusCode(500, new { 
                    message = $"Timeout: Registro não respondeu ao comando de iniciar após {tempoDecorrido:F1} segundos. Verifique o sistema e a comunicação Modbus.",
                    rodando = false,
                    ultimoStatusLido = ultimoStatusLido?.ToString(),
                    tentativas = tentativasLeitura,
                    sucesso = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar registro");
            return StatusCode(500, new { message = "Erro ao iniciar registro", error = ex.Message });
        }
    }

    [HttpPost("pressao-carga")]
    public async Task<IActionResult> EscreverPressaoCarga([FromBody] double pressao)
    {
        try
        {
            var registro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "PRESSAO_CARGA" && m.Ativo);

            if (registro == null)
            {
                return NotFound(new { message = "Registro Modbus 'PRESSAO_CARGA' não encontrado ou inativo" });
            }

            // Determina função de escrita
            string funcaoEscrita = registro.TipoDado == "Boolean" || registro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            var configTemp = new ModbusConfig
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

            // Converte pressão para o tipo adequado
            object valor;
            if (funcaoEscrita == "WriteSingleRegister")
            {
                valor = (ushort)Math.Round(pressao);
            }
            else
            {
                valor = pressao > 0;
            }

            var sucesso = await _modbusService.EscreverRegistroAsync(configTemp, valor);

            if (sucesso)
            {
                _logger.LogInformation("Pressão de carga {Pressao} escrita com sucesso", pressao);
                return Ok(new { message = $"Pressão de carga {pressao} escrita com sucesso", sucesso = true });
            }
            else
            {
                return StatusCode(500, new { message = "Erro ao escrever pressão de carga" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever pressão de carga");
            return StatusCode(500, new { message = "Erro ao escrever pressão de carga", error = ex.Message });
        }
    }

    [HttpPost("tempo-carga")]
    public async Task<IActionResult> EscreverTempoCarga([FromBody] double tempo)
    {
        try
        {
            var registro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "TEMPO_CARGA" && m.Ativo);

            if (registro == null)
            {
                return NotFound(new { message = "Registro Modbus 'TEMPO_CARGA' não encontrado ou inativo" });
            }

            // Determina função de escrita
            string funcaoEscrita = registro.TipoDado == "Boolean" || registro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            var configTemp = new ModbusConfig
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

            // Converte tempo para o tipo adequado
            object valor;
            if (funcaoEscrita == "WriteSingleRegister")
            {
                valor = (ushort)Math.Round(tempo);
            }
            else
            {
                valor = tempo > 0;
            }

            var sucesso = await _modbusService.EscreverRegistroAsync(configTemp, valor);

            if (sucesso)
            {
                _logger.LogInformation("Tempo de carga {Tempo} escrito com sucesso", tempo);
                return Ok(new { message = $"Tempo de carga {tempo} escrito com sucesso", sucesso = true });
            }
            else
            {
                return StatusCode(500, new { message = "Erro ao escrever tempo de carga" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever tempo de carga");
            return StatusCode(500, new { message = "Erro ao escrever tempo de carga", error = ex.Message });
        }
    }

    private async Task<IActionResult> ExecutarComandoCamara(string acao)
    {
        try
        {
            var nomeBotao = acao == "avanca" ? "BOTAO_AVANCA_IHM" : "BOTAO_RECUA_IHM";
            var botaoRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == nomeBotao && m.Ativo);

            if (botaoRegistro == null)
            {
                return NotFound(new { message = $"Registro Modbus '{nomeBotao}' não encontrado ou inativo" });
            }

            // Determina função de escrita
            string funcaoEscrita = botaoRegistro.TipoDado == "Boolean" || botaoRegistro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            var configTemp = new ModbusConfig
            {
                Id = botaoRegistro.Id,
                Nome = botaoRegistro.Nome,
                IpAddress = botaoRegistro.IpAddress,
                Port = botaoRegistro.Port,
                SlaveId = botaoRegistro.SlaveId,
                FuncaoModbus = funcaoEscrita,
                EnderecoRegistro = botaoRegistro.EnderecoRegistro,
                QuantidadeRegistros = botaoRegistro.QuantidadeRegistros,
                TipoDado = botaoRegistro.TipoDado,
                Ativo = botaoRegistro.Ativo
            };

            // Ativa o botão (mantém ativado)
            _logger.LogInformation("Ativando {Acao} (registro {RegistroId})", acao, botaoRegistro.Id);
            var ativado = await _modbusService.EscreverRegistroAsync(configTemp, true);
            if (!ativado)
            {
                return StatusCode(500, new { message = $"Erro ao ativar {acao}" });
            }

            _logger.LogInformation("{Acao} ativado com sucesso", acao);
            return Ok(new { 
                message = $"{acao} ativado com sucesso!",
                sucesso = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando {Acao}", acao);
            return StatusCode(500, new { message = $"Erro ao {acao}", error = ex.Message });
        }
    }

    private async Task<IActionResult> ExecutarComandoMotor(string acao)
    {
        try
        {
            // Busca os registros necessários
            var botaoRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == (acao == "ligar" ? "BOTAO_LIGA_MOTOR" : "BOTAO_DESLIGA_MOTOR") && m.Ativo);

            // Busca o registro de LEITURA do status do motor (ReadInputs - Input Discrete)
            // Existem dois registros MOTOR_BOMBA: um para leitura (ReadInputs) e outro para escrita (ReadCoils)
            var statusRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "MOTOR_BOMBA" && m.Ativo && m.FuncaoModbus == "ReadInputs");

            if (botaoRegistro == null)
            {
                return NotFound(new { message = $"Registro Modbus 'BOTAO_{(acao == "ligar" ? "LIGA" : "DESLIGA")}_MOTOR' não encontrado ou inativo" });
            }

            if (statusRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus 'MOTOR_BOMBA' (ReadInputs) não encontrado ou inativo" });
            }

            _logger.LogInformation("Usando registro de leitura MOTOR_BOMBA: ID={RegistroId}, Função={FuncaoModbus}, Endereço={Endereco}", 
                statusRegistro.Id, statusRegistro.FuncaoModbus, statusRegistro.EnderecoRegistro);

            // Lê o status atual do motor
            var statusAtual = await _modbusService.LerRegistroAsync(statusRegistro.Id);
            bool statusAtualBool = statusAtual is bool boolVal ? boolVal : (statusAtual?.ToString() == "1" || statusAtual?.ToString() == "True");

            bool statusEsperado = acao == "ligar";

            // Se já está no estado desejado, retorna sucesso
            if (statusAtualBool == statusEsperado)
            {
                return Ok(new { 
                    message = $"Motor já está {(statusEsperado ? "ligado" : "desligado")}",
                    status = statusEsperado,
                    sucesso = true
                });
            }

            // Determina função de escrita
            string funcaoEscrita = botaoRegistro.TipoDado == "Boolean" || botaoRegistro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            // Cria config temporária para escrita
            var configTemp = new ModbusConfig
            {
                Id = botaoRegistro.Id,
                Nome = botaoRegistro.Nome,
                IpAddress = botaoRegistro.IpAddress,
                Port = botaoRegistro.Port,
                SlaveId = botaoRegistro.SlaveId,
                FuncaoModbus = funcaoEscrita,
                EnderecoRegistro = botaoRegistro.EnderecoRegistro,
                QuantidadeRegistros = botaoRegistro.QuantidadeRegistros,
                TipoDado = botaoRegistro.TipoDado,
                Ativo = botaoRegistro.Ativo
            };

            // 1. Ativa o botão (mantém ativado até receber confirmação)
            _logger.LogInformation("Ativando botão {Acao} motor (registro {RegistroId})", acao, botaoRegistro.Id);
            var ativado = await _modbusService.EscreverRegistroAsync(configTemp, true);
            if (!ativado)
            {
                return StatusCode(500, new { message = $"Erro ao ativar botão {acao} motor" });
            }

            // 2. Aguarda um tempo inicial para o CLP processar o comando
            await Task.Delay(500);

            // 3. Verifica se o status mudou (aguarda confirmação via MOTOR_BOMBA)
            // O botão permanece ativado enquanto aguarda a confirmação
            var timeout = TimeSpan.FromSeconds(5); // Aumentado de 2 para 5 segundos
            var intervalo = TimeSpan.FromMilliseconds(300); // Aumentado de 200 para 300ms
            var inicio = DateTime.UtcNow;
            bool statusAlterado = false;
            object? ultimoStatusLido = null;
            int tentativasLeitura = 0;

            _logger.LogInformation("Aguardando confirmação do comando {Acao} via MOTOR_BOMBA. Status esperado: {StatusEsperado}", acao, statusEsperado);

            while (DateTime.UtcNow - inicio < timeout)
            {
                await Task.Delay(intervalo);
                tentativasLeitura++;

                try
                {
                    var novoStatus = await _modbusService.LerRegistroAsync(statusRegistro.Id);
                    ultimoStatusLido = novoStatus;

                    if (novoStatus == null)
                    {
                        _logger.LogWarning("Leitura do status do motor retornou null na tentativa {Tentativa}", tentativasLeitura);
                        continue;
                    }

                    bool novoStatusBool = novoStatus is bool boolVal2 ? boolVal2 : (novoStatus?.ToString() == "1" || novoStatus?.ToString() == "True");

                    _logger.LogInformation("Tentativa {Tentativa}: Status lido = {StatusLido} (bool: {StatusBool}), Esperado: {StatusEsperado}", 
                        tentativasLeitura, novoStatus, novoStatusBool, statusEsperado);

                    // MOTOR_BOMBA confirma que o comando foi executado
                    if (novoStatusBool == statusEsperado)
                    {
                        statusAlterado = true;
                        _logger.LogInformation("Comando confirmado via MOTOR_BOMBA após {Tentativas} tentativas e {TempoTotal}ms", 
                            tentativasLeitura, (DateTime.UtcNow - inicio).TotalMilliseconds);
                        break;
                    }
                }
                catch (Exception exLeitura)
                {
                    _logger.LogWarning(exLeitura, "Erro ao ler status do motor na tentativa {Tentativa}", tentativasLeitura);
                    // Continua tentando mesmo se houver erro na leitura
                }
            }

            // 4. Desativa o botão após receber confirmação ou timeout
            _logger.LogInformation("Desativando botão {Acao} motor (registro {RegistroId})", acao, botaoRegistro.Id);
            var desativado = await _modbusService.EscreverRegistroAsync(configTemp, false);
            if (!desativado)
            {
                _logger.LogWarning("Aviso: Não foi possível desativar botão {Acao} motor", acao);
            }

            // 5. Retorna resultado baseado na confirmação do MOTOR_BOMBA
            if (statusAlterado)
            {
                _logger.LogInformation("Motor {Acao} com sucesso", acao == "ligar" ? "ligado" : "desligado");
                return Ok(new { 
                    message = $"Motor {(statusEsperado ? "ligado" : "desligado")} com sucesso!",
                    status = statusEsperado,
                    sucesso = true
                });
            }
            else
            {
                var tempoDecorrido = (DateTime.UtcNow - inicio).TotalSeconds;
                _logger.LogWarning("Timeout: Motor não respondeu ao comando de {Acao} após {TempoDecorrido}s e {Tentativas} tentativas. Status inicial: {StatusInicial}, Último status lido: {UltimoStatus}", 
                    acao, tempoDecorrido, tentativasLeitura, statusAtualBool, ultimoStatusLido);
                
                return StatusCode(500, new { 
                    message = $"Timeout: Motor não respondeu ao comando de {acao} após {tempoDecorrido:F1} segundos. Verifique o sistema e a comunicação Modbus.",
                    status = statusAtualBool,
                    ultimoStatusLido = ultimoStatusLido?.ToString(),
                    tentativas = tentativasLeitura,
                    sucesso = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando {Acao} motor", acao);
            return StatusCode(500, new { message = $"Erro ao {acao} motor", error = ex.Message });
        }
    }

    private async Task<IActionResult> ExecutarComandoRadiador(string acao)
    {
        try
        {
            // Busca os registros necessários
            var botaoRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == (acao == "ligar" ? "BOTAO_LIGA_RADIADOR" : "BOTAO_DESLIGA_RADIADOR") && m.Ativo);

            if (botaoRegistro == null)
            {
                return NotFound(new { message = $"Registro Modbus 'BOTAO_{(acao == "ligar" ? "LIGA" : "DESLIGA")}_RADIADOR' não encontrado ou inativo" });
            }

            // Determina função de escrita
            string funcaoEscrita = botaoRegistro.TipoDado == "Boolean" || botaoRegistro.FuncaoModbus == "ReadCoils" 
                ? "WriteSingleCoil" 
                : "WriteSingleRegister";

            // Cria config temporária para escrita
            var configTemp = new ModbusConfig
            {
                Id = botaoRegistro.Id,
                Nome = botaoRegistro.Nome,
                IpAddress = botaoRegistro.IpAddress,
                Port = botaoRegistro.Port,
                SlaveId = botaoRegistro.SlaveId,
                FuncaoModbus = funcaoEscrita,
                EnderecoRegistro = botaoRegistro.EnderecoRegistro,
                QuantidadeRegistros = botaoRegistro.QuantidadeRegistros,
                TipoDado = botaoRegistro.TipoDado,
                Ativo = botaoRegistro.Ativo
            };

            // 1. Ativa o botão
            _logger.LogInformation("Ativando botão {Acao} radiador (registro {RegistroId})", acao, botaoRegistro.Id);
            var ativado = await _modbusService.EscreverRegistroAsync(configTemp, true);
            if (!ativado)
            {
                return StatusCode(500, new { message = $"Erro ao ativar botão {acao} radiador" });
            }

            // 2. Aguarda processamento do CLP
            await Task.Delay(100);

            // 3. Desativa o botão
            _logger.LogInformation("Desativando botão {Acao} radiador (registro {RegistroId})", acao, botaoRegistro.Id);
            await _modbusService.EscreverRegistroAsync(configTemp, false);

            _logger.LogInformation("Radiador {Acao} com sucesso", acao == "ligar" ? "ligado" : "desligado");
            return Ok(new { 
                message = $"Radiador {(acao == "ligar" ? "ligado" : "desligado")} com sucesso!",
                sucesso = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando {Acao} radiador", acao);
            return StatusCode(500, new { message = $"Erro ao {acao} radiador", error = ex.Message });
        }
    }
}

// DTO para escrita Modbus
public class WriteModbusRequest
{
    [JsonPropertyName("valor")]
    public JsonElement Valor { get; set; }
}

// DTO para importação
public class ImportModbusRequest
{
    public string JsonContent { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public byte? SlaveId { get; set; }
    public bool? ReplaceExisting { get; set; }
}
