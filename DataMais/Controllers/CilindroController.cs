using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CilindroController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<CilindroController> _logger;

    public CilindroController(DataMaisDbContext context, ILogger<CilindroController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("cliente/{clienteId}")]
    public async Task<IActionResult> GetByCliente(int clienteId)
    {
        try
        {
            var cilindros = await _context.Cilindros
                .Where(c => c.ClienteId == clienteId)
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Descricao,
                    c.CodigoCliente,
                    c.CodigoInterno,
                    c.Modelo,
                    c.Fabricante
                })
                .ToListAsync();

            return Ok(cilindros);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter cilindros do cliente");
            return StatusCode(500, new { message = "Erro ao obter cilindros do cliente" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var cilindro = await _context.Cilindros
                .Include(c => c.Relatorios)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cilindro == null)
            {
                return NotFound(new { message = "Cilindro não encontrado" });
            }

            var result = new
            {
                cilindro.Id,
                cilindro.Nome,
                cilindro.Descricao,
                cilindro.CodigoCliente,
                cilindro.CodigoInterno,
                cilindro.Modelo,
                cilindro.Fabricante,
                dataFabricacao = cilindro.DataFabricacao?.ToString("yyyy-MM-dd"),
                diametroInterno = cilindro.DiametroInterno,
                comprimentoHaste = cilindro.ComprimentoHaste,
                diametroHaste = cilindro.DiametroHaste,
                maximaPressaoA = cilindro.MaximaPressaoA,
                maximaPressaoB = cilindro.MaximaPressaoB,
                preCargaA = cilindro.PreCargaA,
                cargaNominalA = cilindro.CargaNominalA,
                tempoRampaSubidaA = cilindro.TempoRampaSubidaA,
                tempoDuracaoCargaA = cilindro.TempoDuracaoCargaA,
                tempoRampaDescidaA = cilindro.TempoRampaDescidaA,
                percentualVariacaoAlarmeA = cilindro.PercentualVariacaoAlarmeA,
                histereseAlarmeA = cilindro.HistereseAlarmeA,
                percentualVariacaoDesligaProcessoA = cilindro.PercentualVariacaoDesligaProcessoA,
                preCargaB = cilindro.PreCargaB,
                cargaNominalB = cilindro.CargaNominalB,
                tempoRampaSubidaB = cilindro.TempoRampaSubidaB,
                tempoDuracaoCargaB = cilindro.TempoDuracaoCargaB,
                tempoRampaDescidaB = cilindro.TempoRampaDescidaB,
                percentualVariacaoAlarmeB = cilindro.PercentualVariacaoAlarmeB,
                histereseAlarmeB = cilindro.HistereseAlarmeB,
                percentualVariacaoDesligaProcessoB = cilindro.PercentualVariacaoDesligaProcessoB,
                relatorios = cilindro.Relatorios.Select(r => new
                {
                    r.Id,
                    r.Numero,
                    data = r.Data.ToString("yyyy-MM-dd")
                }).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter cilindro");
            return StatusCode(500, new { message = "Erro ao obter cilindro" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CilindroCreateDto dto)
    {
        try
        {
            // Log dos dados recebidos para debug
            _logger.LogInformation($"Criando cilindro - Nome: {dto.Nome}, CodigoCliente: {dto.CodigoCliente}, CodigoInterno: {dto.CodigoInterno}, ClienteId: {dto.ClienteId}");

            if (!ModelState.IsValid)
            {
                // Coletar todos os erros de validação
                var errors = new Dictionary<string, string[]>();
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state?.Errors != null && state.Errors.Count > 0)
                    {
                        errors[key] = state.Errors.Select(e => e.ErrorMessage).ToArray();
                    }
                }
                
                _logger.LogWarning($"Erros de validação: {string.Join(", ", errors.SelectMany(e => e.Value))}");
                
                return BadRequest(new 
                { 
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    title = "One or more validation errors occurred.",
                    status = 400,
                    errors = errors,
                    traceId = System.Diagnostics.Activity.Current?.Id
                });
            }

            // Validação explícita do ClienteId
            if (dto.ClienteId == 0)
            {
                _logger.LogWarning("ClienteId é zero ou não foi fornecido");
                return BadRequest(new { message = "ClienteId é obrigatório e deve ser um valor válido" });
            }

            // Verificar se cliente existe
            var clienteExiste = await _context.Clientes
                .AnyAsync(c => c.Id == dto.ClienteId);
            
            if (!clienteExiste)
            {
                return BadRequest(new { message = $"Cliente com ID {dto.ClienteId} não encontrado" });
            }

            // Verificar se código interno já existe
            var codigoInternoExiste = await _context.Cilindros
                .AnyAsync(c => c.CodigoInterno == dto.CodigoInterno);
            
            if (codigoInternoExiste)
            {
                return Conflict(new { message = "Código interno já cadastrado" });
            }

            // Verificar se código cliente já existe para o mesmo cliente
            var codigoClienteExiste = await _context.Cilindros
                .AnyAsync(c => c.ClienteId == dto.ClienteId && 
                    c.CodigoCliente == dto.CodigoCliente);
            
            if (codigoClienteExiste)
            {
                return Conflict(new { message = "Código cliente já cadastrado para este cliente" });
            }

            // Criar o objeto Cilindro a partir do DTO
            var cilindro = new Cilindro
            {
                Nome = dto.Nome,
                Descricao = dto.Descricao,
                CodigoCliente = dto.CodigoCliente,
                CodigoInterno = dto.CodigoInterno,
                Modelo = dto.Modelo,
                Fabricante = dto.Fabricante,
                DataFabricacao = dto.DataFabricacao,
                DiametroInterno = dto.DiametroInterno,
                ComprimentoHaste = dto.ComprimentoHaste,
                DiametroHaste = dto.DiametroHaste,
                MaximaPressaoA = dto.MaximaPressaoA,
                MaximaPressaoB = dto.MaximaPressaoB,
                PreCargaA = dto.PreCargaA,
                CargaNominalA = dto.CargaNominalA,
                TempoRampaSubidaA = dto.TempoRampaSubidaA,
                TempoDuracaoCargaA = dto.TempoDuracaoCargaA,
                TempoRampaDescidaA = dto.TempoRampaDescidaA,
                PercentualVariacaoAlarmeA = dto.PercentualVariacaoAlarmeA,
                HistereseAlarmeA = dto.HistereseAlarmeA,
                PercentualVariacaoDesligaProcessoA = dto.PercentualVariacaoDesligaProcessoA,
                PreCargaB = dto.PreCargaB,
                CargaNominalB = dto.CargaNominalB,
                TempoRampaSubidaB = dto.TempoRampaSubidaB,
                TempoDuracaoCargaB = dto.TempoDuracaoCargaB,
                TempoRampaDescidaB = dto.TempoRampaDescidaB,
                PercentualVariacaoAlarmeB = dto.PercentualVariacaoAlarmeB,
                HistereseAlarmeB = dto.HistereseAlarmeB,
                PercentualVariacaoDesligaProcessoB = dto.PercentualVariacaoDesligaProcessoB,
                ClienteId = dto.ClienteId,
                DataCriacao = DateTime.UtcNow
            };

            _context.Cilindros.Add(cilindro);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = cilindro.Id }, cilindro);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar cilindro");
            return StatusCode(500, new { message = "Erro ao criar cilindro" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CilindroUpdateDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                // Coletar todos os erros de validação
                var errors = new Dictionary<string, string[]>();
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state?.Errors != null && state.Errors.Count > 0)
                    {
                        errors[key] = state.Errors.Select(e => e.ErrorMessage).ToArray();
                    }
                }
                
                _logger.LogWarning($"Erros de validação: {string.Join(", ", errors.SelectMany(e => e.Value))}");
                
                return BadRequest(new 
                { 
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    title = "One or more validation errors occurred.",
                    status = 400,
                    errors = errors,
                    traceId = System.Diagnostics.Activity.Current?.Id
                });
            }

            var cilindro = await _context.Cilindros.FindAsync(id);
            if (cilindro == null)
            {
                return NotFound(new { message = "Cilindro não encontrado" });
            }

            // Verificar se código interno já existe em outro cilindro
            if (dto.CodigoInterno != cilindro.CodigoInterno)
            {
                var codigoInternoExiste = await _context.Cilindros
                    .AnyAsync(c => c.CodigoInterno == dto.CodigoInterno && c.Id != id);
                
                if (codigoInternoExiste)
                {
                    return Conflict(new { message = "Código interno já cadastrado" });
                }
            }

            // Verificar se código cliente já existe para o mesmo cliente em outro cilindro
            var clienteId = dto.ClienteId != 0 ? dto.ClienteId : cilindro.ClienteId;
            if (dto.CodigoCliente != cilindro.CodigoCliente || 
                clienteId != cilindro.ClienteId)
            {
                var codigoClienteExiste = await _context.Cilindros
                    .AnyAsync(c => c.ClienteId == clienteId && 
                        c.CodigoCliente == dto.CodigoCliente && 
                        c.Id != id);
                
                if (codigoClienteExiste)
                {
                    return Conflict(new { message = "Código cliente já cadastrado para este cliente" });
                }
            }

            // Verificar se cliente existe (se foi alterado)
            if (dto.ClienteId != 0 && dto.ClienteId != cilindro.ClienteId)
            {
                var clienteExiste = await _context.Clientes
                    .AnyAsync(c => c.Id == dto.ClienteId);
                
                if (!clienteExiste)
                {
                    return BadRequest(new { message = "Cliente não encontrado" });
                }
            }

            // Atualiza todas as propriedades
            cilindro.Nome = dto.Nome;
            cilindro.Descricao = dto.Descricao;
            cilindro.CodigoCliente = dto.CodigoCliente;
            cilindro.CodigoInterno = dto.CodigoInterno;
            cilindro.Modelo = dto.Modelo;
            cilindro.Fabricante = dto.Fabricante;
            cilindro.DataFabricacao = dto.DataFabricacao;
            
            // Atualizar ClienteId se fornecido
            if (dto.ClienteId != 0 && dto.ClienteId != cilindro.ClienteId)
            {
                cilindro.ClienteId = dto.ClienteId;
            }
            cilindro.DiametroInterno = dto.DiametroInterno;
            cilindro.ComprimentoHaste = dto.ComprimentoHaste;
            cilindro.DiametroHaste = dto.DiametroHaste;
            cilindro.MaximaPressaoA = dto.MaximaPressaoA;
            cilindro.MaximaPressaoB = dto.MaximaPressaoB;
            cilindro.PreCargaA = dto.PreCargaA;
            cilindro.CargaNominalA = dto.CargaNominalA;
            cilindro.TempoRampaSubidaA = dto.TempoRampaSubidaA;
            cilindro.TempoDuracaoCargaA = dto.TempoDuracaoCargaA;
            cilindro.TempoRampaDescidaA = dto.TempoRampaDescidaA;
            cilindro.PercentualVariacaoAlarmeA = dto.PercentualVariacaoAlarmeA;
            cilindro.HistereseAlarmeA = dto.HistereseAlarmeA;
            cilindro.PercentualVariacaoDesligaProcessoA = dto.PercentualVariacaoDesligaProcessoA;
            cilindro.PreCargaB = dto.PreCargaB;
            cilindro.CargaNominalB = dto.CargaNominalB;
            cilindro.TempoRampaSubidaB = dto.TempoRampaSubidaB;
            cilindro.TempoDuracaoCargaB = dto.TempoDuracaoCargaB;
            cilindro.TempoRampaDescidaB = dto.TempoRampaDescidaB;
            cilindro.PercentualVariacaoAlarmeB = dto.PercentualVariacaoAlarmeB;
            cilindro.HistereseAlarmeB = dto.HistereseAlarmeB;
            cilindro.PercentualVariacaoDesligaProcessoB = dto.PercentualVariacaoDesligaProcessoB;
            cilindro.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(cilindro);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar cilindro");
            return StatusCode(500, new { message = "Erro ao atualizar cilindro" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var cilindro = await _context.Cilindros.FindAsync(id);
            if (cilindro == null)
            {
                return NotFound(new { message = "Cilindro não encontrado" });
            }

            _context.Cilindros.Remove(cilindro);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar cilindro");
            return StatusCode(500, new { message = "Erro ao deletar cilindro" });
        }
    }
}

// DTO para criação de cilindro (sem propriedades de navegação)
public class CilindroCreateDto
{
    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Required]
    [MaxLength(50)]
    public string CodigoCliente { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CodigoInterno { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? Fabricante { get; set; }

    public DateTime? DataFabricacao { get; set; }

    public decimal? DiametroInterno { get; set; }
    public decimal? ComprimentoHaste { get; set; }
    public decimal? DiametroHaste { get; set; }
    
    [Required]
    public decimal MaximaPressaoA { get; set; }
    
    [Required]
    public decimal MaximaPressaoB { get; set; }
    public decimal? PreCargaA { get; set; }
    public decimal? CargaNominalA { get; set; }
    public decimal? TempoRampaSubidaA { get; set; }
    public decimal? TempoDuracaoCargaA { get; set; }
    public decimal? TempoRampaDescidaA { get; set; }
    public decimal? PercentualVariacaoAlarmeA { get; set; }
    public decimal? HistereseAlarmeA { get; set; }
    public decimal? PercentualVariacaoDesligaProcessoA { get; set; }
    public decimal? PreCargaB { get; set; }
    public decimal? CargaNominalB { get; set; }
    public decimal? TempoRampaSubidaB { get; set; }
    public decimal? TempoDuracaoCargaB { get; set; }
    public decimal? TempoRampaDescidaB { get; set; }
    public decimal? PercentualVariacaoAlarmeB { get; set; }
    public decimal? HistereseAlarmeB { get; set; }
    public decimal? PercentualVariacaoDesligaProcessoB { get; set; }

    [Required]
    public int ClienteId { get; set; }
}

// DTO para atualização de cilindro (sem propriedades de navegação)
public class CilindroUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Required]
    [MaxLength(50)]
    public string CodigoCliente { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CodigoInterno { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? Fabricante { get; set; }

    public DateTime? DataFabricacao { get; set; }

    public decimal? DiametroInterno { get; set; }
    public decimal? ComprimentoHaste { get; set; }
    public decimal? DiametroHaste { get; set; }
    
    [Required]
    public decimal MaximaPressaoA { get; set; }
    
    [Required]
    public decimal MaximaPressaoB { get; set; }
    public decimal? PreCargaA { get; set; }
    public decimal? CargaNominalA { get; set; }
    public decimal? TempoRampaSubidaA { get; set; }
    public decimal? TempoDuracaoCargaA { get; set; }
    public decimal? TempoRampaDescidaA { get; set; }
    public decimal? PercentualVariacaoAlarmeA { get; set; }
    public decimal? HistereseAlarmeA { get; set; }
    public decimal? PercentualVariacaoDesligaProcessoA { get; set; }
    public decimal? PreCargaB { get; set; }
    public decimal? CargaNominalB { get; set; }
    public decimal? TempoRampaSubidaB { get; set; }
    public decimal? TempoDuracaoCargaB { get; set; }
    public decimal? TempoRampaDescidaB { get; set; }
    public decimal? PercentualVariacaoAlarmeB { get; set; }
    public decimal? HistereseAlarmeB { get; set; }
    public decimal? PercentualVariacaoDesligaProcessoB { get; set; }

    public int ClienteId { get; set; }
}
