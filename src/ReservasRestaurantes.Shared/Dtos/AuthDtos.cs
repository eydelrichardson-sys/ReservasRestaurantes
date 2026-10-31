using System.ComponentModel.DataAnnotations;

namespace ReservasRestaurantes.Shared.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "Correo inválido")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; set; } = "";
}

public class RegistroRestauranteRequest
{
    [Required(ErrorMessage = "El nombre del restaurante es obligatorio."), StringLength(120)]
    public string NombreRestaurante { get; set; } = "";

    [Required(ErrorMessage = "La dirección es obligatoria."), StringLength(200)]
    public string Direccion { get; set; } = "";

    [StringLength(30)]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "Tu nombre es obligatorio."), StringLength(120)]
    public string NombreAdmin { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Correo inválido."), StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria."), MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres."), StringLength(100)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirma tu contraseña."), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = "";
    public int? IdRestaurante { get; set; }
    public DateTime Expira { get; set; }
}
