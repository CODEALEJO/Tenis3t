using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Models
{
    public class Inventario
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del producto es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El género es obligatorio")]
        public string Genero { get; set; } // "hombre" o "dama"

        [NotMapped]
        public int Cantidad => Tallas?.Sum(t => t.Cantidad) ?? 0;

        [Required(ErrorMessage = "El costo es obligatorio")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser mayor a 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Costo { get; set; }

        [Required(ErrorMessage = "El precio de venta es obligatorio")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio de venta debe ser mayor a 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVenta { get; set; }

        // Relación con el usuario
        public string UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public IdentityUser Usuario { get; set; }
        public ICollection<Salida> Salidas { get; set; }

        public string LoteIngreso { get; set; } = Guid.NewGuid().ToString();
        public DateTime FechaIngreso { get; set; } = DateTime.Now;


        // Relación con las tallas
        public List<TallaInventario> Tallas { get; set; } = new List<TallaInventario>();

        [NotMapped]
        public decimal GananciaPorUnidad => PrecioVenta - Costo;

        [NotMapped]
        public decimal GananciaTotal => GananciaPorUnidad * Cantidad;

        [NotMapped]
        public string CostoFormateado => Costo.ToString("N0", new System.Globalization.CultureInfo("es-CO"));

        [NotMapped]
        public string PrecioVentaFormateado => PrecioVenta.ToString("N0", new System.Globalization.CultureInfo("es-CO"));

        [NotMapped]
        public string GananciaPorUnidadFormateado => GananciaPorUnidad.ToString("N0", new System.Globalization.CultureInfo("es-CO"));

        [NotMapped]
        public string GananciaTotalFormateado => GananciaTotal.ToString("N0", new System.Globalization.CultureInfo("es-CO"));
    }
}