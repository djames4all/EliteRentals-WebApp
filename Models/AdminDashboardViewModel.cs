namespace EliteRentals.Models
{
    public class AdminDashboardViewModel
    {
        public int OccupancyRate { get; set; }
        public decimal OverduePayments { get; set; }
        public int ActiveMaintenance { get; set; }
        public int PendingRequests { get; set; }

        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<RentTrendDto> RentTrends { get; set; } = new();
        public List<LeaseExpirationDto> LeaseExpirations { get; set; } = new();

        public int ExpiringLeases30Days { get; set; }
        public int ExpiringLeases60Days { get; set; }
        public int ExpiringLeases90Days { get; set; }
        public int LeasesMissingDocuments { get; set; }
        public int LeasesWithOverduePayments { get; set; }

        public List<MaintenanceAgingDto> MaintenanceAgingBuckets { get; set; } = new();
        public List<AlertDto> Alerts { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public string Tenant { get; set; } = "";
        public string Property { get; set; } = "";
        public string Action { get; set; } = "";
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
    }

    public class RentTrendDto
    {
        public string Month { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class LeaseExpirationDto
    {
        public string Month { get; set; } = "";
        public int Count { get; set; }
    }

    public class MaintenanceAgingDto
    {
        public int WeeksOpen { get; set; }
        public int Count { get; set; }
    }

    public class AlertDto
    {
        public string Type { get; set; } = "";      // e.g. "Lease Expiring"
        public string Message { get; set; } = "";   // e.g. "Ferndale apartment expires on 10 Oct"
        public string Severity { get; set; } = "";  // e.g. "warning", "danger"
    }


}
