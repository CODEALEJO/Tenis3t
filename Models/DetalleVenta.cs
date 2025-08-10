using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tenis3t.Models
{
    public class DetalleVenta
    {
        public int Id { get; set; }

        [Required]
        public int VentaId { get; set; }
        public Venta Venta { get; set; }

        [Required]
        public int InventarioId { get; set; }  // Nuevo campo para el producto

        [Required]
        public int? TallaInventarioId { get; set; }
        public TallaInventario TallaInventario { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
        public int Cantidad { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        // Propiedad calculada
        [NotMapped]
        public decimal Subtotal => Cantidad * PrecioUnitario;

        // Propiedades de navegación para fácil acceso
        [NotMapped]
        public string NombreProducto => TallaInventario?.Inventario?.Nombre ?? "N/A";

        [NotMapped]
        public string Talla => TallaInventario?.Talla ?? "N/A";
    }
}