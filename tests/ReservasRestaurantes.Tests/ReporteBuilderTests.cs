using Xunit;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Tests;

public class ReporteBuilderTests
{
    private static Reserva R(DateOnly fecha, int hora, int personas, EstadoReserva estado) => new()
    {
        Fecha = fecha,
        Hora = new TimeOnly(hora, 0),
        CantidadPersonas = personas,
        Estado = estado
    };

    private static readonly DateOnly D1 = new(2026, 11, 2);   // lunes

    [Fact]
    public void CuentaReservasPorEstadoYCalculaTasas()
    {
        var reservas = new List<Reserva>
        {
            R(D1, 19, 4, EstadoReserva.Aprobada),
            R(D1, 20, 2, EstadoReserva.Aprobada),
            R(D1, 20, 3, EstadoReserva.Rechazada),
            R(D1, 13, 2, EstadoReserva.Cancelada),
            R(D1, 14, 2, EstadoReserva.Pendiente),
        };
        var resenas = new List<Resena>
        {
            new() { Calificacion = 5 },
            new() { Calificacion = 4 },
        };

        var r = ReporteBuilder.Crear(D1, D1.AddDays(6), reservas, resenas);

        Assert.Equal(5, r.TotalReservas);
        Assert.Equal(2, r.Aprobadas);
        Assert.Equal(1, r.Rechazadas);
        Assert.Equal(1, r.Canceladas);
        Assert.Equal(1, r.Pendientes);
        Assert.Equal(6, r.PersonasAprobadas);
        Assert.Equal(67, r.TasaAprobacion);   // 2 aprobadas de 3 resueltas (redondeado)
        Assert.Equal(4.5, r.PromedioCalificacion);
        Assert.Equal(2, r.TotalResenas);
    }

    [Fact]
    public void PorDia_IncluyeTodosLosDiasDelRango()
    {
        var r = ReporteBuilder.Crear(D1, D1.AddDays(6), new List<Reserva>(), new List<Resena>());

        Assert.Equal(7, r.PorDia.Count);
        Assert.All(r.PorDia, p => Assert.Equal(0, p.Valor));
        Assert.Equal(7, r.PorDiaSemana.Count);
    }

    [Fact]
    public void SinReservas_NoDivideEntreCero()
    {
        var r = ReporteBuilder.Crear(D1, D1, new List<Reserva>(), new List<Resena>());

        Assert.Equal(0, r.TasaAprobacion);
        Assert.Equal(0, r.PromedioCalificacion);
        Assert.Equal(0, r.PromedioPersonas);
    }
}
