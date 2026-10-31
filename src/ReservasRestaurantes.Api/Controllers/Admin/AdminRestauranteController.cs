using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers.Admin;

[Route("api/admin/restaurante")]
public class AdminRestauranteController : AdminControllerBase
{
    private readonly AppDbContext _db;

    public AdminRestauranteController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<RestauranteDetalleDto>> Obtener()
    {
        var r = await _db.Restaurantes.AsNoTracking()
            .Include(x => x.Horarios)
            .Include(x => x.Mesas)
            .Include(x => x.MenuItems)
            .FirstOrDefaultAsync(x => x.Id == IdRestaurante);

        if (r == null) return NotFound(new ApiError("Restaurante no encontrado."));
        return RestauranteDetalleBuilder.Crear(r, soloMenuDisponible: false);
    }

    [HttpPut]
    public async Task<IActionResult> Actualizar(RestauranteEditDto dto)
    {
        var r = await _db.Restaurantes.FindAsync(IdRestaurante);
        if (r == null) return NotFound(new ApiError("Restaurante no encontrado."));

        r.Nombre = dto.Nombre.Trim();
        r.Descripcion = dto.Descripcion?.Trim();
        r.Direccion = dto.Direccion.Trim();
        r.Telefono = dto.Telefono?.Trim();
        r.Email = dto.Email?.Trim();
        r.ImagenUrl = string.IsNullOrWhiteSpace(dto.ImagenUrl) ? null : dto.ImagenUrl.Trim();
        r.DuracionReservaMin = dto.DuracionReservaMin;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("horarios")]
    public async Task<IActionResult> ActualizarHorarios(List<HorarioDto> horarios)
    {
        if (horarios.Count != 7 || horarios.Select(h => h.DiaSemana).Distinct().Count() != 7
            || horarios.Any(h => h.DiaSemana < 0 || h.DiaSemana > 6))
            return BadRequest(new ApiError("Debes enviar exactamente un horario por cada día de la semana."));

        var invalido = horarios.FirstOrDefault(h => !h.Cerrado && h.Apertura >= h.Cierre);
        if (invalido != null)
            return BadRequest(new ApiError($"{invalido.NombreDia}: la hora de apertura debe ser menor que la de cierre."));

        var existentes = await _db.Horarios.Where(h => h.IdRestaurante == IdRestaurante).ToListAsync();
        foreach (var dto in horarios)
        {
            var h = existentes.FirstOrDefault(x => x.DiaSemana == dto.DiaSemana);
            if (h == null)
            {
                h = new Horario { IdRestaurante = IdRestaurante, DiaSemana = dto.DiaSemana };
                _db.Horarios.Add(h);
            }
            h.Apertura = dto.Apertura;
            h.Cierre = dto.Cierre;
            h.Cerrado = dto.Cerrado;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
