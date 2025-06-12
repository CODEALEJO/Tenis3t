using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
    public class MetodoPagoViewModel
    {
        [Required]
        public int MetodoPagoId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        public string? Referencia { get; set; }

        // Para mostrar en la vista
        public string? NombreMetodo { get; set; }
    }
}