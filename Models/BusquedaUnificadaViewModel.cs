using System;
using System.Collections.Generic;

namespace Tenis3t.Models
{
    public class BusquedaUnificadaViewModel
    {
        public string NombreProducto { get; set; }
        public List<ResultadoInventario> EnInventario { get; set; }
        public List<ResultadoExhibicion> EnExhibicion { get; set; }
    }

    public class ResultadoInventario
    {
        public string Usuario { get; set; }
        public int CantidadTotal { get; set; }
        public List<TallaCantidad> Tallas { get; set; }
        public decimal PrecioVenta { get; set; }
    }

    public class ResultadoExhibicion
    {
        public string Cliente { get; set; }
        public int Cantidad { get; set; }
        public string Talla { get; set; }
        public DateTime FechaPrestamo { get; set; }
    }

    public class TallaCantidad
    {
        public string Talla { get; set; }
        public int Cantidad { get; set; }
    }
}