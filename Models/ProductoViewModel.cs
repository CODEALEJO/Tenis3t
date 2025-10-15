using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Models
{
  public class ProductoViewModel
{
    public string Nombre { get; set; }
    public string Genero { get; set; }
    public decimal Costo { get; set; }
    public decimal PrecioVenta { get; set; }
    public List<TallaViewModel> Tallas { get; set; }
}

}