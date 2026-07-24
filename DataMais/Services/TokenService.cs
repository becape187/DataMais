using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataMais.Models;
using Microsoft.IdentityModel.Tokens;

namespace DataMais.Services;

/// <summary>
/// Gera tokens JWT para autenticação. O segredo vem do JWT_SECRET (.env / systemd).
/// Registrado como singleton em Program.cs com o segredo já resolvido.
/// </summary>
public class TokenService
{
    private readonly byte[] _key;

    public const int ValidadeHoras = 12;

    public TokenService(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new ArgumentException("JWT_SECRET precisa ter ao menos 32 caracteres.", nameof(secret));
        }
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string GerarToken(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(usuario.Role) ? "Visualizador" : usuario.Role),
            new Claim("email", usuario.Email)
        };

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(_key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(ValidadeHoras),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
