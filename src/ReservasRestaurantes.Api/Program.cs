using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReservasRestaurantes.Api.Data;
using ReservasRestaurantes.Api.Hubs;
using ReservasRestaurantes.Api.Services;
using ReservasRestaurantes.Shared.Dtos;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Servicios ----------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(cfg.GetConnectionString("Default")));

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        // Errores de validación con el mismo formato ApiError que usa el resto de la API.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var mensajes = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Dato inválido." : e.ErrorMessage)
                .Distinct()
                .Take(3);
            return new BadRequestObjectResult(new ApiError(string.Join(" ", mensajes)));
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<SmtpSettings>(cfg.GetSection("Smtp"));
builder.Services.Configure<RecordatorioOptions>(cfg.GetSection("Recordatorios"));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DisponibilidadService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<RecordatorioService>();

// JWT
var jwtKey = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en appsettings.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
if (!builder.Environment.IsDevelopment() && jwtKey.StartsWith("CAMBIAR-ESTA-CLAVE"))
    throw new InvalidOperationException("En producción debes definir un Jwt:Key propio y secreto (variable de entorno Jwt__Key).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // conservamos los nombres cortos: sub, name, role, rid
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = cfg["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // SignalR (WebSockets) no puede enviar cabeceras: el token viaja en ?access_token=
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// CORS (el cliente Blazor corre en otro puerto)
var origenes = cfg.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("client", p => p
    .WithOrigins(origenes)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // necesario para SignalR

// Limitar abuso en endpoints públicos (por IP)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy("publico", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));

    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1) }));

    o.AddPolicy("registro", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));
});

// Licencia Community de QuestPDF (gratuita para proyectos académicos y pequeñas empresas)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// ---------- Base de datos ----------
// Si la base de datos no responde, la API NO se cierra: arranca igual y deja el error en la consola
// y en /api/health, para poder diagnosticarlo.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // si ya corriste database/01_schema.sql no hace nada
    DbPatcher.Aplicar(db);       // agrega columnas/tablas nuevas a bases creadas con versiones anteriores
    if (cfg.GetValue<bool>("Seed:Demo"))
    {
        DbSeeder.Seed(db);
        DbSeeder.SeedHistorial(db);
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex,
        "NO SE PUDO INICIAR LA BASE DE DATOS. Revisa ConnectionStrings:Default en appsettings.json y que SQL Server este activo.");
}

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Cabeceras de seguridad básicas
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors("client");

// Manejo global de errores. Va DESPUÉS de CORS para que incluso un error 500 lleve las cabeceras CORS
// (si no, el navegador lo muestra como un error de red confuso).
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}", ctx.Request.Method, ctx.Request.Path);
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new ApiError("Ocurrió un error inesperado. Inténtalo de nuevo más tarde."));
        }
    }
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Diagnostico rapido: abre http://localhost:5100/api/health
app.MapGet("/api/health", async (AppDbContext db, IWebHostEnvironment env) =>
{
    try
    {
        var ok = await db.Database.CanConnectAsync();
        return Results.Ok(new { api = "ok", baseDeDatos = ok ? "conectada" : "sin conexion" });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { api = "ok", baseDeDatos = "error", detalle = env.IsDevelopment() ? ex.Message : null });
    }
});

app.MapControllers();
app.MapHub<ReservasHub>("/hubs/reservas");

app.Run();

// Necesario para pruebas de integración con WebApplicationFactory
public partial class Program { }
