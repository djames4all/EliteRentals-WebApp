// Models/DTOs/RentalApplicationDto.cs
namespace EliteRentals.Models.DTOs
{
    public class RentalApplicationDto
    {
        public int ApplicationId { get; set; }
        public int PropertyId { get; set; }
        public string ApplicantName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
    }
}
