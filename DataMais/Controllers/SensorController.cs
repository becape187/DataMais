using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<SensorController> _logger;
    private readonly ModbusService _modbusService;

    public SensorController(DataMaisDbContext context, ILogger<SensorController> logger, ModbusService modbusService)
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
            var sensores = await _context.Sensores
                .Include(s => s.ModbusConfig)
                .Select(s => new
                {
                    s.Id,
                    s.Nome,
                    s.Descricao,
                    s.Tipo,
                    s.Unidade,
                    s.InputMin,
                    s.OutputMin,
                    s.InputMax,
                    s.OutputMax,
                    s.ModbusConfigId,
                    modbusConfig = s.ModbusConfig != null ? new
                    {
                        s.ModbusConfig.Id,
                        s.ModbusConfig.Nome,
                        s.ModbusConfig.EnderecoRegistro,
                        s.ModbusConfig.FatorConversao,
                        s.ModbusConfig.Offset
                    } : null,
                    s.Ativo,
                    dataCriacao = s.DataCriacao.ToString("yyyy-MM-ddTHH:mm:ss"),
                    dataAtualizacao = s.DataAtualizacao.HasValue ? s.DataAtualizacao.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
                })
                .ToListAsync();

            return Ok(sensores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter sensores");
            return StatusCode(500, new { message = "Erro ao obter sensores" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var sensor = await _context.Sensores
                .Include(s => s.ModbusConfig)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sensor == null)
            {
                return NotFound(new { message = "Sensor não encontrado" });
            }

            var result = new
            {
                sensor.Id,
                sensor.Nome,
                sensor.Descricao,
                sensor.Tipo,
                sensor.Unidade,
                sensor.InputMin,
                sensor.OutputMin,
                sensor.InputMax,
                sensor.OutputMax,
                sensor.ModbusConfigId,
                modbusConfig = sensor.ModbusConfig != null ? new
                {
                    sensor.ModbusConfig.Id,
                    sensor.ModbusConfig.Nome,
                    sensor.ModbusConfig.EnderecoRegistro,
                    sensor.ModbusConfig.FatorConversao,
                    sensor.ModbusConfig.Offset
                } : null,
                sensor.Ativo,
                dataCriacao = sensor.DataCriacao.ToString("yyyy-MM-ddTHH:mm:ss"),
                dataAtualizacao = sensor.DataAtualizacao.HasValue ? sensor.DataAtualizacao.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter sensor");
            return StatusCode(500, new { message = "Erro ao obter sensor" });
        }
    }

    [HttpGet("nome/{nome}")]
    public async Task<IActionResult> GetByNome(string nome)
    {
        try
        {
            var sensor = await _context.Sensores
                .Include(s => s.ModbusConfig)
                .FirstOrDefaultAsync(s => s.Nome == nome && s.Ativo);

            if (sensor == null)
            {
                return NotFound(new { message = $"Sensor '{nome}' não encontrado" });
            }

            var result = new
            {
                sensor.Id,
                sensor.Nome,
                sensor.Descricao,
                sensor.Tipo,
                sensor.Unidade,
                sensor.InputMin,
                sensor.OutputMin,
                sensor.InputMax,
                sensor.OutputMax,
                sensor.ModbusConfigId,
                modbusConfig = sensor.ModbusConfig != null ? new
                {
                    sensor.ModbusConfig.Id,
                    sensor.ModbusConfig.Nome,
                    sensor.ModbusConfig.EnderecoRegistro,
                    sensor.ModbusConfig.FatorConversao,
                    sensor.ModbusConfig.Offset
                } : null,
                sensor.Ativo
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter sensor por nome");
            return StatusCode(500, new { message = "Erro ao obter sensor por nome" });
        }
    }

    [HttpGet("tipo/{tipo}")]
    public async Task<IActionResult> GetByTipo(string tipo)
    {
        try
        {
            var sensores = await _context.Sensores
                .Include(s => s.ModbusConfig)
                .Where(s => s.Tipo.ToLower() == tipo.ToLower() && s.Ativo)
                .Select(s => new
                {
                    s.Id,
                    s.Nome,
                    s.Descricao,
                    s.Tipo,
                    s.Unidade,
                    s.InputMin,
                    s.OutputMin,
                    s.InputMax,
                    s.OutputMax,
                    s.ModbusConfigId,
                    modbusConfig = s.ModbusConfig != null ? new
                    {
                        s.ModbusConfig.Id,
                        s.ModbusConfig.Nome,
                        s.ModbusConfig.EnderecoRegistro,
                        s.ModbusConfig.FatorConversao,
                        s.ModbusConfig.Offset
                    } : null,
                    s.Ativo
                })
                .ToListAsync();

            return Ok(sensores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter sensores por tipo");
            return StatusCode(500, new { message = "Erro ao obter sensores por tipo" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SensorCreateDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verifica se o ModbusConfig existe (se fornecido)
            if (dto.ModbusConfigId.HasValue)
            {
                var modbusConfig = await _context.ModbusConfigs.FindAsync(dto.ModbusConfigId.Value);
                if (modbusConfig == null)
                {
                    return BadRequest(new { message = "Configuração Modbus não encontrada" });
                }
            }

            var sensor = new Sensor
            {
                Nome = dto.Nome,
                Descricao = dto.Descricao,
                Tipo = dto.Tipo,
                Unidade = dto.Unidade,
                ModbusConfigId = dto.ModbusConfigId,
                InputMin = dto.InputMin,
                OutputMin = dto.OutputMin,
                InputMax = dto.InputMax,
                OutputMax = dto.OutputMax,
                Ativo = dto.Ativo ?? true,
                DataCriacao = DateTime.UtcNow
            };

            _context.Sensores.Add(sensor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = sensor.Id }, new
            {
                sensor.Id,
                sensor.Nome,
                sensor.Descricao,
                sensor.Tipo,
                sensor.Unidade,
                sensor.InputMin,
                sensor.OutputMin,
                sensor.InputMax,
                sensor.OutputMax,
                sensor.ModbusConfigId,
                sensor.Ativo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar sensor");
            return StatusCode(500, new { message = "Erro ao criar sensor" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SensorUpdateDto dto)
    {
        try
        {
            var sensor = await _context.Sensores.FindAsync(id);
            if (sensor == null)
            {
                return NotFound(new { message = "Sensor não encontrado" });
            }

            // Verifica se o ModbusConfig existe (se fornecido)
            if (dto.ModbusConfigId.HasValue)
            {
                var modbusConfig = await _context.ModbusConfigs.FindAsync(dto.ModbusConfigId.Value);
                if (modbusConfig == null)
                {
                    return BadRequest(new { message = "Configuração Modbus não encontrada" });
                }
            }

            sensor.Nome = dto.Nome ?? sensor.Nome;
            sensor.Descricao = dto.Descricao ?? sensor.Descricao;
            sensor.Tipo = dto.Tipo ?? sensor.Tipo;
            sensor.Unidade = dto.Unidade ?? sensor.Unidade;
            if (dto.ModbusConfigId.HasValue)
                sensor.ModbusConfigId = dto.ModbusConfigId;
            if (dto.InputMin.HasValue)
                sensor.InputMin = dto.InputMin;
            if (dto.OutputMin.HasValue)
                sensor.OutputMin = dto.OutputMin;
            if (dto.InputMax.HasValue)
                sensor.InputMax = dto.InputMax;
            if (dto.OutputMax.HasValue)
                sensor.OutputMax = dto.OutputMax;
            if (dto.Ativo.HasValue)
                sensor.Ativo = dto.Ativo.Value;
            sensor.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                sensor.Id,
                sensor.Nome,
                sensor.Descricao,
                sensor.Tipo,
                sensor.Unidade,
                sensor.InputMin,
                sensor.OutputMin,
                sensor.InputMax,
                sensor.OutputMax,
                sensor.ModbusConfigId,
                sensor.Ativo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar sensor");
            return StatusCode(500, new { message = "Erro ao atualizar sensor" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var sensor = await _context.Sensores.FindAsync(id);
            if (sensor == null)
            {
                return NotFound(new { message = "Sensor não encontrado" });
            }

            _context.Sensores.Remove(sensor);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar sensor");
            return StatusCode(500, new { message = "Erro ao deletar sensor" });
        }
    }

    /// <summary>
    /// Lê o valor do sensor aplicando a conversão linear de calibração
    /// </summary>
    [HttpGet("{id}/read")]
    public async Task<IActionResult> ReadValue(int id)
    {
        try
        {
            var sensor = await _context.Sensores
                .Include(s => s.ModbusConfig)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sensor == null)
            {
                return NotFound(new { message = "Sensor não encontrado" });
            }

            if (!sensor.Ativo)
            {
                return BadRequest(new { message = "Sensor está inativo" });
            }

            if (!sensor.ModbusConfigId.HasValue || sensor.ModbusConfig == null)
            {
                return BadRequest(new { message = "Sensor não possui configuração Modbus associada" });
            }

            // Lê o valor bruto do Modbus
            var valorBruto = await _modbusService.LerRegistroAsync(sensor.ModbusConfigId.Value);
            
            if (valorBruto == null)
            {
                return BadRequest(new { message = "Não foi possível ler o valor do Modbus" });
            }

            decimal valorConvertido;
            
            // Converte para decimal
            if (valorBruto is decimal dec)
            {
                valorConvertido = dec;
            }
            else if (valorBruto is double dbl)
            {
                valorConvertido = (decimal)dbl;
            }
            else if (valorBruto is float flt)
            {
                valorConvertido = (decimal)flt;
            }
            else if (valorBruto is int intVal)
            {
                valorConvertido = intVal;
            }
            else if (valorBruto is uint uintVal)
            {
                valorConvertido = uintVal;
            }
            else
            {
                valorConvertido = Convert.ToDecimal(valorBruto);
            }

            // Aplica conversão linear se os pontos de calibração estiverem configurados
            if (sensor.InputMin.HasValue && sensor.OutputMin.HasValue && 
                sensor.InputMax.HasValue && sensor.OutputMax.HasValue)
            {
                var inputMin = sensor.InputMin.Value;
                var outputMin = sensor.OutputMin.Value;
                var inputMax = sensor.InputMax.Value;
                var outputMax = sensor.OutputMax.Value;

                // Evita divisão por zero
                if (inputMax != inputMin)
                {
                    // Conversão linear: Output = ((OutputMax - OutputMin) / (InputMax - InputMin)) * (Input - InputMin) + OutputMin
                    var slope = (outputMax - outputMin) / (inputMax - inputMin);
                    valorConvertido = slope * (valorConvertido - inputMin) + outputMin;
                }
            }

            return Ok(new
            {
                sensorId = sensor.Id,
                sensorNome = sensor.Nome,
                valorBruto = valorConvertido, // Retorna o valor já convertido
                valorConvertido = valorConvertido,
                unidade = sensor.Unidade ?? "bar"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler valor do sensor");
            return StatusCode(500, new { message = "Erro ao ler valor do sensor", error = ex.Message });
        }
    }
}

// DTOs
public class SensorCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tipo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Unidade { get; set; }

    public int? ModbusConfigId { get; set; }

    public decimal? InputMin { get; set; }

    public decimal? OutputMin { get; set; }

    public decimal? InputMax { get; set; }

    public decimal? OutputMax { get; set; }

    public bool? Ativo { get; set; }
}

public class SensorUpdateDto
{
    [MaxLength(100)]
    public string? Nome { get; set; }

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [MaxLength(50)]
    public string? Tipo { get; set; }

    [MaxLength(20)]
    public string? Unidade { get; set; }

    public int? ModbusConfigId { get; set; }

    public decimal? InputMin { get; set; }

    public decimal? OutputMin { get; set; }

    public decimal? InputMax { get; set; }

    public decimal? OutputMax { get; set; }

    public bool? Ativo { get; set; }
}
