using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tenis3t.Models
{
  public class ProductoDisponibleViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public decimal PrecioVenta { get; set; }
    public List<TallaViewModel> Tallas { get; set; } = new List<TallaViewModel>();
}

}