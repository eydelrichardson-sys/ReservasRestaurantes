/* =====================================================================
   ReservasDB - Sistema de Reservas para Restaurantes
   SQL Server (Express / LocalDB / Developer)
   Ejecutar completo. Es idempotente: se puede correr varias veces.
   Nota: si no lo ejecutas, la API crea el esquema al iniciar (EnsureCreated).
   ===================================================================== */

IF DB_ID('ReservasDB') IS NULL
    CREATE DATABASE ReservasDB;
GO

USE ReservasDB;
GO

/* ---------- Restaurantes ---------- */
IF OBJECT_ID('dbo.Restaurantes', 'U') IS NULL
CREATE TABLE dbo.Restaurantes (
    Id                  INT IDENTITY(1,1) CONSTRAINT PK_Restaurantes PRIMARY KEY,
    Nombre              NVARCHAR(120)  NOT NULL,
    Descripcion         NVARCHAR(600)  NULL,
    Direccion           NVARCHAR(200)  NOT NULL,
    Telefono            NVARCHAR(30)   NULL,
    Email               NVARCHAR(150)  NULL,
    ImagenUrl           NVARCHAR(400)  NULL,
    DuracionReservaMin  INT            NOT NULL CONSTRAINT DF_Rest_Duracion DEFAULT 90,
    Activo              BIT            NOT NULL CONSTRAINT DF_Rest_Activo DEFAULT 1,
    CONSTRAINT CK_Rest_Duracion CHECK (DuracionReservaMin BETWEEN 30 AND 240)
);
GO

/* ---------- Usuarios (administradores de restaurante) ---------- */
IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
CREATE TABLE dbo.Usuarios (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Usuarios PRIMARY KEY,
    Email          NVARCHAR(150) NOT NULL,
    PasswordHash   NVARCHAR(200) NOT NULL,
    Nombre         NVARCHAR(120) NOT NULL,
    Rol            NVARCHAR(20)  NOT NULL CONSTRAINT DF_Usu_Rol DEFAULT 'Admin',
    IdRestaurante  INT NULL CONSTRAINT FK_Usu_Rest REFERENCES dbo.Restaurantes(Id),
    Activo         BIT NOT NULL CONSTRAINT DF_Usu_Activo DEFAULT 1,
    IntentosFallidos INT NOT NULL CONSTRAINT DF_Usu_Intentos DEFAULT 0,
    BloqueadoHasta DATETIME2(0) NULL,
    CONSTRAINT UQ_Usuarios_Email UNIQUE (Email)
);
GO

/* ---------- Horarios (uno por dia de la semana: 0=Domingo ... 6=Sabado) ---------- */
IF OBJECT_ID('dbo.Horarios', 'U') IS NULL
CREATE TABLE dbo.Horarios (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Horarios PRIMARY KEY,
    IdRestaurante  INT NOT NULL CONSTRAINT FK_Hor_Rest REFERENCES dbo.Restaurantes(Id) ON DELETE CASCADE,
    DiaSemana      INT NOT NULL CONSTRAINT CK_Hor_Dia CHECK (DiaSemana BETWEEN 0 AND 6),
    Apertura       TIME(0) NOT NULL,
    Cierre         TIME(0) NOT NULL,
    Cerrado        BIT NOT NULL CONSTRAINT DF_Hor_Cerrado DEFAULT 0,
    CONSTRAINT UQ_Horarios_Rest_Dia UNIQUE (IdRestaurante, DiaSemana)
);
GO

/* ---------- Mesas ---------- */
IF OBJECT_ID('dbo.Mesas', 'U') IS NULL
CREATE TABLE dbo.Mesas (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Mesas PRIMARY KEY,
    IdRestaurante  INT NOT NULL CONSTRAINT FK_Mesa_Rest REFERENCES dbo.Restaurantes(Id) ON DELETE CASCADE,
    Numero         INT NOT NULL,
    Capacidad      INT NOT NULL CONSTRAINT CK_Mesa_Cap CHECK (Capacidad BETWEEN 1 AND 50),
    Activa         BIT NOT NULL CONSTRAINT DF_Mesa_Activa DEFAULT 1,
    CONSTRAINT UQ_Mesas_Rest_Numero UNIQUE (IdRestaurante, Numero)
);
GO

/* ---------- Menu ---------- */
IF OBJECT_ID('dbo.MenuItems', 'U') IS NULL
CREATE TABLE dbo.MenuItems (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_MenuItems PRIMARY KEY,
    IdRestaurante  INT NOT NULL CONSTRAINT FK_Menu_Rest REFERENCES dbo.Restaurantes(Id) ON DELETE CASCADE,
    Nombre         NVARCHAR(120) NOT NULL,
    Descripcion    NVARCHAR(400) NULL,
    Precio         DECIMAL(10,2) NOT NULL CONSTRAINT CK_Menu_Precio CHECK (Precio >= 0),
    Categoria      NVARCHAR(60)  NOT NULL,
    Disponible     BIT NOT NULL CONSTRAINT DF_Menu_Disp DEFAULT 1
);
GO

/* ---------- Reservas ---------- */
IF OBJECT_ID('dbo.Reservas', 'U') IS NULL
CREATE TABLE dbo.Reservas (
    Id                INT IDENTITY(1,1) CONSTRAINT PK_Reservas PRIMARY KEY,
    IdRestaurante     INT NOT NULL CONSTRAINT FK_Res_Rest REFERENCES dbo.Restaurantes(Id),
    IdMesa            INT NULL     CONSTRAINT FK_Res_Mesa REFERENCES dbo.Mesas(Id),
    NombreCliente     NVARCHAR(120) NOT NULL,
    EmailCliente      NVARCHAR(150) NOT NULL,
    TelefonoCliente   NVARCHAR(30)  NOT NULL,
    Fecha             DATE    NOT NULL,
    Hora              TIME(0) NOT NULL,
    CantidadPersonas  INT NOT NULL CONSTRAINT CK_Res_Personas CHECK (CantidadPersonas BETWEEN 1 AND 20),
    Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_Res_Estado DEFAULT 'Pendiente',
    Notas             NVARCHAR(400) NULL,
    MotivoRechazo     NVARCHAR(300) NULL,
    Codigo            NVARCHAR(12) NOT NULL,
    FechaCreacion     DATETIME2(0) NOT NULL CONSTRAINT DF_Res_Creacion DEFAULT SYSDATETIME(),
    RecordatorioEnviado BIT NOT NULL CONSTRAINT DF_Res_Recordatorio DEFAULT 0,
    CONSTRAINT CK_Res_Estado CHECK (Estado IN ('Pendiente','Aprobada','Rechazada','Cancelada')),
    CONSTRAINT UQ_Reservas_Codigo UNIQUE (Codigo)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservas_Rest_Fecha' AND object_id = OBJECT_ID('dbo.Reservas'))
    CREATE INDEX IX_Reservas_Rest_Fecha ON dbo.Reservas (IdRestaurante, Fecha) INCLUDE (Hora, Estado, IdMesa);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservas_Mesa' AND object_id = OBJECT_ID('dbo.Reservas'))
    CREATE INDEX IX_Reservas_Mesa ON dbo.Reservas (IdMesa);
GO

/* ---------- Resenas (calificacion 1-5 que deja el cliente tras su visita) ---------- */
IF OBJECT_ID('dbo.Resenas', 'U') IS NULL
CREATE TABLE dbo.Resenas (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Resenas PRIMARY KEY,
    IdReserva      INT NOT NULL CONSTRAINT FK_Resena_Reserva REFERENCES dbo.Reservas(Id),
    IdRestaurante  INT NOT NULL CONSTRAINT FK_Resena_Rest REFERENCES dbo.Restaurantes(Id),
    Calificacion   INT NOT NULL CONSTRAINT CK_Resena_Cal CHECK (Calificacion BETWEEN 1 AND 5),
    Comentario     NVARCHAR(500) NULL,
    Fecha          DATETIME2(0) NOT NULL CONSTRAINT DF_Resena_Fecha DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Resenas_Reserva UNIQUE (IdReserva)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Resenas_IdRestaurante' AND object_id = OBJECT_ID('dbo.Resenas'))
    CREATE INDEX IX_Resenas_IdRestaurante ON dbo.Resenas (IdRestaurante);
GO

PRINT 'Esquema ReservasDB listo. Los datos demo los crea la API al iniciar (Seed:Demo = true).';
GO
GO

/* Datos demo. Requiere haber ejecutado 01_schema.sql. Solo inserta si no hay restaurantes.
   Usuarios: admin@lacasona.example / admin@marisqueria.example  -  Contrasena: Admin123! */
USE ReservasDB;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Restaurantes)
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.Restaurantes (Nombre, Descripcion, Direccion, Telefono, Email, DuracionReservaMin) VALUES
    (N'La Casona Dominicana', N'Cocina criolla tradicional en un ambiente familiar. Sancocho, mofongo y la mejor bandera dominicana de la zona.', N'Av. George Washington #123, Santo Domingo', N'809-555-0101', N'info@lacasona.example', 90),
    (N'Marisquería El Malecón', N'Pescados y mariscos frescos frente al mar. Ideal para celebraciones y almuerzos de domingo.', N'Calle Mella #45, Boca Chica', N'809-555-0202', N'reservas@marisqueria.example', 120);

    DECLARE @r1 INT = (SELECT Id FROM dbo.Restaurantes WHERE Nombre = N'La Casona Dominicana');
    DECLARE @r2 INT = (SELECT Id FROM dbo.Restaurantes WHERE Nombre = N'Marisquería El Malecón');

    -- Horarios: 0=Domingo ... 6=Sabado, 11:00-22:00 (la marisqueria cierra los lunes)
    INSERT dbo.Horarios (IdRestaurante, DiaSemana, Apertura, Cierre, Cerrado)
    SELECT r.Id, d.n, '11:00', '22:00', CASE WHEN r.Id = @r2 AND d.n = 1 THEN 1 ELSE 0 END
    FROM (VALUES (@r1), (@r2)) r(Id)
    CROSS JOIN (VALUES (0),(1),(2),(3),(4),(5),(6)) d(n);

    -- Mesas: 7 por restaurante (capacidades 2,2,4,4,4,6,8)
    INSERT dbo.Mesas (IdRestaurante, Numero, Capacidad)
    SELECT r.Id, m.Numero, m.Capacidad
    FROM (VALUES (@r1), (@r2)) r(Id)
    CROSS JOIN (VALUES (1,2),(2,2),(3,4),(4,4),(5,4),(6,6),(7,8)) m(Numero, Capacidad);

    INSERT dbo.MenuItems (IdRestaurante, Nombre, Descripcion, Precio, Categoria) VALUES
    (@r1, N'Yuca frita con queso', N'Con salsa de ajo', 250, N'Entradas'),
    (@r1, N'La Bandera', N'Arroz blanco, habichuelas rojas y pollo guisado', 380, N'Platos principales'),
    (@r1, N'Sancocho de siete carnes', N'Servido con arroz y aguacate', 450, N'Platos principales'),
    (@r1, N'Majarete', N'Postre de maíz con canela', 150, N'Postres'),
    (@r2, N'Ceviche de pescado', N'Pesca del día con limón y cilantro', 420, N'Entradas'),
    (@r2, N'Pescado frito', N'Con tostones y ensalada', 650, N'Platos principales'),
    (@r2, N'Langosta a la parrilla', N'Con mantequilla de ajo', 1600, N'Platos principales'),
    (@r2, N'Jugo natural', N'Chinola, limón o tamarindo', 120, N'Bebidas');

    INSERT dbo.Usuarios (Email, PasswordHash, Nombre, Rol, IdRestaurante) VALUES
    (N'admin@lacasona.example',    N'$2a$11$pCkxu7GTzCoVhtDoZVHgR.TpWKyDzC8A1MODFKwmi/Xpuv8KkKDmy', N'Admin La Casona',   N'Admin', @r1),
    (N'admin@marisqueria.example', N'$2a$11$pCkxu7GTzCoVhtDoZVHgR.TpWKyDzC8A1MODFKwmi/Xpuv8KkKDmy', N'Admin El Malecón', N'Admin', @r2);

    PRINT 'Datos demo insertados.';
END
ELSE PRINT 'Ya existen restaurantes; no se insertó nada.';
GO
