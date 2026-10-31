using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers.Admin;

[Route("api/admin/mesas")]
public class AdminMesasController : AdminControllerBase
{
    private readonly AppDbContext _db;

    public AdminMesasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<List<MesaDto>> Listar() =>
        await _db.Mesas.AsNoTracking()
            .Where(m => m.IdRestaurante == IdRestaurante)
            .OrderBy(m => m.Numero)
            .Select(m => new MesaDto { Id = m.Id, Numero = m.Numero, Capacidad = m.Capacidad, Activa = m.Activa })
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<MesaDto>> Crear(MesaDto dto)
    {
        if (await _db.Mesas.AnyAsync(m => m.IdRestaurante == IdRestaurante && m.Numero == dto.Numero))
            return Conflict(new ApiError($"Ya existe la mesa {dto.Numero}."));

        var mesa = new Mesa
        {
            IdRestaurante = IdRestaurante,
            Numero = dto.Numero,
            Capacidad = dto.Capacidad,
            Activa = dto.Activa
        };
        _db.Mesas.Add(mesa);
        await _db.SaveChangesAsync();
        return mesa.ToDto();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, MesaDto dto)
    {
        var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.Id == id && m.IdRestaurante == IdRestaurante);
        if (mesa == null) return NotFound(new ApiError("Mesa no encontrada."));

        if (await _db.Mesas.AnyAsync(m => m.IdRestaurante == IdRestaurante && m.Numero == dto.Numero && m.Id != id))
            return Conflict(new ApiError($"Ya existe la mesa {dto.Numero}."));

        mesa.Numero = dto.Numero;
        mesa.Capacidad = dto.Capacidad;
        mesa.Activa = dto.Activa;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.Id == id && m.IdRestaurante == IdRestaurante);
        if (mesa == null) return NotFound(new ApiError("Mesa no encontrada."));

        if (await _db.Reservas.AnyAsync(r => r.IdMesa == id))
            return Conflict(new ApiError("La mesa tiene reservas en el historial. Desactívala en lugar de eliminarla."));

        _db.Mesas.Remove(mesa);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
