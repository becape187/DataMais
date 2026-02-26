using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<ClienteController> _logger;

    public ClienteController(DataMaisDbContext context, ILogger<ClienteController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var clientes = await _context.Clientes
                .OrderBy(c => c.Nome)
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Cnpj,
                    c.Contato,
                    c.Email
                })
                .ToListAsync();

            return Ok(clientes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter clientes");
            return StatusCode(500, new { message = "Erro ao obter clientes" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var cliente = await _context.Clientes
                .Include(c => c.Cilindros)
                .Include(c => c.Ensaios)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
            {
                return NotFound(new { message = "Cliente não encontrado" });
            }

            var result = new
            {
                cliente.Id,
                cliente.Nome,
                cliente.Cnpj,
                cliente.Contato,
                cliente.Email,
                cilindros = cliente.Cilindros.Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Descricao,
                    c.CodigoCliente,
                    c.CodigoInterno,
                    c.Modelo,
                    c.Fabricante
                }).ToList(),
                relatorios = await _context.Relatorios
                    .Where(r => r.ClienteId == id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Numero,
                        r.Data,
                        cilindroId = r.CilindroId,
                        cilindroNome = r.Cilindro != null ? r.Cilindro.Nome : ""
                    })
                    .ToListAsync()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter cliente");
            return StatusCode(500, new { message = "Erro ao obter cliente" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Cliente cliente)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            cliente.DataCriacao = DateTime.UtcNow;
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, new
            {
                cliente.Id,
                cliente.Nome,
                cliente.Cnpj,
                cliente.Contato,
                cliente.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar cliente");
            return StatusCode(500, new { message = "Erro ao criar cliente" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Cliente clienteAtualizado)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound(new { message = "Cliente não encontrado" });
            }

            cliente.Nome = clienteAtualizado.Nome;
            cliente.Cnpj = clienteAtualizado.Cnpj;
            cliente.Contato = clienteAtualizado.Contato;
            cliente.Email = clienteAtualizado.Email;
            cliente.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                cliente.Id,
                cliente.Nome,
                cliente.Cnpj,
                cliente.Contato,
                cliente.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar cliente");
            return StatusCode(500, new { message = "Erro ao atualizar cliente" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound(new { message = "Cliente não encontrado" });
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar cliente");
            return StatusCode(500, new { message = "Erro ao deletar cliente" });
        }
    }
}
