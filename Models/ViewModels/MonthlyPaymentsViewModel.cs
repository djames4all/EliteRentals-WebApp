using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{
    public class MonthlyPaymentsViewModel
    {
        public string MonthName { get; set; } = "";
        public List<PaymentDto> Payments { get; set; } = new();
    }

}
