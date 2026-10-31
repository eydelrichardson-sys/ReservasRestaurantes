using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Shared.Enums;

namespace ReservasRestaurantes.Api.Services;

public class RecordatorioOptions
{
    /// <summary>Apaga o enciende los recordatorios automáticos.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Se envía el recordatorio cuando faltan menos de estas horas para la reserva.</summary>
    public int HorasAntes { get; set; } = 24;

    /// <summary>Cada cuántos minutos se revisan las reservas.</summary>
    public int IntervaloMinutos { get; set; } = 10;
}

/// <summary>
/// Proceso automático en segundo plano: revisa periódicamente las reservas aprobadas que
/// empiezan dentro de las próximas N horas y les envía un correo de recordatorio una sola vez.
/// </summary>
public class RecordatorioService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecordatorioService> _log;
    private readonly RecordatorioOptions _opt;
    private readonly TimeProvider _clock;

    public RecordatorioService(
        IServiceScopeFactory scopes,
        IOptions<RecordatorioOptions> opt,
        TimeProvider clock,
        ILogger<RecordatorioService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _clock = clock;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_opt.Activo)
        {
            _log.LogInformation("Recordatorios automáticos desactivados (Recordatorios:Activo = false).");
            return;
        }

        try
        {
            // Espera inicial: deja que la API termine de preparar la base de datos.
            await Task.Delay(TimeSpan.FromSeconds(20), ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ProcesarAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error procesando recordatorios; se reintentará en el próximo ciclo.");
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _opt.IntervaloMinutos)), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal de la aplicación.
        }
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var ahora = _clock.GetLocalNow().DateTime;
        var hoy = DateOnly.FromDateTime(ahora);
        var limite = hoy.AddDays(2);
        var ventana = TimeSpan.FromHours(Math.Max(1, _opt.HorasAntes));

        var candidatas = await db.Reservas
            .Include(r => r.Restaurante)
            .Include(r => r.Mesa)
            .Where(r => r.Estado == EstadoReserva.Aprobada
                        && !r.RecordatorioEnviado
                        && r.Fecha >= hoy && r.Fecha <= limite)
            .ToListAsync(ct);

        var enviados = 0;
        foreach (var r in candidatas)
        {
            var falta = r.Fecha.ToDateTime(r.Hora) - ahora;
            if (falta <= TimeSpan.Zero || falta > ventana) continue;

            if (await email.EnviarRecordatorioAsync(r))
            {
                r.RecordatorioEnviado = true;
                await db.SaveChangesAsync(ct);
                enviados++;
            }
        }

        if (enviados > 0)
            _log.LogInformation("Se enviaron {Cantidad} recordatorio(s) de reserva.", enviados);
    }
}
