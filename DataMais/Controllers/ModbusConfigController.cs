using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using System.Text.Json;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModbusConfigController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<ModbusConfigController> _logger;

    public ModbusConfigController(DataMaisDbContext context, ILogger<ModbusConfigController> logger)
    {
        _context = context;
        _logger = logger;
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
