using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Models;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Api.Controllers.Admin;

/// <summary>Reportes y estadísticas del restaurante con exportación a Excel y PDF.</summary>
[Route("api/admin/reportes")]
public class AdminReportesController : AdminControllerBase
{
    private const string MimeExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AppDbContext _db;
    private readonly ILogger<AdminReportesController> _log;

    public AdminReportesController(AppDbContext db, ILogger<AdminReportesController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<ReporteResumenDto>> Resumen([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        var error = ValidarRango(desde, hasta);
        if (error != null) return BadRequest(new ApiError(error));

        var (reservas, resenas) = await CargarAsync(desde, hasta);
        return ReporteBuilder.Crear(desde, hasta, reservas, resenas);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        var error = ValidarRango(desde, hasta);
        if (error != null) return BadRequest(new ApiError(error));

        var (reservas, resenas) = await CargarAsync(desde, hasta);
        var r = ReporteBuilder.Crear(desde, hasta, reservas, resenas);

        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add("Resumen");
        ws.Cell(1, 1).Value = "Reporte de reservas";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(2, 1).Value = $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

        var filas = new (string, XLCellValue)[]
        {
            ("Total de reservas", r.TotalReservas),
            ("Pendientes", r.Pendientes),
            ("Aprobadas", r.Aprobadas),
            ("Rechazadas", r.Rechazadas),
            ("Canceladas", r.Canceladas),
            ("Personas (aprobadas)", r.PersonasAprobadas),
            ("Promedio de personas", r.PromedioPersonas),
            ("Tasa de aprobación (%)", r.TasaAprobacion),
            ("Calificación promedio", r.PromedioCalificacion),
            ("Total de reseñas", r.TotalResenas),
        };
        var fila = 4;
        foreach (var (k, v) in filas)
        {
            ws.Cell(fila, 1).Value = k;
            ws.Cell(fila, 2).Value = v;
            fila++;
        }
        ws.Range(4, 1, fila - 1, 1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        var wr = wb.Worksheets.Add("Reservas");
        var enc = new[] { "Código", "Fecha", "Hora", "Cliente", "Correo", "Teléfono", "Personas", "Mesa", "Estado", "Notas" };
        for (var i = 0; i < enc.Length; i++) wr.Cell(1, i + 1).Value = enc[i];
        var cab = wr.Range(1, 1, 1, enc.Length);
        cab.Style.Font.Bold = true;
        cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#c8553d");
        cab.Style.Font.FontColor = XLColor.White;

        var f = 2;
        foreach (var x in reservas)
        {
            wr.Cell(f, 1).Value = x.Codigo;
            wr.Cell(f, 2).Value = x.Fecha.ToDateTime(TimeOnly.MinValue);
            wr.Cell(f, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            wr.Cell(f, 3).Value = x.Hora.ToString("HH:mm");
            wr.Cell(f, 4).Value = x.NombreCliente;
            wr.Cell(f, 5).Value = x.EmailCliente;
            wr.Cell(f, 6).Value = x.TelefonoCliente;
            wr.Cell(f, 7).Value = x.CantidadPersonas;
            wr.Cell(f, 8).Value = x.Mesa?.Numero.ToString() ?? "";
            wr.Cell(f, 9).Value = x.Estado.ToString();
            wr.Cell(f, 10).Value = x.Notas ?? "";
            f++;
        }
        wr.SheetView.FreezeRows(1);
        wr.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        _log.LogInformation("Reporte Excel generado: restaurante {Id}, {Desde}..{Hasta}", IdRestaurante, desde, hasta);
        return File(ms.ToArray(), MimeExcel, $"reservas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.xlsx");
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        var error = ValidarRango(desde, hasta);
        if (error != null) return BadRequest(new ApiError(error));

        var rest = await _db.Restaurantes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == IdRestaurante);
        var (reservas, resenas) = await CargarAsync(desde, hasta);
        var r = ReporteBuilder.Crear(desde, hasta, reservas, resenas);
        var nombre = rest?.Nombre ?? "Restaurante";

        var pdf = Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.A4.Landscape());
                p.Margin(30);
                p.DefaultTextStyle(t => t.FontSize(10));

                p.Header().Column(c =>
                {
                    c.Item().Text(nombre).FontSize(20).Bold().FontColor("#c8553d");
                    c.Item().Text($"Reporte de reservas · {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}").FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingBottom(8);
                });

                p.Content().Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(1); });
                        void Fila(string k, string v)
                        {
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(k);
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(v).Bold();
                        }
                        Fila("Total de reservas", r.TotalReservas.ToString());
                        Fila("Aprobadas / Rechazadas / Canceladas / Pendientes",
                            $"{r.Aprobadas} / {r.Rechazadas} / {r.Canceladas} / {r.Pendientes}");
                        Fila("Personas atendidas (aprobadas)", r.PersonasAprobadas.ToString());
                        Fila("Tasa de aprobación", $"{r.TasaAprobacion:0.#}%");
                        Fila("Calificación promedio", r.TotalResenas == 0 ? "—" : $"{r.PromedioCalificacion:0.0} ({r.TotalResenas} reseñas)");
                    });

                    c.Item().Text("Detalle de reservas").FontSize(13).Bold();

                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(70); cd.ConstantColumn(65); cd.ConstantColumn(40);
                            cd.RelativeColumn(3); cd.ConstantColumn(50); cd.ConstantColumn(40); cd.ConstantColumn(70);
                        });
                        t.Header(h =>
                        {
                            foreach (var x in new[] { "Código", "Fecha", "Hora", "Cliente", "Personas", "Mesa", "Estado" })
                                h.Cell().Background("#c8553d").Padding(4).Text(x).FontColor(Colors.White).Bold();
                        });
                        foreach (var x in reservas)
                        {
                            void Celda(string s) => t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(s);
                            Celda(x.Codigo);
                            Celda(x.Fecha.ToString("dd/MM/yyyy"));
                            Celda(x.Hora.ToString("HH:mm"));
                            Celda(x.NombreCliente);
                            Celda(x.CantidadPersonas.ToString());
                            Celda(x.Mesa?.Numero.ToString() ?? "-");
                            Celda(x.Estado.ToString());
                        }
                    });
                });

                p.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();

        _log.LogInformation("Reporte PDF generado: restaurante {Id}, {Desde}..{Hasta}", IdRestaurante, desde, hasta);
        return File(pdf, "application/pdf", $"reservas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf");
    }

    // ---------- helpers ----------

    private static string? ValidarRango(DateOnly desde, DateOnly hasta)
    {
        if (desde == default || hasta == default) return "Indica las fechas 'desde' y 'hasta'.";
        if (desde > hasta) return "La fecha 'desde' no puede ser posterior a 'hasta'.";
        if (hasta.DayNumber - desde.DayNumber > 366) return "El rango máximo es de 366 días.";
        return null;
    }

    private async Task<(List<Reserva>, List<Resena>)> CargarAsync(DateOnly desde, DateOnly hasta)
    {
        var reservas = await _db.Reservas.AsNoTracking()
            .Include(x => x.Mesa)
            .Where(x => x.IdRestaurante == IdRestaurante && x.Fecha >= desde && x.Fecha <= hasta)
            .OrderBy(x => x.Fecha).ThenBy(x => x.Hora)
            .ToListAsync();

        var resenas = await _db.Resenas.AsNoTracking()
            .Where(x => x.IdRestaurante == IdRestaurante
                        && x.Reserva!.Fecha >= desde && x.Reserva.Fecha <= hasta)
            .ToListAsync();

        return (reservas, resenas);
    }
}
