/* =====================================================================
   ReservasDB - Actualizacion a la version 2
   Para bases de datos creadas con la version anterior (sin Resenas ni recordatorios).
   Es idempotente. NOTA: la API tambien aplica esto sola al iniciar (DbPatcher),
   por lo que este script es opcional; sirve para hacerlo a mano o para documentarlo.
   ===================================================================== */
USE ReservasDB;
GO

IF COL_LENGTH('dbo.Reservas','RecordatorioEnviado') IS NULL
    ALTER TABLE dbo.Reservas ADD RecordatorioEnviado BIT NOT NULL CONSTRAINT DF_Res_Recordatorio DEFAULT 0;
GO

IF COL_LENGTH('dbo.Usuarios','IntentosFallidos') IS NULL
    ALTER TABLE dbo.Usuarios ADD IntentosFallidos INT NOT NULL CONSTRAINT DF_Usu_Intentos DEFAULT 0;
GO

IF COL_LENGTH('dbo.Usuarios','BloqueadoHasta') IS NULL
    ALTER TABLE dbo.Usuarios ADD BloqueadoHasta DATETIME2(0) NULL;
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

PRINT 'Actualizacion v2 aplicada.';
