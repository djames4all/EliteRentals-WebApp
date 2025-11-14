using EliteRentals.Models;
using System.Collections.Generic;

namespace EliteRentals.Services
{
    public static class InMemoryContactStore
    {
        public static List<ContactMessage> Messages { get; } = new List<ContactMessage>();
    }
}
