using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Dtos;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Services;

/// <summary>
/// Calcula los indicadores del reporte a partir de las reservas y calificaciones del rango.
/// Es una clase estática y pura (sin base de datos) para poder probarla con pruebas unitarias.
/// </summary>
public static class ReporteBuilder
{
    private static readonly string[] NombresDia = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };

    /// <summary>Una reserva "efectiva" ocupa o va a ocupar mesa: pendiente o aprobada.</summary>
    private static bool Efectiva(Reserva r) =>
        r.Estado == EstadoReserva.Pendiente || r.Estado == EstadoReserva.Aprobada;

    public static ReporteResumenDto Crear(
        DateOnly desde, DateOnly hasta,
        IReadOnlyList<Reserva> reservas, IReadOnlyList<Resena> resenas)
    {
        var aprobadas = reservas.Where(r => r.Estado == EstadoReserva.Aprobada).ToList();

        var dto = new ReporteResumenDto
        {
            Desde = desde,
            Hasta = hasta,
            TotalReservas = reservas.Count,
            Pendientes = reservas.Count(r => r.Estado == EstadoReserva.Pendiente),
            Aprobadas = aprobadas.Count,
            Rechazadas = reservas.Count(r => r.Estado == EstadoReserva.Rechazada),
            Canceladas = reservas.Count(r => r.Estado == EstadoReserva.Cancelada),
            PersonasAprobadas = aprobadas.Sum(r => r.CantidadPersonas),
            TotalResenas = resenas.Count
        };

        dto.PromedioPersonas = reservas.Count == 0
            ? 0
            : Math.Round(reservas.Average(r => (double)r.CantidadPersonas), 1);

        var decididas = dto.Aprobadas + dto.Rechazadas;
        dto.TasaAprobacion = decididas == 0 ? 0 : Math.Round(dto.Aprobadas * 100.0 / decididas, 0);

        dto.PromedioCalificacion = resenas.Count == 0
            ? 0
            : Math.Round(resenas.Average(r => (double)r.Calificacion), 1);

        var efectivas = reservas.Where(Efectiva).ToList();

        // Por día: todos los días del rango, aunque no tengan reservas.
        var porFecha = efectivas.GroupBy(r => r.Fecha).ToDictionary(g => g.Key, g => g.Count());
        for (var d = desde; d <= hasta; d = d.AddDays(1))
        {
            porFecha.TryGetValue(d, out var cantidad);
            dto.PorDia.Add(new PuntoDto { Etiqueta = d.ToString("dd/MM"), Valor = cantidad });
        }

        // Por hora: desde la primera hasta la última hora con reservas.
        if (efectivas.Count > 0)
        {
            var porHora = efectivas.GroupBy(r => r.Hora.Hour).ToDictionary(g => g.Key, g => g.Count());
            for (var h = porHora.Keys.Min(); h <= porHora.Keys.Max(); h++)
            {
                porHora.TryGetValue(h, out var cantidad);
                dto.PorHora.Add(new PuntoDto { Etiqueta = $"{h}:00", Valor = cantidad });
            }
        }

        // Por día de la semana, de lunes a domingo.
        var porDiaSemana = efectivas.GroupBy(r => (int)r.Fecha.DayOfWeek).ToDictionary(g => g.Key, g => g.Count());
        for (var i = 0; i < 7; i++)
        {
            var dow = (i + 1) % 7; // 1=lunes ... 6=sábado, 0=domingo
            porDiaSemana.TryGetValue(dow, out var cantidad);
            dto.PorDiaSemana.Add(new PuntoDto { Etiqueta = NombresDia[i], Valor = cantidad });
        }

        return dto;
    }
}
