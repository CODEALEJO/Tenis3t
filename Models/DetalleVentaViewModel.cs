using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
    public class DetalleVentaViewModel
    {
        [Required]
        public int TallaInventarioId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }

        // Para mostrar información en la vista
        public string? NombreProducto { get; set; }
        public string? Talla { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Disponible { get; set; }
    }

}

