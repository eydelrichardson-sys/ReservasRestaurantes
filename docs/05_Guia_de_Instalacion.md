---
title: "Guía de instalación y despliegue"
---

# Requisitos

- Windows con **Visual Studio 2022** (carga de trabajo *ASP.NET y desarrollo web*) o el **SDK de .NET 8**.
- **SQL Server Express** (o LocalDB) y, opcionalmente, SQL Server Management Studio.

# Pasos

1. Extrae el zip en una carpeta **nueva** y abre `ReservasRestaurantes.sln`.
2. Clic derecho en la solución → **Restaurar paquetes NuGet**.
   > No uses "Update all" en NuGet: los paquetes 10.x requieren .NET 10 y romperían el proyecto. Mantén las versiones 8.0.x.
3. Revisa `src/ReservasRestaurantes.Api/appsettings.json` → `ConnectionStrings:Default`. Por defecto: `Server=localhost\SQLEXPRESS;Database=ReservasDB;Trusted_Connection=True;TrustServerCertificate=True;`
4. **Base de datos** — elige una:
   - *Automática:* no hagas nada; la API crea el esquema, aplica las actualizaciones y carga datos demo al iniciar.
   - *Manual:* ejecuta `database/ReservasDB_completa.sql` en SSMS. Si ya tenías la base de la versión anterior, ejecuta `database/04_actualizacion_v2.sql`.
5. Clic derecho en la solución → **Configurar proyectos de inicio** → *Varios proyectos*: `ReservasRestaurantes.Api` y `ReservasRestaurantes.Client` en **Iniciar**; `Shared` y `Tests` en **Ninguno**.
6. Pulsa **F5**.
   - API: <http://localhost:5100/swagger> · estado: <http://localhost:5100/api/health>
   - Cliente: <http://localhost:5200>

Por línea de comandos:

```bash
dotnet run --project src/ReservasRestaurantes.Api
dotnet run --project src/ReservasRestaurantes.Client
dotnet test
```

# Usuarios demo

| Restaurante | Correo | Contraseña |
|---|---|---|
| La Casona Dominicana | admin@lacasona.example | Admin123! |
| Marisquería El Malecón | admin@marisqueria.example | Admin123! |

Los datos demo incluyen ~30 días de historial para que los reportes tengan información.

# Configuración opcional

## Correo real (SMTP)
En `appsettings.json` → `Smtp`: `Host`, `Port`, `User`, `Password`, `From`. Ejemplo con Gmail: host `smtp.gmail.com`, puerto 587 y una *contraseña de aplicación*. Con `Host` vacío los correos solo se escriben en el log.

## Recordatorios
`Recordatorios:Activo` (true/false), `HorasAntes` (24) e `IntervaloMinutos` (10).

# Lista de verificación para producción

- [ ] Definir `Jwt__Key` como variable de entorno (32+ caracteres aleatorios). La API no inicia en producción con la clave de ejemplo.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (desactiva Swagger y datos demo vía `appsettings.Production.json`).
- [ ] Cadena de conexión con un usuario SQL de mínimos privilegios.
- [ ] `Cors:Origins` con el dominio real del cliente y HTTPS en ambos.
- [ ] `ApiBaseUrl` del cliente (`wwwroot/appsettings.json`) apuntando a la API pública.
- [ ] Configurar SMTP real.
- [ ] Respaldos periódicos de la base de datos.

# Solución de problemas

| Síntoma | Solución |
|---|---|
| "Class Library cannot be started directly" | Configura Api y Client como proyectos de inicio. |
| `/api/health` dice `sin conexion` | Revisa la cadena de conexión y que SQL Server esté activo. |
| El cliente no carga datos | Verifica que la API esté corriendo y `Cors:Origins` incluya `http://localhost:5200`. |
| "Invalid column name" | Ejecuta la API una vez (aplica las actualizaciones) o corre `04_actualizacion_v2.sql`. |
| Error 423 al iniciar sesión | Cuenta bloqueada 15 minutos por intentos fallidos. |
