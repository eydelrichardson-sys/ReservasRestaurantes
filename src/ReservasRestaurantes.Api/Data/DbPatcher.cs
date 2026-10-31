using Microsoft.EntityFrameworkCore;

namespace ReservasRestaurantes.Api.Data;

/// <summary>
/// Actualiza una base de datos creada con una versión anterior (sin migraciones de EF).
/// Todas las sentencias son idempotentes: si la columna o tabla ya existe, no hacen nada.
/// Equivale a ejecutar database/04_actualizacion_v2.sql.
/// </summary>
public static class DbPatcher
{
    private static readonly string[] Sentencias =
    {
        "IF COL_LENGTH('dbo.Reservas','RecordatorioEnviado') IS NULL " +
        "ALTER TABLE dbo.Reservas ADD RecordatorioEnviado BIT NOT NULL CONSTRAINT DF_Res_Recordatorio DEFAULT 0",

        "IF COL_LENGTH('dbo.Usuarios','IntentosFallidos') IS NULL " +
        "ALTER TABLE dbo.Usuarios ADD IntentosFallidos INT NOT NULL CONSTRAINT DF_Usu_Intentos DEFAULT 0",

        "IF COL_LENGTH('dbo.Usuarios','BloqueadoHasta') IS NULL " +
        "ALTER TABLE dbo.Usuarios ADD BloqueadoHasta DATETIME2(0) NULL",

        @"IF OBJECT_ID('dbo.Resenas','U') IS NULL
          CREATE TABLE dbo.Resenas (
              Id             INT IDENTITY(1,1) CONSTRAINT PK_Resenas PRIMARY KEY,
              IdReserva      INT NOT NULL CONSTRAINT FK_Resena_Reserva REFERENCES dbo.Reservas(Id),
              IdRestaurante  INT NOT NULL CONSTRAINT FK_Resena_Rest REFERENCES dbo.Restaurantes(Id),
              Calificacion   INT NOT NULL CONSTRAINT CK_Resena_Cal CHECK (Calificacion BETWEEN 1 AND 5),
              Comentario     NVARCHAR(500) NULL,
              Fecha          DATETIME2(0) NOT NULL CONSTRAINT DF_Resena_Fecha DEFAULT SYSDATETIME(),
              CONSTRAINT UQ_Resenas_Reserva UNIQUE (IdReserva)
          )",

        "IF OBJECT_ID('dbo.Resenas','U') IS NOT NULL AND NOT EXISTS (" +
        "SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Resenas') AND name = 'IX_Resenas_IdRestaurante') " +
        "CREATE INDEX IX_Resenas_IdRestaurante ON dbo.Resenas (IdRestaurante)"
    };

    public static void Aplicar(AppDbContext db)
    {
        foreach (var sql in Sentencias)
            db.Database.ExecuteSqlRaw(sql);
    }
}
