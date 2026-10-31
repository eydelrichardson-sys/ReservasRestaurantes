using Xunit;
using ReservasRestaurantes.Api.Services;

namespace ReservasRestaurantes.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Admin123!")]
    [InlineData("Segura2026")]
    [InlineData("abcDEF12")]
    public void ContrasenasValidas_NoDevuelvenError(string pass) =>
        Assert.Null(PasswordPolicy.Validar(pass));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1")]            // muy corta
    [InlineData("minuscula123")]   // sin mayúscula
    [InlineData("MAYUSCULA123")]   // sin minúscula
    [InlineData("SinNumeros")]     // sin dígito
    public void ContrasenasDebiles_DevuelvenError(string? pass) =>
        Assert.NotNull(PasswordPolicy.Validar(pass));
}
