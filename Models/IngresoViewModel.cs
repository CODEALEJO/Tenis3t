using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Models
{
   public class IngresoViewModel
{
    public string LoteIngreso { get; set; }
    public DateTime FechaIngreso { get; set; }
    public int CantidadProductos { get; set; }
    public List<ProductoViewModel> Productos { get; set; }
}

}