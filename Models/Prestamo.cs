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

        [Required(ErrorMessage = "La fecha del préstamo es obligatoria")]
        [Display(Name = "Fecha de Préstamo")]
        public DateTime FechaPrestamo { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La fecha de devolución estimada es obligatoria")]
        [Display(Name = "Fecha Estimada de Devolución")]
        public DateTime FechaDevolucionEstimada { get; set; }

        [Display(Name = "Fecha Real de Devolución")]
        public DateTime? FechaDevolucionReal { get; set; }

        [Required(ErrorMessage = "El estado del préstamo es obligatorio")]
        [StringLength(20)]
        public string Estado { get; set; } = "Prestado";

        [Required(ErrorMessage = "La cantidad es obligatoria")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "La talla es obligatoria")]
        [Display(Name = "Talla")]
        public int TallaInventarioId { get; set; }

        [ForeignKey("TallaInventarioId")]
        public TallaInventario TallaInventario { get; set; }

        [Required(ErrorMessage = "El local o persona es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres")]
        [Display(Name = "Local/Persona")]
        public string LocalPersona { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Tipo de Préstamo")]
        public string TipoPrestamo { get; set; }

        // Usuario que realiza el préstamo
        [Required]
        [StringLength(450)]
        public string UsuarioPrestamistaId { get; set; }

        [ForeignKey("UsuarioPrestamistaId")]
        public IdentityUser UsuarioPrestamista { get; set; }

        // Usuario que recibe el préstamo
        [Required]
        [StringLength(450)]
        public string UsuarioReceptorId { get; set; }

        [ForeignKey("UsuarioReceptorId")]
        public IdentityUser UsuarioReceptor { get; set; }

        // Propiedades calculadas
        [NotMapped]
        [Display(Name = "¿Vencido?")]
        public bool EstaVencido => Estado == "Prestado" && DateTime.Now > FechaDevolucionEstimada;

        [NotMapped]
        [Display(Name = "Días Restantes")]
        public string DiasRestantes
        {
            get
            {
                if (Estado != "Prestado") return "N/A";
                var dias = (FechaDevolucionEstimada - DateTime.Now).Days;
                return dias > 0 ? $"{dias} días" : "Vencido";
            }
        }

        [NotMapped]
        public Inventario Inventario => TallaInventario?.Inventario;
    }
}