using System;
using System.Collections.Generic;

namespace Tenis3t.Models
{
    public class IngresoViewModel
    {
        public string LoteIngreso { get; set; }
        public DateTime FechaIngreso { get; set; }
        public int CantidadProductos { get; set; }

        // Lista de productos incluidos en el ingreso
        public List<ProductoViewModel> Productos { get; set; } = new();
    }
}
