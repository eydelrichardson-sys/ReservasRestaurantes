using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Hubs;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Controllers;

/// <summary>Endpoints públicos para clientes: crear, consultar, cancelar y calificar con el código de reserva.</summary>
[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int DiasMaximosAnticipacion = 90;

    private readonly AppDbContext _db;
    private readonly DisponibilidadService _disp;
    private readonly IHubContext<ReservasHub> _hub;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReservasController> _log;

    public ReservasController(
        AppDbContext db, DisponibilidadService disp, IHubContext<ReservasHub> hub,
        TimeProvider clock, ILogger<ReservasController> log)
    {
        _db = db;
        _disp = disp;
        _hub = hub;
        _clock = clock;
        _log = log;
    }

    private DateTime Ahora => _clock.GetLocalNow().DateTime;

    [HttpPost]
    [EnableRateLimiting("publico")]
    public async Task<ActionResult<ReservaDto>> Crear(CrearReservaRequest req)
    {
        var hoy = DateOnly.FromDateTime(Ahora);
        if (req.Fecha < hoy)
            return BadRequest(new ApiError("La fecha no puede ser pasada."));
        if (req.Fecha > hoy.AddDays(DiasMaximosAnticipacion))
            return BadRequest(new ApiError($"Solo se puede reservar con hasta {DiasMaximosAnticipacion} días de anticipación."));

        // Serializable: evita que dos clientes tomen la misma mesa al mismo tiempo.
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var slots = await _disp.CalcularAsync(req.IdRestaurante, req.Fecha, req.CantidadPersonas);
        var slot = slots.FirstOrDefault(s => s.Hora == req.Hora);

        if (slot == null)
            return BadRequest(new ApiError("Ese horario no es válido para este restaurante."));
        if (slot.Mesa == null)
            return Conflict(new ApiError("Ya no hay mesas disponibles en ese horario. Elige otro, por favor."));

        var reserva = new Reserva
        {
            IdRestaurante = req.IdRestaurante,
            IdMesa = slot.Mesa.Id,
            NombreCliente = req.Nombre.Trim(),
            EmailCliente = req.Email.Trim().ToLowerInvariant(),
            TelefonoCliente = req.Telefono.Trim(),
            Fecha = req.Fecha,
            Hora = req.Hora,
            CantidadPersonas = req.CantidadPersonas,
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim(),
            Estado = EstadoReserva.Pendiente,
            Codigo = await GenerarCodigoAsync(),
            FechaCreacion = Ahora
        };

        _db.Reservas.Add(reserva);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _db.Entry(reserva).Reference(r => r.Restaurante).LoadAsync();
        await _db.Entry(reserva).Reference(r => r.Mesa).LoadAsync();

        _log.LogInformation("Reserva {Codigo} creada: restaurante {Rest}, {Fecha} {Hora}, {Personas} persona(s)",
            reserva.Codigo, reserva.IdRestaurante, reserva.Fecha, reserva.Hora, reserva.CantidadPersonas);

        await _hub.Clients.Group(ReservasHub.Grupo(reserva.IdRestaurante)).SendAsync("ReservasActualizadas");
        return CreatedAtAction(nameof(Consultar), new { codigo = reserva.Codigo }, await ConEstadoResenaAsync(reserva));
    }

    [HttpGet("{codigo}")]
    [EnableRateLimiting("publico")]
    public async Task<ActionResult<ReservaDto>> Consultar(string codigo)
    {
        var reserva = await BuscarPorCodigoAsync(codigo);
        if (reserva == null) return NotFound(new ApiError("No encontramos una reserva con ese código."));
        return await ConEstadoResenaAsync(reserva);
    }

    [HttpPost("{codigo}/cancelar")]
    [EnableRateLimiting("publico")]
    public async Task<ActionResult<ReservaDto>> Cancelar(string codigo)
    {
        var reserva = await BuscarPorCodigoAsync(codigo);
        if (reserva == null) return NotFound(new ApiError("No encontramos una reserva con ese código."));

        if (reserva.Estado is not (EstadoReserva.Pendiente or EstadoReserva.Aprobada))
            return Conflict(new ApiError($"La reserva ya está {reserva.Estado.ToString().ToLower()} y no se puede cancelar."));

        if (reserva.Fecha.ToDateTime(reserva.Hora) <= Ahora)
            return Conflict(new ApiError("La reserva ya pasó y no se puede cancelar."));

        reserva.Estado = EstadoReserva.Cancelada;
        await _db.SaveChangesAsync();

        _log.LogInformation("Reserva {Codigo} cancelada por el cliente", reserva.Codigo);

        await _hub.Clients.Group(ReservasHub.Grupo(reserva.IdRestaurante)).SendAsync("ReservasActualizadas");
        return await ConEstadoResenaAsync(reserva);
    }

    /// <summary>El cliente califica su visita (1 a 5 estrellas) una sola vez, después de la hora de la reserva.</summary>
    [HttpPost("{codigo}/resena")]
    [EnableRateLimiting("publico")]
    public async Task<ActionResult<ReservaDto>> Calificar(string codigo, CrearResenaRequest req)
    {
        var reserva = await BuscarPorCodigoAsync(codigo);
        if (reserva == null) return NotFound(new ApiError("No encontramos una reserva con ese código."));

        if (reserva.Estado != EstadoReserva.Aprobada)
            return Conflict(new ApiError("Solo puedes calificar reservas que el restaurante aprobó."));
        if (reserva.Fecha.ToDateTime(reserva.Hora) > Ahora)
            return Conflict(new ApiError("Podrás calificar después de tu visita."));
        if (await _db.Resenas.AnyAsync(r => r.IdReserva == reserva.Id))
            return Conflict(new ApiError("Ya calificaste esta reserva. ¡Gracias!"));

        _db.Resenas.Add(new Resena
        {
            IdReserva = reserva.Id,
            IdRestaurante = reserva.IdRestaurante,
            Calificacion = req.Calificacion,
            Comentario = string.IsNullOrWhiteSpace(req.Comentario) ? null : req.Comentario.Trim(),
            Fecha = Ahora
        });
        await _db.SaveChangesAsync();

        _log.LogInformation("Reserva {Codigo} calificada con {Estrellas} estrella(s)", reserva.Codigo, req.Calificacion);
        return await ConEstadoResenaAsync(reserva);
    }

    // ---------- helpers ----------

    private Task<Reserva?> BuscarPorCodigoAsync(string codigo)
    {
        var c = (codigo ?? "").Trim().ToUpperInvariant();
        return _db.Reservas
            .Include(r => r.Restaurante)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Codigo == c);
    }

    private async Task<ReservaDto> ConEstadoResenaAsync(Reserva reserva)
    {
        var dto = reserva.ToDto();
        dto.YaCalificada = await _db.Resenas.AnyAsync(r => r.IdReserva == reserva.Id);
        dto.PuedeCalificar = reserva.Estado == EstadoReserva.Aprobada
                             && reserva.Fecha.ToDateTime(reserva.Hora) <= Ahora
                             && !dto.YaCalificada;
        return dto;
    }

    private async Task<string> GenerarCodigoAsync()
    {
        while (true)
        {
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = AlfabetoCodigo[RandomNumberGenerator.GetInt32(AlfabetoCodigo.Length)];
            var codigo = new string(chars);

            if (!await _db.Reservas.AnyAsync(r => r.Codigo == codigo))
                return codigo;
        }
    }
}
