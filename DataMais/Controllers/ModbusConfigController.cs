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

            // Inserir no banco
            await _context.ModbusConfigs.AddRangeAsync(novosRegistros);
            await _context.SaveChangesAsync();

            var resumo = new
            {
                total = novosRegistros.Count,
                coils = novosRegistros.Count(r => r.FuncaoModbus == "ReadCoils"),
                discreteInputs = novosRegistros.Count(r => r.FuncaoModbus == "ReadInputs"),
                holdingRegisters = novosRegistros.Count(r => r.FuncaoModbus == "ReadHoldingRegisters"),
                inputRegisters = novosRegistros.Count(r => r.FuncaoModbus == "ReadInputRegisters")
            };

            _logger.LogInformation($"Importados {novosRegistros.Count} registros Modbus para {ipAddress}");

            return Ok(new
            {
                message = $"{novosRegistros.Count} registros Modbus importados com sucesso!",
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

    private async Task<IActionResult> ExecutarComandoMotor(string acao)
    {
        try
        {
            // Busca os registros necessários
            var botaoRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == (acao == "ligar" ? "BOTAO_LIGA_MOTOR" : "BOTAO_DESLIGA_MOTOR") && m.Ativo);

            var statusRegistro = await _context.ModbusConfigs
                .FirstOrDefaultAsync(m => m.Nome == "MOTOR_BOMBA" && m.Ativo);

            if (botaoRegistro == null)
            {
                return NotFound(new { message = $"Registro Modbus 'BOTAO_{(acao == "ligar" ? "LIGA" : "DESLIGA")}_MOTOR' não encontrado ou inativo" });
            }

            if (statusRegistro == null)
            {
                return NotFound(new { message = "Registro Modbus 'MOTOR_BOMBA' não encontrado ou inativo" });
            }

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

            // 1. Ativa o botão
            _logger.LogInformation("Ativando botão {Acao} motor (registro {RegistroId})", acao, botaoRegistro.Id);
            var ativado = await _modbusService.EscreverRegistroAsync(configTemp, true);
            if (!ativado)
            {
                return StatusCode(500, new { message = $"Erro ao ativar botão {acao} motor" });
            }

            // 2. Aguarda processamento do CLP
            await Task.Delay(100);

            // 3. Desativa o botão
            _logger.LogInformation("Desativando botão {Acao} motor (registro {RegistroId})", acao, botaoRegistro.Id);
            await _modbusService.EscreverRegistroAsync(configTemp, false);

            // 4. Verifica se o status mudou (timeout de 5 segundos, verifica a cada 200ms)
            var timeout = TimeSpan.FromSeconds(5);
            var intervalo = TimeSpan.FromMilliseconds(200);
            var inicio = DateTime.UtcNow;
            bool statusAlterado = false;

            while (DateTime.UtcNow - inicio < timeout)
            {
                await Task.Delay(intervalo);
                var novoStatus = await _modbusService.LerRegistroAsync(statusRegistro.Id);
                bool novoStatusBool = novoStatus is bool boolVal2 ? boolVal2 : (novoStatus?.ToString() == "1" || novoStatus?.ToString() == "True");

                if (novoStatusBool == statusEsperado)
                {
                    statusAlterado = true;
                    break;
                }
            }

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
                _logger.LogWarning("Timeout: Motor não respondeu ao comando de {Acao}", acao);
                return StatusCode(500, new { 
                    message = $"Timeout: Motor não respondeu ao comando de {acao}. Verifique o sistema.",
                    status = statusAtualBool,
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
