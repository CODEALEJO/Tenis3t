using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Models
{
    public class Venta
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La fecha de venta es obligatoria")]
        [Display(Name = "Fecha de Venta")]
        public DateTime FechaVenta { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El estado de la venta es obligatorio")]
        [StringLength(20)]
        public string Estado { get; set; } = "Completada"; // Puede ser "Completada", "Cancelada", etc.

        [Required(ErrorMessage = "El total de la venta es obligatorio")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El total debe ser mayor a 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Usuario que realiza la venta
        [Required]
        [StringLength(450)]
        public string UsuarioVendedorId { get; set; }

        [ForeignKey("UsuarioVendedorId")]
        public IdentityUser UsuarioVendedor { get; set; }

        // Cliente (opcional, podría ser venta al público general)
        [StringLength(100)]
        public string? Cliente { get; set; }

        // Detalles de la venta
        public List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();

        // Propiedades calculadas
        [NotMapped]
        public string TotalFormateado => Total.ToString("N0", new System.Globalization.CultureInfo("es-CO"));

        [NotMapped]
        public int CantidadTotal => Detalles?.Sum(d => d.Cantidad) ?? 0;

        [NotMapped]
        public decimal GananciaTotal => Detalles?.Sum(d => d.GananciaTotal) ?? 0;
    }
}