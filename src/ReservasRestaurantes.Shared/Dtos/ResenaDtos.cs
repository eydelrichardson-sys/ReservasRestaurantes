using System.ComponentModel.DataAnnotations;

namespace ReservasRestaurantes.Shared.Dtos;

public class CrearResenaRequest
{
    [Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5 estrellas.")]
    public int Calificacion { get; set; }

    [StringLength(500, ErrorMessage = "El comentario no puede pasar de 500 caracteres.")]
    public string? Comentario { get; set; }
}

public class ResenaDto
{
    public int Id { get; set; }

    /// <summary>Solo el primer nombre, por privacidad.</summary>
    public string Nombre { get; set; } = "";

    public int Calificacion { get; set; }
    public string? Comentario { get; set; }
    public DateTime Fecha { get; set; }
}
