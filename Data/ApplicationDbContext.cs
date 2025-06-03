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


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de TallaInventario
            modelBuilder.Entity<TallaInventario>()
                .HasOne(t => t.Inventario)
                .WithMany(i => i.Tallas)
                .HasForeignKey(t => t.InventarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de Prestamo con TallaInventario
            modelBuilder.Entity<Prestamo>()
                .HasOne(p => p.TallaInventario)
                .WithMany()
                .HasForeignKey(p => p.TallaInventarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Elimina la configuración de Prestamo con Inventario

            // Configuración de Inventario con Usuario
            modelBuilder.Entity<Inventario>()
                .HasOne(i => i.Usuario)
                .WithMany()
                .HasForeignKey(i => i.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de Prestamo con Usuario
            modelBuilder.Entity<Prestamo>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cambiar nombre de tabla para TallaInventario
            modelBuilder.Entity<TallaInventario>().ToTable("TallasInventario");
        }
    }
}
