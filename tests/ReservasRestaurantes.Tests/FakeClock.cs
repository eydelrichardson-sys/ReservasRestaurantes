namespace ReservasRestaurantes.Tests;

/// <summary>Reloj controlable para que las pruebas no dependan de la fecha real.</summary>
public class FakeClock : TimeProvider
{
    private readonly DateTimeOffset _ahora;

    public FakeClock(DateTime local) =>
        _ahora = new DateTimeOffset(local, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _ahora;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
