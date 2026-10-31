USE ReservasDB;
GO

/* Reservas de hoy por restaurante */
SELECT r.Nombre AS Restaurante, rs.Hora, rs.NombreCliente, rs.CantidadPersonas,
       m.Numero AS Mesa, rs.Estado, rs.Codigo
FROM dbo.Reservas rs
JOIN dbo.Restaurantes r ON r.Id = rs.IdRestaurante
LEFT JOIN dbo.Mesas m   ON m.Id = rs.IdMesa
WHERE rs.Fecha = CAST(GETDATE() AS DATE)
ORDER BY r.Nombre, rs.Hora;

/* Resumen de reservas por estado (ultimos 30 dias) */
SELECT IdRestaurante, Estado, COUNT(*) AS Total
FROM dbo.Reservas
WHERE Fecha >= DATEADD(DAY, -30, CAST(GETDATE() AS DATE))
GROUP BY IdRestaurante, Estado
ORDER BY IdRestaurante, Estado;

/* Deteccion de solapamientos de mesa (no deberia devolver filas) */
SELECT a.Id AS ReservaA, b.Id AS ReservaB, a.IdMesa, a.Fecha, a.Hora AS HoraA, b.Hora AS HoraB
FROM dbo.Reservas a
JOIN dbo.Reservas b
  ON a.IdMesa = b.IdMesa AND a.Fecha = b.Fecha AND a.Id < b.Id
JOIN dbo.Restaurantes r ON r.Id = a.IdRestaurante
WHERE a.Estado IN ('Pendiente','Aprobada') AND b.Estado IN ('Pendiente','Aprobada')
  AND a.Hora < DATEADD(MINUTE, r.DuracionReservaMin, b.Hora)
  AND b.Hora < DATEADD(MINUTE, r.DuracionReservaMin, a.Hora);

/* Limpiar reservas viejas (rechazadas/canceladas de mas de 90 dias) */
-- DELETE FROM dbo.Reservas
-- WHERE Estado IN ('Rechazada','Cancelada') AND Fecha < DATEADD(DAY, -90, CAST(GETDATE() AS DATE));
