using System.ComponentModel.DataAnnotations;

namespace Tenis3t.Models
{
public class LoginViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty; // Inicializa con valor por defecto

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty; // Inicializa con valor por defecto

    [Display(Name = "Recordar sesión")]
    public bool RememberMe { get; set; } = false; // Valor por defecto
}

    public class RegisterViewModel
    {
        [Required]
       
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; }
    }
}