using System;
using System.Collections.Generic;
using System.Globalization;
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

        // ✅ Propiedades formateadas para mostrar correctamente los valores
        public string CostoFormateado => Costo.ToString("N0", new CultureInfo("es-CO"));
        public string PrecioVentaFormateado => PrecioVenta.ToString("N0", new CultureInfo("es-CO"));
    }

}