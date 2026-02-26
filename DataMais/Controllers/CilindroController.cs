using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                maximaPressaoSuportadaA = cilindro.MaximaPressaoSuportadaA,
                maximaPressaoSuportadaB = cilindro.MaximaPressaoSuportadaB,
                maximaPressaoSegurancaA = cilindro.MaximaPressaoSegurancaA,
                maximaPressaoSegurancaB = cilindro.MaximaPressaoSegurancaB,
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
    public async Task<IActionResult> Create([FromBody] Cilindro cilindro)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            cilindro.DataCriacao = DateTime.UtcNow;
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
    public async Task<IActionResult> Update(int id, [FromBody] Cilindro cilindroAtualizado)
    {
        try
        {
            var cilindro = await _context.Cilindros.FindAsync(id);
            if (cilindro == null)
            {
                return NotFound(new { message = "Cilindro não encontrado" });
            }

            // Atualiza todas as propriedades
            cilindro.Nome = cilindroAtualizado.Nome;
            cilindro.Descricao = cilindroAtualizado.Descricao;
            cilindro.CodigoCliente = cilindroAtualizado.CodigoCliente;
            cilindro.CodigoInterno = cilindroAtualizado.CodigoInterno;
            cilindro.Modelo = cilindroAtualizado.Modelo;
            cilindro.Fabricante = cilindroAtualizado.Fabricante;
            cilindro.DataFabricacao = cilindroAtualizado.DataFabricacao;
            cilindro.DiametroInterno = cilindroAtualizado.DiametroInterno;
            cilindro.ComprimentoHaste = cilindroAtualizado.ComprimentoHaste;
            cilindro.DiametroHaste = cilindroAtualizado.DiametroHaste;
            cilindro.MaximaPressaoSuportadaA = cilindroAtualizado.MaximaPressaoSuportadaA;
            cilindro.MaximaPressaoSuportadaB = cilindroAtualizado.MaximaPressaoSuportadaB;
            cilindro.MaximaPressaoSegurancaA = cilindroAtualizado.MaximaPressaoSegurancaA;
            cilindro.MaximaPressaoSegurancaB = cilindroAtualizado.MaximaPressaoSegurancaB;
            cilindro.PreCargaA = cilindroAtualizado.PreCargaA;
            cilindro.CargaNominalA = cilindroAtualizado.CargaNominalA;
            cilindro.TempoRampaSubidaA = cilindroAtualizado.TempoRampaSubidaA;
            cilindro.TempoDuracaoCargaA = cilindroAtualizado.TempoDuracaoCargaA;
            cilindro.TempoRampaDescidaA = cilindroAtualizado.TempoRampaDescidaA;
            cilindro.PercentualVariacaoAlarmeA = cilindroAtualizado.PercentualVariacaoAlarmeA;
            cilindro.HistereseAlarmeA = cilindroAtualizado.HistereseAlarmeA;
            cilindro.PercentualVariacaoDesligaProcessoA = cilindroAtualizado.PercentualVariacaoDesligaProcessoA;
            cilindro.PreCargaB = cilindroAtualizado.PreCargaB;
            cilindro.CargaNominalB = cilindroAtualizado.CargaNominalB;
            cilindro.TempoRampaSubidaB = cilindroAtualizado.TempoRampaSubidaB;
            cilindro.TempoDuracaoCargaB = cilindroAtualizado.TempoDuracaoCargaB;
            cilindro.TempoRampaDescidaB = cilindroAtualizado.TempoRampaDescidaB;
            cilindro.PercentualVariacaoAlarmeB = cilindroAtualizado.PercentualVariacaoAlarmeB;
            cilindro.HistereseAlarmeB = cilindroAtualizado.HistereseAlarmeB;
            cilindro.PercentualVariacaoDesligaProcessoB = cilindroAtualizado.PercentualVariacaoDesligaProcessoB;
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
