using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthController> _log;

    public AuthController(AppDbContext db, TokenService tokens, TimeProvider clock, ILogger<AuthController> log)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _log = log;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var ahora = _clock.GetLocalNow().DateTime;
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo);

        if (usuario?.BloqueadoHasta != null && usuario.BloqueadoHasta > ahora)
        {
            var minutos = (int)Math.Ceiling((usuario.BloqueadoHasta.Value - ahora).TotalMinutes);
            _log.LogWarning("Intento de login en cuenta bloqueada: {Email}", email);
            return StatusCode(StatusCodes.Status423Locked,
                new ApiError($"Cuenta bloqueada temporalmente por demasiados intentos. Inténtalo de nuevo en {minutos} minuto(s)."));
        }

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(req.Password, usuario.PasswordHash))
        {
            if (usuario != null)
            {
                usuario.IntentosFallidos++;
                if (usuario.IntentosFallidos >= MaxIntentos)
                {
                    usuario.BloqueadoHasta = ahora.Add(DuracionBloqueo);
                    usuario.IntentosFallidos = 0;
                    _log.LogWarning("Cuenta {Email} bloqueada {Minutos} min por intentos fallidos", email, DuracionBloqueo.TotalMinutes);
                }
                await _db.SaveChangesAsync();
            }

            _log.LogWarning("Login fallido para {Email}", email);
            // Mismo mensaje exista o no el correo: no revela qué cuentas están registradas.
            return Unauthorized(new ApiError("Correo o contraseña incorrectos."));
        }

        if (usuario.IntentosFallidos != 0 || usuario.BloqueadoHasta != null)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
            await _db.SaveChangesAsync();
        }

        _log.LogInformation("Login correcto: {Email}", email);
        return Respuesta(usuario);
    }

    /// <summary>Registro de un restaurante nuevo junto con su cuenta de administrador.</summary>
    [HttpPost("registro")]
    [EnableRateLimiting("registro")]
    public async Task<ActionResult<LoginResponse>> Registro(RegistroRestauranteRequest req)
    {
        var errorPassword = PasswordPolicy.Validar(req.Password);
        if (errorPassword != null)
            return BadRequest(new ApiError(errorPassword));

        var email = req.Email.Trim().ToLowerInvariant();
        var nombre = req.NombreRestaurante.Trim();

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
            return Conflict(new ApiError("Ya existe una cuenta con ese correo."));
        if (await _db.Restaurantes.AnyAsync(r => r.Nombre == nombre))
            return Conflict(new ApiError("Ya existe un restaurante con ese nombre."));

        await using var tx = await _db.Database.BeginTransactionAsync();

        var restaurante = new Restaurante
        {
            Nombre = nombre,
            Direccion = req.Direccion.Trim(),
            Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim(),
            Email = email,
            DuracionReservaMin = 90
        };
        _db.Restaurantes.Add(restaurante);
        await _db.SaveChangesAsync();

        // Horario inicial de todos los días de 11:00 a 22:00; luego lo ajusta desde "Perfil y horarios".
        for (var dia = 0; dia < 7; dia++)
        {
            _db.Horarios.Add(new Horario
            {
                IdRestaurante = restaurante.Id,
                DiaSemana = dia,
                Apertura = new TimeOnly(11, 0),
                Cierre = new TimeOnly(22, 0),
                Cerrado = false
            });
        }

        var usuario = new Usuario
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Nombre = req.NombreAdmin.Trim(),
            Rol = "Admin",
            IdRestaurante = restaurante.Id
        };
        _db.Usuarios.Add(usuario);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogInformation("Restaurante registrado: {Restaurante} (id {Id}) por {Email}", nombre, restaurante.Id, email);
        return Respuesta(usuario);
    }

    private LoginResponse Respuesta(Usuario usuario)
    {
        var (token, expira) = _tokens.Crear(usuario);
        return new LoginResponse
        {
            Token = token,
            Nombre = usuario.Nombre,
            Rol = usuario.Rol,
            IdRestaurante = usuario.IdRestaurante,
            Expira = expira
        };
    }
}
