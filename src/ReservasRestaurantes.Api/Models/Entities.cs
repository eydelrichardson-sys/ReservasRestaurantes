using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Models;

public class Restaurante
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Direccion { get; set; } = "";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? ImagenUrl { get; set; }
    public int DuracionReservaMin { get; set; } = 90;
    public bool Activo { get; set; } = true;

    public ICollection<Horario> Horarios { get; set; } = new List<Horario>();
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = "Admin";
    public int? IdRestaurante { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Intentos de login fallidos seguidos (se reinicia al entrar o al bloquear).</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Mientras sea futuro, la cuenta no puede iniciar sesión.</summary>
    public DateTime? BloqueadoHasta { get; set; }

    public Restaurante? Restaurante { get; set; }
}

public class Horario
{
    public int Id { get; set; }
    public int IdRestaurante { get; set; }
    public int DiaSemana { get; set; }
    public TimeOnly Apertura { get; set; }
    public TimeOnly Cierre { get; set; }
    public bool Cerrado { get; set; }

    public Restaurante? Restaurante { get; set; }
}

public class Mesa
{
    public int Id { get; set; }
    public int IdRestaurante { get; set; }
    public int Numero { get; set; }
    public int Capacidad { get; set; }
    public bool Activa { get; set; } = true;

    public Restaurante? Restaurante { get; set; }
}

public class MenuItem
{
    public int Id { get; set; }
    public int IdRestaurante { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Categoria { get; set; } = "";
    public bool Disponible { get; set; } = true;

    public Restaurante? Restaurante { get; set; }
}

public class Reserva
{
    public int Id { get; set; }
    public int IdRestaurante { get; set; }
    public int? IdMesa { get; set; }
    public string NombreCliente { get; set; } = "";
    public string EmailCliente { get; set; } = "";
    public string TelefonoCliente { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public TimeOnly Hora { get; set; }
    public int CantidadPersonas { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;
    public string? Notas { get; set; }
    public string? MotivoRechazo { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    /// <summary>true cuando ya se envió el correo de recordatorio previo a la visita.</summary>
    public bool RecordatorioEnviado { get; set; }

    public Restaurante? Restaurante { get; set; }
    public Mesa? Mesa { get; set; }
}

public class Resena
{
    public int Id { get; set; }
    public int IdReserva { get; set; }
    public int IdRestaurante { get; set; }
    public int Calificacion { get; set; }
    public string? Comentario { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    public Reserva? Reserva { get; set; }
    public Restaurante? Restaurante { get; set; }
}
