using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ReservasRestaurantes.Api.Models;

namespace ReservasRestaurantes.Api.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Reservas";
}

public interface IEmailService
{
    /// <summary>Correo al aprobar la reserva. true si se envió (o se simuló por log cuando no hay SMTP configurado).</summary>
    Task<bool> EnviarConfirmacionAsync(Reserva reserva);

    /// <summary>Recordatorio previo a la visita (lo dispara RecordatorioService).</summary>
    Task<bool> EnviarRecordatorioAsync(Reserva reserva);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailService> _log;

    public EmailService(IOptions<SmtpSettings> smtp, ILogger<EmailService> log)
    {
        _smtp = smtp.Value;
        _log = log;
    }

    public Task<bool> EnviarConfirmacionAsync(Reserva r)
    {
        var restaurante = r.Restaurante?.Nombre ?? "el restaurante";
        return EnviarAsync(r,
            $"Reserva confirmada en {restaurante}",
            ConstruirHtml(r, restaurante,
                titulo: "¡Tu reserva está confirmada!",
                intro: $"<b>{E(restaurante)}</b> aprobó tu reserva.",
                color: "#198754"));
    }

    public Task<bool> EnviarRecordatorioAsync(Reserva r)
    {
        var restaurante = r.Restaurante?.Nombre ?? "el restaurante";
        return EnviarAsync(r,
            $"Recordatorio: tu reserva en {restaurante}",
            ConstruirHtml(r, restaurante,
                titulo: "Te esperamos pronto",
                intro: $"Te recordamos tu reserva en <b>{E(restaurante)}</b>.",
                color: "#c8553d"));
    }

    private async Task<bool> EnviarAsync(Reserva r, string asunto, string html)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host))
        {
            _log.LogWarning(
                "SMTP no configurado. Correo simulado a {Email}: {Asunto} (código {Codigo})",
                r.EmailCliente, asunto, r.Codigo);
            return true;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.From));
            msg.To.Add(new MailboxAddress(r.NombreCliente, r.EmailCliente));
            msg.Subject = asunto;
            msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(_smtp.User))
                await client.AuthenticateAsync(_smtp.User, _smtp.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo enviar el correo '{Asunto}' de la reserva {Id}", asunto, r.Id);
            return false;
        }
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string ConstruirHtml(Reserva r, string restaurante, string titulo, string intro, string color)
    {
        var mesa = r.Mesa != null ? $"<li><b>Mesa:</b> {r.Mesa.Numero}</li>" : "";

        return $@"
<div style='font-family:Arial,sans-serif;max-width:520px;margin:auto'>
  <h2 style='color:{color}'>{titulo}</h2>
  <p>Hola {E(r.NombreCliente)}, {intro}</p>
  <ul>
    <li><b>Fecha:</b> {r.Fecha:dd/MM/yyyy}</li>
    <li><b>Hora:</b> {r.Hora:HH\:mm}</li>
    <li><b>Personas:</b> {r.CantidadPersonas}</li>
    {mesa}
    <li><b>Código de reserva:</b> {E(r.Codigo)}</li>
  </ul>
  <p>Si no puedes asistir, cancela con tu código desde la sección ""Mi reserva"" del sitio.</p>
  <p style='color:#6c757d;font-size:12px'>Este es un mensaje automático, por favor no respondas a este correo.</p>
</div>";
    }
}
