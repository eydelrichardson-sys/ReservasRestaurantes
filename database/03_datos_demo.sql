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
