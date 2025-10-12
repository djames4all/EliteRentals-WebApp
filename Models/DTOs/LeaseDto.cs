namespace EliteRentals.Models.DTOs
{
    public class LeaseDto
    {
        public int LeaseId { get; set; }
        public int PropertyId { get; set; }
        public int TenantId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "Active";
        public byte[]? DocumentData { get; set; }
        public string? DocumentType { get; set; }

        public PropertyDto Property { get; set; }
        public TenantDto Tenant { get; set; }

        // convenience display helpers (for views)
        public string TenantName => $"{Tenant?.FirstName} {Tenant?.LastName}";
        public string PropertyTitle => Property?.Title ?? "Unknown Property";
    }

    public class LeaseCreateUpdateDto
    {
        public int PropertyId { get; set; }
        public int TenantId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? Deposit { get; set; } // nullable
        public string Status { get; set; }
    }

    public class PropertyDto
    {
        public int PropertyId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal RentAmount { get; set; }
    }

    public class TenantDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }
}
