using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{
    public class PropertyListQuery
    {
        public string? Location { get; set; }
        public int? MinBedrooms { get; set; }
        public bool? PetFriendly { get; set; }
        public string SortBy { get; set; } = "newest"; // newest|lowest|highest
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
    }

    public class PropertyListViewModel
    {
        public List<PropertyReadDto> Items { get; set; } = new();
        public PropertyListQuery Query { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, Query.PageSize));
        public string ApiBaseUrl { get; set; } = "";
    }
}
