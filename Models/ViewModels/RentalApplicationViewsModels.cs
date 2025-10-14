using System.ComponentModel.DataAnnotations;
using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{
    public class RentalApplicationCreateViewModel
    {
        [Required] public int PropertyId { get; set; }
        [Required, StringLength(120)] public string ApplicantName { get; set; } = "";
        [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
        [Required, StringLength(50)] public string Phone { get; set; } = "";
        [StringLength(1000)] public string? Notes { get; set; } // UI-only (not in API)
    }

    public class RentalApplicationPageViewModel
    {
        public PropertyReadDto Property { get; set; } = new();
        public RentalApplicationCreateViewModel Form { get; set; } = new();
        public string ApiBaseUrl { get; set; } = "";
    }
}
