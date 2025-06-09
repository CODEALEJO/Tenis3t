using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
    public class VentaViewModel
    {
        [Display(Name = "Cliente (opcional)")]
        [StringLength(100)]
        public string? Cliente { get; set; }

        public List<DetalleVentaViewModel> Detalles { get; set; } = new List<DetalleVentaViewModel>();
    }
}