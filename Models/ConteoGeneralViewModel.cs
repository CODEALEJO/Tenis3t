using System.Collections.Generic;
using Tenis3t.Models;

namespace Tenis3t.Models
{
    public class ConteoGeneralViewModel
    {
        public List<Prestamo> Exhibiciones { get; set; }
        public List<Inventario> Inventario { get; set; }
        public int TotalExhibiciones { get; set; }
        public int TotalInventario { get; set; }
    }
}