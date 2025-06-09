using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Models
{
 
    public class DetalleVenta
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La cantidad es obligatoria")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El precio unitario es obligatorio")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Required(ErrorMessage = "La talla es obligatoria")]
        [Display(Name = "Talla")]
        public int TallaInventarioId { get; set; }

        [ForeignKey("TallaInventarioId")]
        public TallaInventario TallaInventario { get; set; }

        [Required]
        public int VentaId { get; set; }

        [ForeignKey("VentaId")]
        public Venta Venta { get; set; }

        // Propiedades calculadas
        [NotMapped]
        public decimal Subtotal => Cantidad * PrecioUnitario;

        [NotMapped]
        public decimal GananciaPorUnidad => PrecioUnitario - (TallaInventario?.Inventario?.Costo ?? 0);

        [NotMapped]
        public decimal GananciaTotal => GananciaPorUnidad * Cantidad;

        [NotMapped]
        public string PrecioUnitarioFormateado => PrecioUnitario.ToString("N0", new System.Globalization.CultureInfo("es-CO"));

        [NotMapped]
        public string SubtotalFormateado => Subtotal.ToString("N0", new System.Globalization.CultureInfo("es-CO"));
    }
}
