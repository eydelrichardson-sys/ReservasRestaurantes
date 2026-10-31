namespace ReservasRestaurantes.Shared.Dtos;

public class ApiError
{
    public string Mensaje { get; set; } = "";

    public ApiError() { }
    public ApiError(string mensaje) => Mensaje = mensaje;
}
