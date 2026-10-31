using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Models;

public static class Mappings
{
    /// <summary>Requiere que Restaurante y Mesa estén cargados (Include) para poblar los nombres.</summary>
    public static ReservaDto ToDto(this Reserva r) => new()
    {
        Id = r.Id,
        IdRestaurante = r.IdRestaurante,
        NombreRestaurante = r.Restaurante?.Nombre ?? "",
        IdMesa = r.IdMesa,
        NumeroMesa = r.Mesa?.Numero,
        Nombre = r.NombreCliente,
        Email = r.EmailCliente,
        Telefono = r.TelefonoCliente,
        Fecha = r.Fecha,
        Hora = r.Hora,
        CantidadPersonas = r.CantidadPersonas,
        Estado = r.Estado,
        Notas = r.Notas,
        MotivoRechazo = r.MotivoRechazo,
        Codigo = r.Codigo,
        FechaCreacion = r.FechaCreacion
    };

    public static MesaDto ToDto(this Mesa m) => new()
    {
        Id = m.Id,
        Numero = m.Numero,
        Capacidad = m.Capacidad,
        Activa = m.Activa
    };

    public static MenuItemDto ToDto(this MenuItem m) => new()
    {
        Id = m.Id,
        Nombre = m.Nombre,
        Descripcion = m.Descripcion,
        Precio = m.Precio,
        Categoria = m.Categoria,
        Disponible = m.Disponible
    };

    public static HorarioDto ToDto(this Horario h) => new()
    {
        DiaSemana = h.DiaSemana,
        Apertura = h.Apertura,
        Cierre = h.Cierre,
        Cerrado = h.Cerrado
    };
}
