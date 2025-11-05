namespace EliteRentals.Models.DTOs
{
    public class PropertyUploadDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string Country { get; set; } = "";
        public decimal RentAmount { get; set; }
        public int NumOfBedrooms { get; set; }
        public int NumOfBathrooms { get; set; }
        public string ParkingType { get; set; } = "";
        public int NumOfParkingSpots { get; set; }
        public bool PetFriendly { get; set; }
        public string Status { get; set; } = "Available";

        public List<IFormFile>? Images { get; set; }
    }
}
