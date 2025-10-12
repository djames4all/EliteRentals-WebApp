namespace EliteRentals.Models.DTOs
{
    public class MaintenanceDto
    {
        public int MaintenanceId { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public string? PropertyName { get; set; }
        public int PropertyId { get; set; }
        public string? ReportedBy { get; set; }
        public int TenantId { get; set; }

        public int? AssignedCaretakerId { get; set; }
        public string? AssignedCaretakerName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public byte[]? ProofData { get; set; }
        public string? ProofType { get; set; }
    }
}
