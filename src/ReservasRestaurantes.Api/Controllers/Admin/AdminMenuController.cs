using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers.Admin;

[Route("api/admin/menu")]
public class AdminMenuController : AdminControllerBase
{
    private readonly AppDbContext _db;

    public AdminMenuController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<List<MenuItemDto>> Listar() =>
        await _db.MenuItems.AsNoTracking()
            .Where(m => m.IdRestaurante == IdRestaurante)
            .OrderBy(m => m.Categoria).ThenBy(m => m.Nombre)
            .Select(m => new MenuItemDto
            {
                Id = m.Id,
                Nombre = m.Nombre,
                Descripcion = m.Descripcion,
                Precio = m.Precio,
                Categoria = m.Categoria,
                Disponible = m.Disponible
            })
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<MenuItemDto>> Crear(MenuItemDto dto)
    {
        var item = new MenuItem
        {
            IdRestaurante = IdRestaurante,
            Nombre = dto.Nombre.Trim(),
            Descripcion = dto.Descripcion?.Trim(),
            Precio = dto.Precio,
            Categoria = dto.Categoria.Trim(),
            Disponible = dto.Disponible
        };
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return item.ToDto();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, MenuItemDto dto)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == id && m.IdRestaurante == IdRestaurante);
        if (item == null) return NotFound(new ApiError("Plato no encontrado."));

        item.Nombre = dto.Nombre.Trim();
        item.Descripcion = dto.Descripcion?.Trim();
        item.Precio = dto.Precio;
        item.Categoria = dto.Categoria.Trim();
        item.Disponible = dto.Disponible;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == id && m.IdRestaurante == IdRestaurante);
        if (item == null) return NotFound(new ApiError("Plato no encontrado."));

        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
