using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models.DTOs
{
    public class CrearPrestamoDto
{
    [Required(ErrorMessage = "Debe seleccionar un usuario receptor")]
    public string UsuarioReceptorId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar una talla")]
    public int TallaInventarioId { get; set; }

    [Required(ErrorMessage = "La cantidad es requerida")]
    [Range(1, 999, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; } = 1;
}
}