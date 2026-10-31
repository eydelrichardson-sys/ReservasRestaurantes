using System.ComponentModel.DataAnnotations;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Shared.Dtos;

public class SlotDto
{
    public TimeOnly Hora { get; set; }
    public bool Disponible { get; set; }
}

public class CrearReservaRequest
{
    public int IdRestaurante { get; set; }

    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public TimeOnly Hora { get; set; }

    [Range(1, 20, ErrorMessage = "Entre 1 y 20 personas")]
    public int CantidadPersonas { get; set; } = 2;

    [Required(ErrorMessage = "Tu nombre es obligatorio"), StringLength(120)]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio"), EmailAddress(ErrorMessage = "Correo inválido"), StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio"), StringLength(30)]
    public string Telefono { get; set; } = "";

    [StringLength(400)]
    public string? Notas { get; set; }
}

public class ReservaDto
{
    public int Id { get; set; }
    public int IdRestaurante { get; set; }
    public string NombreRestaurante { get; set; } = "";
    public int? IdMesa { get; set; }
    public int? NumeroMesa { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefono { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public TimeOnly Hora { get; set; }
    public int CantidadPersonas { get; set; }
    public EstadoReserva Estado { get; set; }
    public string? Notas { get; set; }
    public string? MotivoRechazo { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime FechaCreacion { get; set; }

    /// <summary>La reserva fue aprobada, ya pasó la hora y aún no tiene calificación.</summary>
    public bool PuedeCalificar { get; set; }
    public bool YaCalificada { get; set; }
}

public class RechazarReservaRequest
{
    [StringLength(300)]
    public string? Motivo { get; set; }
}

public class MesaEstadoDto
{
    public int IdMesa { get; set; }
    public int Numero { get; set; }
    public int Capacidad { get; set; }
    public bool Ocupada { get; set; }
    public string? Cliente { get; set; }
    public TimeOnly? Hasta { get; set; }
}

public class OcupacionDto
{
    public DateTime Ahora { get; set; }
    public int TotalMesas { get; set; }
    public int MesasOcupadas { get; set; }
    public int ReservasPendientes { get; set; }
    public int ReservasHoy { get; set; }
    public double PorcentajeOcupacion { get; set; }
    public List<MesaEstadoDto> Mesas { get; set; } = new();
}
