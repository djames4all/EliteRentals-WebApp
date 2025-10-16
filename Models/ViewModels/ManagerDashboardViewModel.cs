namespace EliteRentals.Models.ViewModels
{
    public class ManagerDashboardViewModel
    {
        // KPIs
        public int TotalProperties { get; set; }
        public int PropertiesAvailable { get; set; }
        public int PropertiesOccupied { get; set; }

        public int TotalLeases { get; set; }
        public int ActiveLeases { get; set; }

        public int OpenMaintenance { get; set; }      // Pending / In Progress
        public int PendingApplications { get; set; }

        // Optional: last 30 days payments summary (if available)
        public int PaymentsCount30d { get; set; }
        public decimal PaymentsTotal30d { get; set; }

        // Recents (top 5 each)
        public List<EliteRentals.Models.DTOs.PropertyReadDto> RecentProperties { get; set; } = new();
        public List<EliteRentals.Models.DTOs.RentalApplicationDto> RecentApplications { get; set; } = new();
        public List<EliteRentals.Models.Maintenance> RecentMaintenance { get; set; } = new();
    }
}
