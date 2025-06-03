using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Models
{
public class Prestamo
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La fecha del préstamo es obligatoria")]
    public DateTime FechaPrestamo { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "La fecha de devolución estimada es obligatoria")]
    public DateTime FechaDevolucionEstimada { get; set; }

    public DateTime? FechaDevolucionReal { get; set; }

    [Required(ErrorMessage = "El estado del préstamo es obligatorio")]
    public string Estado { get; set; } = "Pendiente";

    [Required(ErrorMessage = "La cantidad es obligatoria")]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; }

    [Required(ErrorMessage = "La talla es obligatoria")]
    public int TallaInventarioId { get; set; }
    
    [ForeignKey("TallaInventarioId")]
    public TallaInventario TallaInventario { get; set; }

    [Required(ErrorMessage = "El local o persona es obligatorio")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres")]
    public string LocalPersona { get; set; }

    [Required]
    public string TipoPrestamo { get; set; }

    // Relación con el usuario
    public string UsuarioId { get; set; }
    
    [ForeignKey("UsuarioId")]
    public IdentityUser Usuario { get; set; }

    // Propiedades calculadas
    [NotMapped]
    public bool EstaVencido => Estado == "Pendiente" && DateTime.Now > FechaDevolucionEstimada;

    [NotMapped]
    public string DiasRestantes
    {
        get
        {
            if (Estado != "Pendiente") return "N/A";
            var dias = (FechaDevolucionEstimada - DateTime.Now).Days;
            return dias > 0 ? $"{dias} días" : "Vencido";
        }
    }
    
    // Propiedad de navegación calculada para acceder al Inventario
    [NotMapped]
    public Inventario Inventario => TallaInventario?.Inventario;
}
}