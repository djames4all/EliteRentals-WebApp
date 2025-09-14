using System.ComponentModel.DataAnnotations;

namespace EliteRentals.Models
{
    public class RegisterViewModel
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
        [Required] public string Role { get; set; } = "Tenant";
    }
}
