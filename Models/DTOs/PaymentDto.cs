namespace EliteRentals.Models.DTOs
{
    public class PaymentDto
    {
        public int PaymentId { get; set; }
        public int TenantId { get; set; }
        public string? TenantName { get; set; } // Derived from tenant
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime Date { get; set; }
        public string? ProofType { get; set; }
        public byte[]? ProofData { get; set; }
    }
}
