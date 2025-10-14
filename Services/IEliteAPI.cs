using EliteRentals.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace EliteRentals.Services
{
    public interface IEliteApi
    {
        // Public
        Task<List<PropertyReadDto>> GetPropertiesAsync(CancellationToken ct = default);
        Task<PropertyReadDto?> GetPropertyAsync(int id, CancellationToken ct = default);

        // Rental Applications (already used)
        Task<int?> CreateRentalApplicationAsync(int propertyId, string applicantName, string email, string phone, IFormFile? document, CancellationToken ct = default);

        // Manager: Properties
        Task<int?> CreatePropertyAsync(PropertyUploadDto dto, IFormFile? image, CancellationToken ct = default);
        Task<bool>  UpdatePropertyAsync(int propertyId, PropertyUploadDto dto, IFormFile? image, CancellationToken ct = default);
        Task<bool>  DeletePropertyAsync(int propertyId, CancellationToken ct = default);
    }
}
