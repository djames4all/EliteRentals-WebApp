namespace EliteRentals.Models.DTOs
{
    public class User
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        public string? Role { get; set; }
    }

    public class Property
    {
        public int PropertyId { get; set; }
        public string Name { get; set; } = "";
    }

}
