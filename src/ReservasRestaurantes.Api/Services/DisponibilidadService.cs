using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Services;

public record SlotCalculado(TimeOnly Hora, Mesa? Mesa);

/// <summary>
/// Calcula qué horarios tienen mesa libre. Un horario es válido cada 30 minutos
/// entre la apertura y (cierre - duración de la reserva). Una mesa está ocupada si
/// tiene una reserva Pendiente/Aprobada cuyo intervalo [hora, hora+duración) se solapa.
/// Limitación: no soporta horarios que cruzan la medianoche.
/// </summary>
public class DisponibilidadService
{
    private const int PasoMinutos = 30;
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public DisponibilidadService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<SlotCalculado>> CalcularAsync(int idRestaurante, DateOnly fecha, int personas)
    {
        var resultado = new List<SlotCalculado>();

        var rest = await _db.Restaurantes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == idRestaurante && r.Activo);
        if (rest == null) return resultado;

        var ahora = _clock.GetLocalNow().DateTime;
        var hoy = DateOnly.FromDateTime(ahora);
        if (fecha < hoy) return resultado;

        var dia = (int)fecha.DayOfWeek;
        var horario = await _db.Horarios.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdRestaurante == idRestaurante && h.DiaSemana == dia);
        if (horario == null || horario.Cerrado) return resultado;

        // Mesas candidatas: activas y con capacidad suficiente. Primero la más ajustada.
        var mesas = await _db.Mesas.AsNoTracking()
            .Where(m => m.IdRestaurante == idRestaurante && m.Activa && m.Capacidad >= personas)
            .OrderBy(m => m.Capacidad).ThenBy(m => m.Numero)
            .ToListAsync();

        var reservas = await _db.Reservas.AsNoTracking()
            .Where(r => r.IdRestaurante == idRestaurante
                        && r.Fecha == fecha
                        && r.IdMesa != null
                        && (r.Estado == EstadoReserva.Pendiente || r.Estado == EstadoReserva.Aprobada))
            .ToListAsync();

        var duracion = TimeSpan.FromMinutes(rest.DuracionReservaMin);
        var paso = TimeSpan.FromMinutes(PasoMinutos);
        var apertura = horario.Apertura.ToTimeSpan();
        var cierre = horario.Cierre.ToTimeSpan();

        for (var ini = apertura; ini + duracion <= cierre; ini += paso)
        {
            var fin = ini + duracion;
            Mesa? libre = null;

            var esPasado = fecha == hoy && ini <= ahora.TimeOfDay;
            if (!esPasado)
            {
                libre = mesas.FirstOrDefault(m => !reservas.Any(r =>
                    r.IdMesa == m.Id &&
                    Solapa(ini, fin, r.Hora.ToTimeSpan(), r.Hora.ToTimeSpan() + duracion)));
            }

            resultado.Add(new SlotCalculado(TimeOnly.FromTimeSpan(ini), libre));
        }

        return resultado;
    }

    private static bool Solapa(TimeSpan aIni, TimeSpan aFin, TimeSpan bIni, TimeSpan bFin) =>
        aIni < bFin && bIni < aFin;
}
