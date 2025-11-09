using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{
    public class MonthlyMaintenanceViewModel
    {
        public string MonthName { get; set; } = "";
        public List<MaintenanceDto> Maintenances { get; set; } = new();
    }

}
