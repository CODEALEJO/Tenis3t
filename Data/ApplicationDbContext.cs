using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Models;

namespace Tenis3t.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TallaInventario> TallasInventario { get; set; }
        public DbSet<Inventario> Inventarios { get; set; }
        public DbSet<Prestamo> Prestamos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVenta { get; set; }
        public DbSet<MetodoPago> MetodoPagos { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de TallaInventario
            modelBuilder.Entity<TallaInventario>()
                .HasOne(t => t.Inventario)
                .WithMany(i => i.Tallas)
                .HasForeignKey(t => t.InventarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Prestamo>(entity =>
         {
             entity.HasOne(p => p.TallaInventario)
                 .WithMany()
                 .HasForeignKey(p => p.TallaInventarioId)
                 .OnDelete(DeleteBehavior.Restrict);

             entity.HasOne(p => p.UsuarioPrestamista)
                 .WithMany()
                 .HasForeignKey(p => p.UsuarioPrestamistaId)
                 .OnDelete(DeleteBehavior.Restrict);

             entity.HasOne(p => p.UsuarioReceptor)
                 .WithMany()
                 .HasForeignKey(p => p.UsuarioReceptorId)
                 .OnDelete(DeleteBehavior.Restrict);
         });

            // Configuración de Inventario con Usuario
            modelBuilder.Entity<Inventario>()
                .HasOne(i => i.Usuario)
                .WithMany()
                .HasForeignKey(i => i.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de Venta
            modelBuilder.Entity<Venta>(entity =>
            {
                entity.HasOne(v => v.UsuarioVendedor)
                    .WithMany()
                    .HasForeignKey(v => v.UsuarioVendedorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuración de DetalleVenta
            modelBuilder.Entity<DetalleVenta>(entity =>
            {
                entity.HasOne(d => d.Venta)
                    .WithMany(v => v.Detalles)
                    .HasForeignKey(d => d.VentaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.TallaInventario)
                    .WithMany()
                    .HasForeignKey(d => d.TallaInventarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Cambiar nombre de tabla para TallaInventario
            modelBuilder.Entity<TallaInventario>().ToTable("TallasInventario");
            modelBuilder.Entity<DetalleVenta>().ToTable("DetallesVenta");
        }
    }
}