using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuarioController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<UsuarioController> _logger;

    public UsuarioController(DataMaisDbContext context, ILogger<UsuarioController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var usuarios = await _context.Usuarios
                .OrderBy(u => u.Nome)
                .Select(u => new
                {
                    u.Id,
                    u.Nome,
                    u.Email,
                    u.Role,
                    u.Ativo,
                    ultimoAcesso = u.UltimoLogin.HasValue 
                        ? u.UltimoLogin.Value.ToString("dd/MM/yyyy HH:mm") 
                        : null
                })
                .ToListAsync();

            return Ok(usuarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter usuários");
            return StatusCode(500, new { message = "Erro ao obter usuários" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return NotFound(new { message = "Usuário não encontrado" });
            }

            var result = new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Email,
                usuario.Role,
                usuario.Ativo,
                ultimoAcesso = usuario.UltimoLogin?.ToString("dd/MM/yyyy HH:mm")
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter usuário");
            return StatusCode(500, new { message = "Erro ao obter usuário" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
            {
                return BadRequest(new { message = "Nome é obrigatório" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email é obrigatório" });
            }

            if (string.IsNullOrWhiteSpace(request.Senha))
            {
                return BadRequest(new { message = "Senha é obrigatória" });
            }

            if (request.Senha.Length < 8)
            {
                return BadRequest(new { message = "Senha deve ter no mínimo 8 caracteres" });
            }

            // Verificar se email já existe
            var emailExiste = await _context.Usuarios
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (emailExiste)
            {
                return Conflict(new { message = "Email já cadastrado" });
            }

            var usuario = new Usuario
            {
                Nome = request.Nome,
                Email = request.Email.ToLower(),
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
                Role = request.Role ?? "Usuario",
                Ativo = request.Ativo ?? true,
                DataCriacao = DateTime.UtcNow
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Email,
                usuario.Role,
                usuario.Ativo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
            return StatusCode(500, new { message = "Erro ao criar usuário" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioRequest request)
    {
        try
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound(new { message = "Usuário não encontrado" });
            }

            // Verificar se email já existe em outro usuário
            if (!string.IsNullOrWhiteSpace(request.Email) && 
                request.Email.ToLower() != usuario.Email.ToLower())
            {
                var emailExiste = await _context.Usuarios
                    .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower() && u.Id != id);

                if (emailExiste)
                {
                    return Conflict(new { message = "Email já cadastrado" });
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Nome))
            {
                usuario.Nome = request.Nome;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                usuario.Email = request.Email.ToLower();
            }

            if (!string.IsNullOrWhiteSpace(request.Senha))
            {
                if (request.Senha.Length < 8)
                {
                    return BadRequest(new { message = "Senha deve ter no mínimo 8 caracteres" });
                }
                usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha);
            }

            if (request.Role != null)
            {
                usuario.Role = request.Role;
            }

            if (request.Ativo.HasValue)
            {
                usuario.Ativo = request.Ativo.Value;
            }

            usuario.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Email,
                usuario.Role,
                usuario.Ativo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar usuário");
            return StatusCode(500, new { message = "Erro ao atualizar usuário" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound(new { message = "Usuário não encontrado" });
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar usuário");
            return StatusCode(500, new { message = "Erro ao deletar usuário" });
        }
    }
}

// DTOs para requests
public class CreateUsuarioRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool? Ativo { get; set; }
}

public class UpdateUsuarioRequest
{
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public string? Senha { get; set; }
    public string? Role { get; set; }
    public bool? Ativo { get; set; }
}
