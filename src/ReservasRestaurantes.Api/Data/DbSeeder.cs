using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Data;

/// <summary>
/// Datos demo para probar rápido. Se desactiva con "Seed:Demo": false en appsettings.
/// Credenciales demo: admin@lacasona.example / Admin123!  y  admin@marisqueria.example / Admin123!
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Restaurantes.Any()) return;

        var r1 = new Restaurante
        {
            Nombre = "La Casona Dominicana",
            Descripcion = "Cocina criolla tradicional en un ambiente familiar. Sancocho, mofongo y la mejor bandera dominicana de la zona.",
            Direccion = "Av. George Washington #123, Santo Domingo",
            Telefono = "809-555-0101",
            Email = "info@lacasona.example",
            DuracionReservaMin = 90
        };
        var r2 = new Restaurante
        {
            Nombre = "Marisquería El Malecón",
            Descripcion = "Pescados y mariscos frescos frente al mar. Ideal para celebraciones y almuerzos de domingo.",
            Direccion = "Calle Mella #45, Boca Chica",
            Telefono = "809-555-0202",
            Email = "reservas@marisqueria.example",
            DuracionReservaMin = 120
        };
        db.Restaurantes.AddRange(r1, r2);
        db.SaveChanges();

        foreach (var r in new[] { r1, r2 })
        {
            for (var d = 0; d < 7; d++)
            {
                db.Horarios.Add(new Horario
                {
                    IdRestaurante = r.Id,
                    DiaSemana = d,
                    Apertura = new TimeOnly(11, 0),
                    Cierre = new TimeOnly(22, 0),
                    Cerrado = r.Id == r2.Id && d == 1 // la marisquería cierra los lunes
                });
            }

            var numero = 1;
            foreach (var cap in new[] { 2, 2, 4, 4, 4, 6, 8 })
                db.Mesas.Add(new Mesa { IdRestaurante = r.Id, Numero = numero++, Capacidad = cap });
        }

        db.MenuItems.AddRange(
            new MenuItem { IdRestaurante = r1.Id, Categoria = "Entradas", Nombre = "Yuca frita con queso", Descripcion = "Con salsa de ajo", Precio = 250 },
            new MenuItem { IdRestaurante = r1.Id, Categoria = "Platos principales", Nombre = "La Bandera", Descripcion = "Arroz blanco, habichuelas rojas y pollo guisado", Precio = 380 },
            new MenuItem { IdRestaurante = r1.Id, Categoria = "Platos principales", Nombre = "Sancocho de siete carnes", Descripcion = "Servido con arroz y aguacate", Precio = 450 },
            new MenuItem { IdRestaurante = r1.Id, Categoria = "Postres", Nombre = "Majarete", Descripcion = "Postre de maíz con canela", Precio = 150 },
            new MenuItem { IdRestaurante = r2.Id, Categoria = "Entradas", Nombre = "Ceviche de pescado", Descripcion = "Pesca del día con limón y cilantro", Precio = 420 },
            new MenuItem { IdRestaurante = r2.Id, Categoria = "Platos principales", Nombre = "Pescado frito", Descripcion = "Con tostones y ensalada", Precio = 650 },
            new MenuItem { IdRestaurante = r2.Id, Categoria = "Platos principales", Nombre = "Langosta a la parrilla", Descripcion = "Con mantequilla de ajo", Precio = 1600 },
            new MenuItem { IdRestaurante = r2.Id, Categoria = "Bebidas", Nombre = "Jugo natural", Descripcion = "Chinola, limón o tamarindo", Precio = 120 });

        var hash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        db.Usuarios.AddRange(
            new Usuario { Email = "admin@lacasona.example", PasswordHash = hash, Nombre = "Admin La Casona", Rol = "Admin", IdRestaurante = r1.Id },
            new Usuario { Email = "admin@marisqueria.example", PasswordHash = hash, Nombre = "Admin El Malecón", Rol = "Admin", IdRestaurante = r2.Id });

        db.SaveChanges();
    }

    /// <summary>
    /// Historial de ejemplo (reservas de los últimos 30 días, algunas calificaciones y unas
    /// reservas próximas pendientes) para que los reportes y el dashboard no estén vacíos.
    /// Solo se genera si todavía no existe ninguna reserva. Es determinista (misma semilla).
    /// Los correos terminan en @demo.example y no reciben recordatorios.
    /// </summary>
    public static void SeedHistorial(AppDbContext db)
    {
        if (db.Reservas.Any()) return;

        var restaurantes = db.Restaurantes.Include(r => r.Mesas).Include(r => r.Horarios).ToList();
        if (restaurantes.Count == 0) return;

        var rnd = new Random(42);
        var nombres = new[]
        {
            "María Pérez", "Juan Rodríguez", "Carolina Santos", "Luis Martínez", "Ana Gómez",
            "Pedro Almonte", "Laura Jiménez", "Rafael Peña", "Yesenia Díaz", "Carlos Núñez"
        };
        var positivos = new[]
        {
            "Excelente atención y la comida deliciosa.", "Todo muy rico, volveremos pronto.",
            "Ambiente familiar y buen servicio.", "La reserva fue rápida y nos esperaban con la mesa lista.",
            "Los mejores sabores criollos, muy recomendado."
        };
        var negativos = new[]
        {
            "La comida tardó más de lo esperado.", "El servicio puede mejorar.", "Esperamos bastante para ser atendidos."
        };
        var horas = new[] { 12, 13, 14, 18, 19, 20 }; // 20:00 + 120 min = 22:00 (cierre)
        var usados = new HashSet<(int IdRest, DateOnly Fecha, int Hora, int Mesa)>();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var n = 0;
        var aprobadas = new List<Reserva>();

        static bool Abierto(Restaurante r, DateOnly fecha)
        {
            var h = r.Horarios.FirstOrDefault(x => x.DiaSemana == (int)fecha.DayOfWeek);
            return h != null && !h.Cerrado;
        }

        Reserva Nueva(Restaurante r, Mesa mesa, DateOnly fecha, int hora, int personas, EstadoReserva estado)
        {
            n++;
            return new Reserva
            {
                IdRestaurante = r.Id,
                IdMesa = mesa.Id,
                NombreCliente = nombres[rnd.Next(nombres.Length)],
                EmailCliente = $"cliente{n}@demo.example",
                TelefonoCliente = $"809-555-{1000 + n:D4}",
                Fecha = fecha,
                Hora = new TimeOnly(hora, 0),
                CantidadPersonas = personas,
                Estado = estado,
                Codigo = $"DEMO{n:D4}",
                FechaCreacion = fecha.ToDateTime(new TimeOnly(9, 0)).AddDays(-rnd.Next(1, 4)),
                RecordatorioEnviado = true
            };
        }

        foreach (var r in restaurantes)
        {
            var mesas = r.Mesas.Where(m => m.Activa).OrderBy(m => m.Numero).ToList();
            if (mesas.Count == 0) continue;

            // Historial de los últimos 30 días
            for (var i = 0; i < 45; i++)
            {
                var fecha = hoy.AddDays(-rnd.Next(1, 31));
                if (!Abierto(r, fecha)) continue;

                var hora = horas[rnd.Next(horas.Length)];
                var personas = rnd.Next(1, 7);
                var mesa = mesas.Where(m => m.Capacidad >= personas).OrderBy(_ => rnd.Next()).FirstOrDefault();
                if (mesa == null || !usados.Add((r.Id, fecha, hora, mesa.Id))) continue;

                var p = rnd.NextDouble();
                var estado = p < 0.72 ? EstadoReserva.Aprobada : p < 0.86 ? EstadoReserva.Rechazada : EstadoReserva.Cancelada;

                var reserva = Nueva(r, mesa, fecha, hora, personas, estado);
                db.Reservas.Add(reserva);
                if (estado == EstadoReserva.Aprobada) aprobadas.Add(reserva);
            }

            // Reservas próximas para que el dashboard tenga trabajo pendiente
            var proximas = new[] { (0, 20, EstadoReserva.Pendiente), (0, 13, EstadoReserva.Aprobada), (1, 19, EstadoReserva.Pendiente), (1, 13, EstadoReserva.Pendiente), (2, 20, EstadoReserva.Pendiente) };
            var k = 0;
            foreach (var (dia, hora, estado) in proximas)
            {
                var fecha = hoy.AddDays(dia);
                if (!Abierto(r, fecha)) continue;
                var mesa = mesas[k++ % mesas.Count];
                if (!usados.Add((r.Id, fecha, hora, mesa.Id))) continue;
                var reserva = Nueva(r, mesa, fecha, hora, 2, estado);
                reserva.RecordatorioEnviado = false;
                db.Reservas.Add(reserva);
            }
        }

        db.SaveChanges();

        // Calificaciones para aproximadamente la mitad de las visitas aprobadas
        foreach (var reserva in aprobadas.Where(_ => rnd.NextDouble() < 0.55))
        {
            var x = rnd.Next(100);
            var cal = x < 55 ? 5 : x < 80 ? 4 : x < 92 ? 3 : x < 97 ? 2 : 1;
            var texto = cal >= 4 ? positivos[rnd.Next(positivos.Length)] : negativos[rnd.Next(negativos.Length)];

            db.Resenas.Add(new Resena
            {
                IdReserva = reserva.Id,
                IdRestaurante = reserva.IdRestaurante,
                Calificacion = cal,
                Comentario = texto,
                Fecha = reserva.Fecha.ToDateTime(reserva.Hora).AddHours(3)
            });
        }

        db.SaveChanges();
    }
}
