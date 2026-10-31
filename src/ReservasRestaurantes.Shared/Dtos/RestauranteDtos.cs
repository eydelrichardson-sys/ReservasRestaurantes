using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ReservasRestaurantes.Shared.Dtos;

public class RestauranteResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Direccion { get; set; } = "";
    public string? ImagenUrl { get; set; }

    /// <summary>Promedio de calificaciones (0 si todavía no tiene).</summary>
    public double Promedio { get; set; }
    public int TotalResenas { get; set; }
}

public class HorarioDto
{
    private static readonly string[] Dias =
        { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };

    /// <summary>0 = Domingo ... 6 = Sábado</summary>
    public int DiaSemana { get; set; }
    public TimeOnly Apertura { get; set; } = new(11, 0);
    public TimeOnly Cierre { get; set; } = new(22, 0);
    public bool Cerrado { get; set; }

    [JsonIgnore]
    public string NombreDia => Dias[Math.Clamp(DiaSemana, 0, 6)];
}

public class MenuItemDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio"), StringLength(120)]
    public string Nombre { get; set; } = "";

    [StringLength(400)]
    public string? Descripcion { get; set; }

    [Range(0, 100000, ErrorMessage = "Precio inválido")]
    public decimal Precio { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria"), StringLength(60)]
    public string Categoria { get; set; } = "Platos principales";

    public bool Disponible { get; set; } = true;
}

public class RestauranteDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Direccion { get; set; } = "";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? ImagenUrl { get; set; }
    public int DuracionReservaMin { get; set; }
    public int TotalMesas { get; set; }
    public int CapacidadTotal { get; set; }
    public double Promedio { get; set; }
    public int TotalResenas { get; set; }
    public List<HorarioDto> Horarios { get; set; } = new();
    public List<MenuItemDto> Menu { get; set; } = new();
}

public class RestauranteEditDto
{
    [Required(ErrorMessage = "El nombre es obligatorio"), StringLength(120)]
    public string Nombre { get; set; } = "";

    [StringLength(600)]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "La dirección es obligatoria"), StringLength(200)]
    public string Direccion { get; set; } = "";

    [StringLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Correo inválido"), StringLength(150)]
    public string? Email { get; set; }

    [StringLength(400)]
    public string? ImagenUrl { get; set; }

    [Range(30, 240, ErrorMessage = "La duración debe estar entre 30 y 240 minutos")]
    public int DuracionReservaMin { get; set; } = 90;
}

public class MesaDto
{
    public int Id { get; set; }

    [Range(1, 999, ErrorMessage = "Número de mesa inválido")]
    public int Numero { get; set; }

    [Range(1, 50, ErrorMessage = "La capacidad debe estar entre 1 y 50")]
    public int Capacidad { get; set; } = 4;

    public bool Activa { get; set; } = true;
}
