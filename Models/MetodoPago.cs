using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
    public enum TipoMetodoPago
    {
        Efectivo,
        Transferencia,
        Tarjeta,
        Otro
    }

    public class MetodoPago
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public TipoMetodoPago Tipo { get; set; }
        
    }
}