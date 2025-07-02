using System.ComponentModel.DataAnnotations;

public class DetalleVentaViewModel
{
    public int? Id { get; set; }
    
    [Required(ErrorMessage = "Debe seleccionar un producto")]
    public int ProductoId { get; set; }
    
    public string NombreProducto { get; set; }
    
    [Required(ErrorMessage = "Debe seleccionar una talla")]
    public int TallaInventarioId { get; set; }
    
    public string Talla { get; set; }
    
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal PrecioUnitario { get; set; }
    
    public int Disponible { get; set; }
}