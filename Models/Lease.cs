using System;

namespace EliteRentals.Models
{
    public class Lease
    {
        public int LeaseId { get; set; }
        public int PropertyId { get; set; }
        public int TenantId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string LeaseStatus { get; set; } = string.Empty;
    }
}
