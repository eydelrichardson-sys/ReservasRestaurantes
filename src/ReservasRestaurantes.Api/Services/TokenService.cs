using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ReservasRestaurantes.Api.Models;

namespace ReservasRestaurantes.Api.Services;

public class TokenService
{
    private readonly IConfiguration _cfg;

    public TokenService(IConfiguration cfg) => _cfg = cfg;

    public (string Token, DateTime Expira) Crear(Usuario u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var horas = double.Parse(_cfg["Jwt:ExpireHours"] ?? "8");
        var expira = DateTime.UtcNow.AddHours(horas);

        // Nombres de claim cortos a propósito (el cliente Blazor los lee tal cual).
        var claims = new List<Claim>
        {
            new("sub", u.Id.ToString()),
            new("name", u.Nombre),
            new("email", u.Email),
            new("role", u.Rol)
        };
        if (u.IdRestaurante.HasValue)
            claims.Add(new Claim("rid", u.IdRestaurante.Value.ToString()));

        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
