using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
    public class MetodoPago
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } // Efectivo, Transferencia, Datafono, Crédito
        public bool RequiereReferencia { get; set; } // Nuevo campo
    }
}