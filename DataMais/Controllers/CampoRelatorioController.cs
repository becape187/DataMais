using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CampoRelatorioController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<CampoRelatorioController> _logger;

    public CampoRelatorioController(DataMaisDbContext context, ILogger<CampoRelatorioController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os campos ativos (não excluídos) ordenados por ordem.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool incluirExcluidos = false)
    {
        try
        {
            var query = _context.CamposRelatorio.AsQueryable();

            if (!incluirExcluidos)
            {
                query = query.Where(c => c.DataExclusao == null);
            }

            var campos = await query
                .OrderBy(c => c.Ordem)
                .ThenBy(c => c.DataCriacao)
                .ToListAsync();

            var result = campos.Select(c => new
            {
                id = c.Id,
                nome = c.Nome,
                tipoResposta = c.TipoResposta,
                ordem = c.Ordem,
                dataCriacao = c.DataCriacao,
                dataExclusao = c.DataExclusao
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar campos do relatório");
            return StatusCode(500, new { message = "Erro ao listar campos do relatório" });
        }
    }

    /// <summary>
    /// Obtém um campo específico por ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var campo = await _context.CamposRelatorio.FindAsync(id);

            if (campo == null)
            {
                return NotFound(new { message = "Campo não encontrado" });
            }

            var result = new
            {
                id = campo.Id,
                nome = campo.Nome,
                tipoResposta = campo.TipoResposta,
                ordem = campo.Ordem,
                dataCriacao = campo.DataCriacao,
                dataExclusao = campo.DataExclusao
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter campo {CampoId}", id);
            return StatusCode(500, new { message = "Erro ao obter campo", error = ex.Message });
        }
    }

    /// <summary>
    /// Cria um novo campo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampoRelatorioRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("Request nulo recebido");
                return BadRequest(new { message = "Request inválido" });
            }

            _logger.LogInformation("Criando campo: Nome={Nome}, TipoResposta={TipoResposta}", request.Nome, request.TipoResposta);

            if (string.IsNullOrWhiteSpace(request.Nome))
            {
                return BadRequest(new { message = "O nome do campo é obrigatório" });
            }

            if (string.IsNullOrWhiteSpace(request.TipoResposta) ||
                !new[] { "SimOuNao", "TextoSimples", "MultiplasLinhas" }.Contains(request.TipoResposta))
            {
                return BadRequest(new { message = "Tipo de resposta inválido. Deve ser: SimOuNao, TextoSimples ou MultiplasLinhas" });
            }

            // Calcula a próxima ordem
            var maxOrdem = await _context.CamposRelatorio
                .Where(c => c.DataExclusao == null)
                .MaxAsync(c => (int?)c.Ordem) ?? 0;

            var campo = new CampoRelatorio
            {
                Nome = request.Nome.Trim(),
                TipoResposta = request.TipoResposta,
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.UtcNow
            };

            _context.CamposRelatorio.Add(campo);
            await _context.SaveChangesAsync();

            var result = new
            {
                id = campo.Id,
                nome = campo.Nome,
                tipoResposta = campo.TipoResposta,
                ordem = campo.Ordem,
                dataCriacao = campo.DataCriacao,
                dataExclusao = campo.DataExclusao
            };

            return CreatedAtAction(nameof(GetById), new { id = campo.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar campo do relatório: {Error}", ex.ToString());
            return StatusCode(500, new { message = "Erro ao criar campo do relatório", error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>
    /// Atualiza um campo existente.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCampoRelatorioRequest request)
    {
        try
        {
            var campo = await _context.CamposRelatorio.FindAsync(id);

            if (campo == null)
            {
                return NotFound(new { message = "Campo não encontrado" });
            }

            if (campo.DataExclusao != null)
            {
                return BadRequest(new { message = "Não é possível atualizar um campo excluído" });
            }

            if (!string.IsNullOrWhiteSpace(request.Nome))
            {
                campo.Nome = request.Nome.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.TipoResposta) &&
                new[] { "SimOuNao", "TextoSimples", "MultiplasLinhas" }.Contains(request.TipoResposta))
            {
                campo.TipoResposta = request.TipoResposta;
            }

            await _context.SaveChangesAsync();

            var result = new
            {
                id = campo.Id,
                nome = campo.Nome,
                tipoResposta = campo.TipoResposta,
                ordem = campo.Ordem,
                dataCriacao = campo.DataCriacao,
                dataExclusao = campo.DataExclusao
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar campo {CampoId}", id);
            return StatusCode(500, new { message = "Erro ao atualizar campo", error = ex.Message });
        }
    }

    /// <summary>
    /// Exclui um campo (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var campo = await _context.CamposRelatorio.FindAsync(id);

            if (campo == null)
            {
                return NotFound(new { message = "Campo não encontrado" });
            }

            if (campo.DataExclusao != null)
            {
                return Ok(new { message = "Campo já estava excluído" });
            }

            // Soft delete
            campo.DataExclusao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Campo excluído com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir campo {CampoId}", id);
            return StatusCode(500, new { message = "Erro ao excluir campo", error = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza a ordem dos campos.
    /// </summary>
    [HttpPost("reordenar")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarCamposRequest request)
    {
        try
        {
            if (request.CamposIds == null || request.CamposIds.Count == 0)
            {
                return BadRequest(new { message = "Lista de IDs de campos é obrigatória" });
            }

            var campos = await _context.CamposRelatorio
                .Where(c => request.CamposIds.Contains(c.Id) && c.DataExclusao == null)
                .ToListAsync();

            if (campos.Count != request.CamposIds.Count)
            {
                return BadRequest(new { message = "Um ou mais campos não foram encontrados" });
            }

            // Atualiza a ordem conforme a lista fornecida
            for (int i = 0; i < request.CamposIds.Count; i++)
            {
                var campo = campos.FirstOrDefault(c => c.Id == request.CamposIds[i]);
                if (campo != null)
                {
                    campo.Ordem = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Campos reordenados com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reordenar campos");
            return StatusCode(500, new { message = "Erro ao reordenar campos", error = ex.Message });
        }
    }
}

// DTOs
public class CreateCampoRelatorioRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("tipoResposta")]
    public string TipoResposta { get; set; } = string.Empty; // "SimOuNao", "TextoSimples", "MultiplasLinhas"
}

public class UpdateCampoRelatorioRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("nome")]
    public string? Nome { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("tipoResposta")]
    public string? TipoResposta { get; set; }
}

public class ReordenarCamposRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("camposIds")]
    public List<int> CamposIds { get; set; } = new();
}
