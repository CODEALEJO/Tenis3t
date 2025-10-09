using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tenis3t.Models
{
    public class Salida
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventarioId { get; set; }

        [ForeignKey("InventarioId")]
        public Inventario Inventario { get; set; }

        [Required]
        public int TallaInventarioId { get; set; }

        [ForeignKey("TallaInventarioId")]
        public TallaInventario TallaInventario { get; set; }

        [Required]
        public int Cantidad { get; set; }

        public string Nota { get; set; }

        public DateTime FechaSalida { get; set; } = DateTime.Now;
    }
}
