using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers;

/// <summary>Endpoints públicos: perfiles de restaurantes, disponibilidad y opiniones.</summary>
[ApiController]
[Route("api/restaurantes")]
public class RestaurantesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DisponibilidadService _disp;

    public RestaurantesController(AppDbContext db, DisponibilidadService disp)
    {
        _db = db;
        _disp = disp;
    }

    [HttpGet]
    public async Task<List<RestauranteResumenDto>> Listar()
    {
        var stats = await _db.Resenas.AsNoTracking()
            .GroupBy(x => x.IdRestaurante)
            .Select(g => new { Id = g.Key, Promedio = g.Average(x => x.Calificacion), Total = g.Count() })
            .ToDictionaryAsync(x => x.Id);

        var lista = await _db.Restaurantes.AsNoTracking()
            .Where(r => r.Activo)
            .OrderBy(r => r.Nombre)
            .Select(r => new RestauranteResumenDto
            {
                Id = r.Id,
                Nombre = r.Nombre,
                Descripcion = r.Descripcion,
                Direccion = r.Direccion,
                ImagenUrl = r.ImagenUrl
            })
            .ToListAsync();

        foreach (var r in lista)
        {
            if (stats.TryGetValue(r.Id, out var s))
            {
                r.Promedio = Math.Round(s.Promedio, 1);
                r.TotalResenas = s.Total;
            }
        }

        return lista;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RestauranteDetalleDto>> Obtener(int id)
    {
        var r = await _db.Restaurantes.AsNoTracking()
            .Include(x => x.Horarios)
            .Include(x => x.Mesas)
            .Include(x => x.MenuItems)
            .FirstOrDefaultAsync(x => x.Id == id && x.Activo);

        if (r == null) return NotFound(new ApiError("Restaurante no encontrado."));

        var calificaciones = await _db.Resenas.AsNoTracking()
            .Where(x => x.IdRestaurante == id)
            .Select(x => x.Calificacion)
            .ToListAsync();

        var promedio = calificaciones.Count == 0 ? 0 : Math.Round(calificaciones.Average(), 1);
        return RestauranteDetalleBuilder.Crear(r, soloMenuDisponible: true, promedio, calificaciones.Count);
    }

    /// <summary>Últimas opiniones del restaurante (solo primer nombre del cliente).</summary>
    [HttpGet("{id:int}/resenas")]
    public async Task<List<ResenaDto>> Resenas(int id)
    {
        var lista = await _db.Resenas.AsNoTracking()
            .Include(x => x.Reserva)
            .Where(x => x.IdRestaurante == id)
            .OrderByDescending(x => x.Fecha)
            .Take(20)
            .ToListAsync();

        return lista.Select(x => new ResenaDto
        {
            Id = x.Id,
            Nombre = PrimerNombre(x.Reserva?.NombreCliente),
            Calificacion = x.Calificacion,
            Comentario = x.Comentario,
            Fecha = x.Fecha
        }).ToList();
    }

    [HttpGet("{id:int}/disponibilidad")]
    [EnableRateLimiting("publico")]
    public async Task<ActionResult<List<SlotDto>>> Disponibilidad(int id, [FromQuery] DateOnly fecha, [FromQuery] int personas = 2)
    {
        if (personas < 1 || personas > 20)
            return BadRequest(new ApiError("La cantidad de personas debe estar entre 1 y 20."));

        var slots = await _disp.CalcularAsync(id, fecha, personas);
        return slots.Select(s => new SlotDto { Hora = s.Hora, Disponible = s.Mesa != null }).ToList();
    }

    private static string PrimerNombre(string? nombreCompleto)
    {
        var primero = (nombreCompleto ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(primero) ? "Cliente" : primero;
    }
}

public static class RestauranteDetalleBuilder
{
    public static RestauranteDetalleDto Crear(Restaurante r, bool soloMenuDisponible, double promedio = 0, int totalResenas = 0)
    {
        var mesasActivas = r.Mesas.Where(m => m.Activa).ToList();
        return new RestauranteDetalleDto
        {
            Id = r.Id,
            Nombre = r.Nombre,
            Descripcion = r.Descripcion,
            Direccion = r.Direccion,
            Telefono = r.Telefono,
            Email = r.Email,
            ImagenUrl = r.ImagenUrl,
            DuracionReservaMin = r.DuracionReservaMin,
            TotalMesas = mesasActivas.Count,
            CapacidadTotal = mesasActivas.Sum(m => m.Capacidad),
            Promedio = promedio,
            TotalResenas = totalResenas,
            Horarios = r.Horarios.OrderBy(h => (h.DiaSemana + 6) % 7).Select(h => h.ToDto()).ToList(), // Lunes primero
            Menu = r.MenuItems
                .Where(m => !soloMenuDisponible || m.Disponible)
                .OrderBy(m => m.Categoria).ThenBy(m => m.Nombre)
                .Select(m => m.ToDto()).ToList()
        };
    }
}
