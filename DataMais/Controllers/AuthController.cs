using System.Security.Claims;
using DataMais.Data;
using DataMais.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DataMaisDbContext _context;
    private readonly TokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(DataMaisDbContext context, TokenService tokenService, ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
            {
                return BadRequest(new { message = "Informe email e senha." });
            }

            var email = request.Email.Trim().ToLower();
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
            {
                return Unauthorized(new { message = "Credenciais inválidas." });
            }

            if (!usuario.Ativo)
            {
                return Unauthorized(new { message = "Usuário inativo. Contate o administrador." });
            }

            usuario.UltimoLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = _tokenService.GerarToken(usuario);

            return Ok(new
            {
                token,
                usuario = new
                {
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no login");
            return StatusCode(500, new { message = "Erro ao efetuar login" });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var id))
        {
            return Unauthorized();
        }

        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null || !usuario.Ativo)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.Role
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}
