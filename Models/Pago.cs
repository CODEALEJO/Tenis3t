using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tenis3t.Models
{
    public class Pago
    {
        public int Id { get; set; }

        [Required]
        public int VentaId { get; set; }
        public Venta Venta { get; set; }

        [Required]
        public int MetodoPagoId { get; set; }
        public MetodoPago MetodoPago { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }
    }
}