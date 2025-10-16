namespace EliteRentals.Models.DTOs
{
    public class ReportDto
    {
        public int ReportId { get; set; }
        public string? ReportName { get; set; }
        public string? ReportType { get; set; }
        public DateTime? DateGenerated { get; set; }
        public string? Status { get; set; }
    }



}
