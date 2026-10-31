using Microsoft.EntityFrameworkCore;
using ReservasRestaurantes.Api.Models;

namespace ReservasRestaurantes.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Restaurante> Restaurantes => Set<Restaurante>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Horario> Horarios => Set<Horario>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Resena> Resenas => Set<Resena>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Restaurante>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(600);
            e.Property(x => x.Direccion).HasMaxLength(200).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.ImagenUrl).HasMaxLength(400);
        });

        mb.Entity<Usuario>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Rol).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Restaurante).WithMany()
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Horario>(e =>
        {
            e.HasIndex(x => new { x.IdRestaurante, x.DiaSemana }).IsUnique();
            e.HasOne(x => x.Restaurante).WithMany(r => r.Horarios)
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Mesa>(e =>
        {
            e.HasIndex(x => new { x.IdRestaurante, x.Numero }).IsUnique();
            e.HasOne(x => x.Restaurante).WithMany(r => r.Mesas)
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(400);
            e.Property(x => x.Categoria).HasMaxLength(60).IsRequired();
            e.Property(x => x.Precio).HasPrecision(10, 2);
            e.HasOne(x => x.Restaurante).WithMany(r => r.MenuItems)
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Reserva>(e =>
        {
            e.Property(x => x.NombreCliente).HasMaxLength(120).IsRequired();
            e.Property(x => x.EmailCliente).HasMaxLength(150).IsRequired();
            e.Property(x => x.TelefonoCliente).HasMaxLength(30).IsRequired();
            e.Property(x => x.Notas).HasMaxLength(400);
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.Codigo).HasMaxLength(12).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => new { x.IdRestaurante, x.Fecha });
            e.HasOne(x => x.Restaurante).WithMany(r => r.Reservas)
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Mesa).WithMany()
                .HasForeignKey(x => x.IdMesa)
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Resena>(e =>
        {
            e.Property(x => x.Comentario).HasMaxLength(500);
            e.HasIndex(x => x.IdReserva).IsUnique(); // una calificación por reserva
            e.HasIndex(x => x.IdRestaurante);
            e.HasOne(x => x.Reserva).WithMany()
                .HasForeignKey(x => x.IdReserva)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Restaurante).WithMany()
                .HasForeignKey(x => x.IdRestaurante)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
