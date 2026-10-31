namespace ReservasRestaurantes.Shared.Dtos;

public class PuntoDto
{
    public string Etiqueta { get; set; } = "";
    public int Valor { get; set; }
}

public class ReporteResumenDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }

    public int TotalReservas { get; set; }
    public int Pendientes { get; set; }
    public int Aprobadas { get; set; }
    public int Rechazadas { get; set; }
    public int Canceladas { get; set; }

    /// <summary>Suma de personas de las reservas aprobadas.</summary>
    public int PersonasAprobadas { get; set; }

    public double PromedioPersonas { get; set; }

    /// <summary>Aprobadas / (Aprobadas + Rechazadas), en porcentaje.</summary>
    public double TasaAprobacion { get; set; }

    public double PromedioCalificacion { get; set; }
    public int TotalResenas { get; set; }

    /// <summary>Reservas efectivas (pendientes + aprobadas) por día del rango.</summary>
    public List<PuntoDto> PorDia { get; set; } = new();

    /// <summary>Reservas efectivas por hora de inicio (horas pico).</summary>
    public List<PuntoDto> PorHora { get; set; } = new();

    /// <summary>Reservas efectivas por día de la semana (Lunes a Domingo).</summary>
    public List<PuntoDto> PorDiaSemana { get; set; } = new();
}
