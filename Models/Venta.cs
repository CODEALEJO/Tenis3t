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
        public string Estado { get; set; } // "Completada", "Cancelada"

        [StringLength(100)]
        public string NombreCliente { get; set; } // Campo simple para el cliente

        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100%")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Descuento { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Relaciones
        public string UsuarioVendedorId { get; set; }
        public IdentityUser UsuarioVendedor { get; set; }

        public List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
        public List<Pago> Pagos { get; set; } = new List<Pago>();

        // Propiedades calculadas
        [NotMapped]
        public decimal Subtotal => Detalles?.Sum(d => d.Subtotal) ?? 0;

        [NotMapped]
        public decimal TotalConDescuento => Subtotal * (1 - Descuento / 100m);

        [NotMapped]
        public decimal TotalPagado => Pagos?.Sum(p => p.Monto) ?? 0;

        [NotMapped]
        public decimal SaldoPendiente => TotalConDescuento - TotalPagado;
    }
}