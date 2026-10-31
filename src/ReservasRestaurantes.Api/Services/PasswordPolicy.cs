namespace ReservasRestaurantes.Api.Services;

/// <summary>Política mínima de contraseñas para cuentas nuevas.</summary>
public static class PasswordPolicy
{
    public const int LongitudMinima = 8;

    /// <summary>Devuelve null si la contraseña cumple la política; si no, el motivo.</summary>
    public static string? Validar(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        if (!password.Any(char.IsUpper))
            return "La contraseña debe incluir al menos una letra mayúscula.";
        if (!password.Any(char.IsLower))
            return "La contraseña debe incluir al menos una letra minúscula.";
        if (!password.Any(char.IsDigit))
            return "La contraseña debe incluir al menos un número.";
        return null;
    }
}
