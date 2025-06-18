using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Models
{
    [Table("Prestamos")] // Especifica explícitamente el nombre de la tabla
    public class Prestamo
    {
        public int Id { get; set; }
        public DateTime FechaPrestamo { get; set; } = DateTime.Now;
        public string Estado { get; set; } = "Prestado";

        [Required]
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }

        [Required]
        public int TallaInventarioId { get; set; }

        [ForeignKey("TallaInventarioId")]
        public virtual TallaInventario TallaInventario { get; set; }

        [Required]
        public string UsuarioPrestamistaId { get; set; }

        [ForeignKey("UsuarioPrestamistaId")]
        public virtual IdentityUser UsuarioPrestamista { get; set; }

        
        public string UsuarioReceptorId { get; set; }

        [ForeignKey("UsuarioReceptorId")]
        public virtual IdentityUser UsuarioReceptor { get; set; }

        // Campos no requeridos
        public string TipoPrestamo { get; set; } = "Persona";
        public string LocalPersona { get; set; } = "N/A";
    }


}