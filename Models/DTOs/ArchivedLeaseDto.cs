namespace EliteRentals.Models.DTOs
{
    public class ArchivedLeaseDto
    {
        public int LeaseId { get; set; }
        public int? PropertyId { get; set; }
        public PropertyDto? Property { get; set; }
        public int? TenantId { get; set; }
        public TenantDto? Tenant { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "";
        public byte[]? DocumentData { get; set; }
        public string? DocumentType { get; set; }
        public DateTime? ArchivedDate { get; set; }
    }

}
