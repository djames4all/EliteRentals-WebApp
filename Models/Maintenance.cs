using EliteRentals.Models.DTOs;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace EliteRentals.Models
{
    public class Maintenance
    {
        public int MaintenanceId { get; set; }

        // Tenant Info
        public int TenantId { get; set; }
        public UserDto? Tenant { get; set; }

        // Property Info
        public int PropertyId { get; set; }
        public PropertyDto? Property { get; set; }

        // Assigned Caretaker
        public int? AssignedCaretakerId { get; set; }
        public CaretakerDto? AssignedCaretaker { get; set; }

        // Maintenance Details
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Urgency { get; set; } = "Low";
        public string Status { get; set; } = "Pending";

        // File proof
        public byte[]? ProofData { get; set; }
        public string? ProofType { get; set; }

        // Dates
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CaretakerDto
    {
        public int CaretakerId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

}
