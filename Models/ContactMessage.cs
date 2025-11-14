using System;

namespace EliteRentals.Models
{
    public class ContactMessage
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }
}
