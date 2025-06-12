using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tenis3t.Models
{
    public class VentaViewModel
    {
        // Datos del cliente
        [Display(Name = "Nombre del Cliente")]
        [StringLength(100)]
        public string? NombreCliente { get; set; }

        [Display(Name = "Cédula")]
        [StringLength(20)]
        public string? CedulaCliente { get; set; }

        [Display(Name = "Teléfono")]
        [StringLength(20)]
        public string? TelefonoCliente { get; set; }

        [Display(Name = "Email")]
        [EmailAddress]
        [StringLength(100)]
        public string? EmailCliente { get; set; }

        // Cliente (relación modificada)
        public int? ClienteId { get; set; }

        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }

        // Detalles de productos
        public List<DetalleVentaViewModel> Detalles { get; set; } = new List<DetalleVentaViewModel>();

        // Métodos de pago (se llenará en el segundo paso)
        public List<MetodoPagoViewModel> MetodosPago { get; set; } = new List<MetodoPagoViewModel>();
    }
}