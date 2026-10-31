using System.Text.Json.Serialization;

namespace ReservasRestaurantes.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoReserva
{
    Pendiente,
    Aprobada,
    Rechazada,
    Cancelada
}
