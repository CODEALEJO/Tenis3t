using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Models
{
    public class TallaInventario
    {
        public int Id { get; set; }

        [Required]
        public int InventarioId { get; set; }
        public Inventario Inventario { get; set; }

        [Required]
        public string Talla { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad debe ser un número positivo")]
        public int Cantidad { get; set; }
    }
}