using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Hubs;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Controllers.Admin;

[Route("api/admin/reservas")]
public class AdminReservasController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IHubContext<ReservasHub> _hub;
    private readonly TimeProvider _clock;
    private readonly ILogger<AdminReservasController> _log;

    public AdminReservasController(AppDbContext db, IEmailService email, IHubContext<ReservasHub> hub,
        TimeProvider clock, ILogger<AdminReservasController> log)
    {
        _clock = clock;
        _log = log;
        _db = db;
        _email = email;
        _hub = hub;
    }

    /// <summary>Lista reservas del restaurante. Filtros opcionales: fecha y estado.</summary>
    [HttpGet]
    public async Task<List<ReservaDto>> Listar([FromQuery] DateOnly? fecha, [FromQuery] EstadoReserva? estado)
    {
        var q = _db.Reservas.AsNoTracking()
            .Include(r => r.Restaurante)
            .Include(r => r.Mesa)
            .Where(r => r.IdRestaurante == IdRestaurante);

        if (fecha.HasValue) q = q.Where(r => r.Fecha == fecha.Value);
        if (estado.HasValue) q = q.Where(r => r.Estado == estado.Value);

        var lista = await q.OrderBy(r => r.Fecha).ThenBy(r => r.Hora).ToListAsync();
        return lista.Select(r => r.ToDto()).ToList();
    }

    [HttpPost("{id:int}/aprobar")]
    public async Task<ActionResult<ReservaDto>> Aprobar(int id)
    {
        var reserva = await CargarAsync(id);
        if (reserva == null) return NotFound(new ApiError("Reserva no encontrada."));

        if (reserva.Estado != EstadoReserva.Pendiente)
            return Conflict(new ApiError($"Solo se pueden aprobar reservas pendientes (esta está {reserva.Estado.ToString().ToLower()})."));

        reserva.Estado = EstadoReserva.Aprobada;
        _log.LogInformation("Reserva {Codigo} aprobada por {Admin}", reserva.Codigo, User.FindFirst("name")?.Value);
        reserva.MotivoRechazo = null;
        await _db.SaveChangesAsync();

        // El correo no debe tumbar la aprobación: si falla, queda registrado en el log.
        await _email.EnviarConfirmacionAsync(reserva);

        await Notificar();
        return reserva.ToDto();
    }

    [HttpPost("{id:int}/rechazar")]
    public async Task<ActionResult<ReservaDto>> Rechazar(int id, RechazarReservaRequest req)
    {
        var reserva = await CargarAsync(id);
        if (reserva == null) return NotFound(new ApiError("Reserva no encontrada."));

        if (reserva.Estado != EstadoReserva.Pendiente)
            return Conflict(new ApiError($"Solo se pueden rechazar reservas pendientes (esta está {reserva.Estado.ToString().ToLower()})."));

        reserva.Estado = EstadoReserva.Rechazada; // libera la mesa: solo Pendiente/Aprobada ocupan
        _log.LogInformation("Reserva {Codigo} rechazada por {Admin}", reserva.Codigo, User.FindFirst("name")?.Value);
        reserva.MotivoRechazo = string.IsNullOrWhiteSpace(req.Motivo) ? null : req.Motivo.Trim();
        await _db.SaveChangesAsync();

        await Notificar();
        return reserva.ToDto();
    }

    /// <summary>Ocupación "ahora": qué mesas tienen una reserva aprobada en curso.</summary>
    [HttpGet("ocupacion")]
    public async Task<ActionResult<OcupacionDto>> Ocupacion()
    {
        var rest = await _db.Restaurantes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == IdRestaurante);
        if (rest == null) return NotFound(new ApiError("Restaurante no encontrado."));

        var ahora = _clock.GetLocalNow().DateTime;
        var hoy = DateOnly.FromDateTime(ahora);
        var t = ahora.TimeOfDay;
        var duracion = TimeSpan.FromMinutes(rest.DuracionReservaMin);

        var mesas = await _db.Mesas.AsNoTracking()
            .Where(m => m.IdRestaurante == IdRestaurante && m.Activa)
            .OrderBy(m => m.Numero)
            .ToListAsync();

        var reservasHoy = await _db.Reservas.AsNoTracking()
            .Where(r => r.IdRestaurante == IdRestaurante && r.Fecha == hoy
                        && (r.Estado == EstadoReserva.Pendiente || r.Estado == EstadoReserva.Aprobada))
            .ToListAsync();

        var estados = new List<MesaEstadoDto>();
        foreach (var m in mesas)
        {
            var enCurso = reservasHoy.FirstOrDefault(r =>
                r.Estado == EstadoReserva.Aprobada && r.IdMesa == m.Id &&
                r.Hora.ToTimeSpan() <= t && t < r.Hora.ToTimeSpan() + duracion);

            estados.Add(new MesaEstadoDto
            {
                IdMesa = m.Id,
                Numero = m.Numero,
                Capacidad = m.Capacidad,
                Ocupada = enCurso != null,
                Cliente = enCurso?.NombreCliente,
                Hasta = enCurso != null ? TimeOnly.FromTimeSpan(enCurso.Hora.ToTimeSpan() + duracion) : null
            });
        }

        var ocupadas = estados.Count(e => e.Ocupada);
        return new OcupacionDto
        {
            Ahora = ahora,
            TotalMesas = mesas.Count,
            MesasOcupadas = ocupadas,
            ReservasPendientes = reservasHoy.Count(r => r.Estado == EstadoReserva.Pendiente),
            ReservasHoy = reservasHoy.Count,
            PorcentajeOcupacion = mesas.Count == 0 ? 0 : Math.Round(ocupadas * 100.0 / mesas.Count, 0),
            Mesas = estados
        };
    }

    // ---------- helpers ----------

    private Task<Reserva?> CargarAsync(int id) =>
        _db.Reservas
            .Include(r => r.Restaurante)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == id && r.IdRestaurante == IdRestaurante);

    private Task Notificar() =>
        _hub.Clients.Group(ReservasHub.Grupo(IdRestaurante)).SendAsync("ReservasActualizadas");
}
