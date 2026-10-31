# Sistema de Reservas para Restaurantes

Proyecto final — **Automatización de Procesos** (UCATEBA).
Los restaurantes publican su perfil y gestionan mesas, horarios y reservas; los clientes ven la disponibilidad en tiempo real y reservan en línea. El sistema automatiza la disponibilidad, la asignación de mesa, los correos (confirmación y recordatorio), las reseñas y los reportes.

**Stack:** .NET 8 · ASP.NET Core Web API · Blazor WebAssembly · SQL Server · EF Core · JWT · SignalR · MailKit · ClosedXML · QuestPDF · xUnit

## Funcionalidades

- Perfiles de restaurante: información, menú por categorías, horarios, capacidad de mesas, calificación promedio.
- Reserva en línea con horarios calculados según mesas libres; sin dobles reservas (transacción *Serializable*).
- Panel de administración en tiempo real (SignalR): aprobar/rechazar, ocupación de mesas.
- **Correos automáticos:** confirmación al aprobar y recordatorio 24 h antes (`BackgroundService`).
- **Reseñas** de clientes (1–5 estrellas) tras la visita.
- **Reportes** con gráficos y exportación a **Excel** y **PDF**.
- **Registro** de restaurantes nuevos.
- **Seguridad:** BCrypt, JWT, roles, bloqueo de cuenta, rate limiting, hub autenticado, cabeceras de seguridad, manejo global de errores.
- **Pruebas unitarias** de disponibilidad, contraseñas y reportes.

## Estructura

```
ReservasRestaurantes/
├── ReservasRestaurantes.sln
├── database/            Scripts SQL (esquema, datos demo, actualización v2, todo-en-uno)
├── docs/                Informe, base de datos, diagramas, manual, instalación (+ versión Word en docs/word)
├── tests/ReservasRestaurantes.Tests/   Pruebas xUnit
└── src/
    ├── ReservasRestaurantes.Shared/    DTOs y enums compartidos
    ├── ReservasRestaurantes.Api/       Controllers (públicos y Admin/), Data, Hubs, Models, Services
    └── ReservasRestaurantes.Client/    Blazor WASM: Components, Layout, Pages (y Admin/), Services, wwwroot
```

## Cómo correrlo

1. Extrae en una carpeta nueva, abre la `.sln` y restaura NuGet (**no uses "Update all"**; mantén las versiones 8.0.x).
2. Ajusta `ConnectionStrings:Default` en `src/ReservasRestaurantes.Api/appsettings.json` (por defecto `localhost\SQLEXPRESS`).
3. La base se crea y actualiza sola al iniciar la API (o ejecuta `database/ReservasDB_completa.sql`; para una base antigua, `database/04_actualizacion_v2.sql`).
4. Proyectos de inicio: **Api** y **Client**. F5.
   - API <http://localhost:5100/swagger> · salud <http://localhost:5100/api/health>
   - Cliente <http://localhost:5200>
5. Pruebas: `dotnet test`.

| Restaurante | Correo | Contraseña |
|---|---|---|
| La Casona Dominicana | admin@lacasona.example | Admin123! |
| Marisquería El Malecón | admin@marisqueria.example | Admin123! |

## API principal

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/restaurantes`, `/api/restaurantes/{id}`, `/{id}/resenas`, `/{id}/disponibilidad` | Público |
| POST | `/api/reservas` · GET `/api/reservas/{codigo}` · POST `/{codigo}/cancelar` · POST `/{codigo}/resena` | Público |
| POST | `/api/auth/login`, `/api/auth/registro` | Público (limitado) |
| GET | `/api/admin/reservas`, `/ocupacion` · POST `/{id}/aprobar`, `/{id}/rechazar` | Admin |
| GET/POST/PUT/DELETE | `/api/admin/mesas`, `/api/admin/menu`, `/api/admin/restaurante` | Admin |
| GET | `/api/admin/reportes/resumen`, `/excel`, `/pdf` | Admin |
| WS | `/hubs/reservas` | Admin (JWT) |

## Configuración

`Smtp` (correo real; vacío = solo log), `Recordatorios` (`Activo`, `HorasAntes`, `IntervaloMinutos`), `Seed:Demo`, `Jwt`, `Cors:Origins`.

## Lista para producción

`Jwt__Key` propio por variable de entorno · `ASPNETCORE_ENVIRONMENT=Production` · SMTP real · HTTPS y `Cors:Origins` reales · `ApiBaseUrl` del cliente · usuario SQL de mínimos privilegios.

Más detalle en la carpeta [`docs/`](docs).
