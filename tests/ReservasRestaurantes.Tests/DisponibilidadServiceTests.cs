using Xunit;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Tests;

public class DisponibilidadServiceTests
{
    // "Hoy" para las pruebas: viernes 30/10/2026 a las 10:00
    private static readonly DateTime Ahora = new(2026, 10, 30, 10, 0, 0);
    private static readonly DateOnly Viernes = new(2026, 11, 6);   // viernes futuro
    private static readonly DateOnly Domingo = new(2026, 11, 8);   // domingo (cerrado en la prueba)

    private static (AppDbContext db, DisponibilidadService svc) Crear(int capacidadMesa = 4)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(opciones);

        var rest = new Restaurante { Id = 1, Nombre = "Test", Direccion = "Calle 1", DuracionReservaMin = 90 };
        db.Restaurantes.Add(rest);
        for (var d = 0; d < 7; d++)
        {
            db.Horarios.Add(new Horario
            {
                IdRestaurante = 1,
                DiaSemana = d,
                Apertura = new TimeOnly(11, 0),
                Cierre = new TimeOnly(22, 0),
                Cerrado = d == (int)DayOfWeek.Sunday
            });
        }
        db.Mesas.Add(new Mesa { Id = 1, IdRestaurante = 1, Numero = 1, Capacidad = capacidadMesa });
        db.SaveChanges();

        return (db, new DisponibilidadService(db, new FakeClock(Ahora)));
    }

    private static void AgregarReserva(AppDbContext db, TimeOnly hora, EstadoReserva estado)
    {
        db.Reservas.Add(new Reserva
        {
            IdRestaurante = 1,
            IdMesa = 1,
            NombreCliente = "Ana",
            EmailCliente = "ana@test.com",
            TelefonoCliente = "809",
            Fecha = Viernes,
            Hora = hora,
            CantidadPersonas = 2,
            Estado = estado,
            Codigo = Guid.NewGuid().ToString("N")[..8].ToUpper()
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task DiaCerrado_NoDevuelveHorarios()
    {
        var (_, svc) = Crear();
        Assert.Empty(await svc.CalcularAsync(1, Domingo, 2));
    }

    [Fact]
    public async Task DiaAbierto_GeneraVeinteHorariosCadaTreintaMinutos()
    {
        var (_, svc) = Crear();
        var slots = await svc.CalcularAsync(1, Viernes, 2);

        Assert.Equal(20, slots.Count);                       // 11:00 .. 20:30 (la reserva dura 90 min)
        Assert.Equal(new TimeOnly(11, 0), slots.First().Hora);
        Assert.Equal(new TimeOnly(20, 30), slots.Last().Hora);
        Assert.All(slots, s => Assert.NotNull(s.Mesa));
    }

    [Fact]
    public async Task ReservaExistente_BloqueaSoloLosHorariosQueSeSolapan()
    {
        var (db, svc) = Crear();
        AgregarReserva(db, new TimeOnly(18, 0), EstadoReserva.Aprobada);   // ocupa 18:00 - 19:30

        var slots = (await svc.CalcularAsync(1, Viernes, 2)).ToDictionary(s => s.Hora);

        Assert.NotNull(slots[new TimeOnly(16, 30)].Mesa);   // termina 18:00 justo cuando empieza la otra
        Assert.Null(slots[new TimeOnly(17, 0)].Mesa);       // se solapa
        Assert.Null(slots[new TimeOnly(18, 0)].Mesa);
        Assert.Null(slots[new TimeOnly(19, 0)].Mesa);
        Assert.NotNull(slots[new TimeOnly(19, 30)].Mesa);   // empieza cuando la otra termina
    }

    [Fact]
    public async Task ReservaPendiente_TambienOcupaLaMesa()
    {
        var (db, svc) = Crear();
        AgregarReserva(db, new TimeOnly(13, 0), EstadoReserva.Pendiente);

        var slots = (await svc.CalcularAsync(1, Viernes, 2)).ToDictionary(s => s.Hora);
        Assert.Null(slots[new TimeOnly(13, 0)].Mesa);
    }

    [Theory]
    [InlineData(EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Cancelada)]
    public async Task ReservaRechazadaOCancelada_LiberaLaMesa(EstadoReserva estado)
    {
        var (db, svc) = Crear();
        AgregarReserva(db, new TimeOnly(13, 0), estado);

        var slots = (await svc.CalcularAsync(1, Viernes, 2)).ToDictionary(s => s.Hora);
        Assert.NotNull(slots[new TimeOnly(13, 0)].Mesa);
    }

    [Fact]
    public async Task GrupoMasGrandeQueLaMesa_NoTieneDisponibilidad()
    {
        var (_, svc) = Crear(capacidadMesa: 4);
        var slots = await svc.CalcularAsync(1, Viernes, 6);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Null(s.Mesa));
    }

    [Fact]
    public async Task Hoy_NoOfreceHorasQueYaPasaron()
    {
        // Se calcula "hoy" (30/10/2026, 10:00): todo empieza a las 11:00 o después, así que aún están libres.
        var (db, _) = Crear();
        var tarde = new DisponibilidadService(db, new FakeClock(new DateTime(2026, 10, 30, 15, 10, 0)));

        var slots = (await tarde.CalcularAsync(1, new DateOnly(2026, 10, 30), 2)).ToDictionary(s => s.Hora);

        Assert.Null(slots[new TimeOnly(15, 0)].Mesa);        // ya pasó
        Assert.Null(slots[new TimeOnly(11, 0)].Mesa);
        Assert.NotNull(slots[new TimeOnly(15, 30)].Mesa);    // todavía no
    }

    [Fact]
    public async Task FechaPasada_NoDevuelveHorarios()
    {
        var (_, svc) = Crear();
        Assert.Empty(await svc.CalcularAsync(1, new DateOnly(2026, 10, 1), 2));
    }
}
