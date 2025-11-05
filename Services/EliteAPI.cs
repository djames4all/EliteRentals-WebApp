using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EliteRentals.Models.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EliteRentals.Services
{
    public class EliteApi : IEliteApi
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _cfg;
        private readonly IHttpContextAccessor _http;

        public EliteApi(IHttpClientFactory factory, IConfiguration cfg, IHttpContextAccessor http)
        {
            _factory = factory;
            _cfg = cfg;
            _http = http;
        }

        private HttpClient Client(bool withAuth = false)
        {
            var c = _factory.CreateClient("EliteRentalsAPI");
            if (withAuth)
            {
                var user = _http.HttpContext?.User;
                var token = user?.Claims.FirstOrDefault(x => x.Type == "JWT")?.Value
                            ?? _http.HttpContext?.Session.GetString("JWT");
                if (!string.IsNullOrWhiteSpace(token))
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            return c;
        }

        // -------- Public: Properties --------
        public async Task<List<PropertyReadDto>> GetPropertiesAsync(CancellationToken ct = default)
        {
            var c = Client();
            return await c.GetFromJsonAsync<List<PropertyReadDto>>("api/Property", ct) ?? new();
        }

        public async Task<PropertyReadDto?> GetPropertyAsync(int id, CancellationToken ct = default)
        {
            var c = Client();
            return await c.GetFromJsonAsync<PropertyReadDto>($"api/Property/{id}", ct);
        }

        // -------- Public: Rental Applications --------
        public async Task<int?> CreateRentalApplicationAsync(
            int propertyId, string applicantName, string email, string phone, IFormFile? document, CancellationToken ct = default)
        {
            var c = Client();
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(propertyId.ToString()), "PropertyId");
            form.Add(new StringContent(applicantName ?? ""), "ApplicantName");
            form.Add(new StringContent(email ?? ""), "Email");
            form.Add(new StringContent(phone ?? ""), "Phone");
            if (document != null && document.Length > 0)
            {
                var sc = new StreamContent(document.OpenReadStream());
                sc.Headers.ContentType = new MediaTypeHeaderValue(document.ContentType ?? "application/octet-stream");
                form.Add(sc, "document", document.FileName);
            }
            var resp = await c.PostAsync("api/RentalApplications", form, ct);
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                var result = await resp.Content.ReadFromJsonAsync<RentalApplicationCreatedProxy>(cancellationToken: ct);
                return result?.ApplicationId ?? 0;
            }
            catch { return 0; }
        }

        private class RentalApplicationCreatedProxy { public int ApplicationId { get; set; } }

        // -------- Manager: Properties (auth) --------
        public async Task<int?> CreatePropertyAsync(PropertyUploadDto dto, List<IFormFile>? images, CancellationToken ct = default)
        {
            var c = Client(withAuth: true);
            using var form = BuildPropertyForm(dto, images);
            var resp = await c.PostAsync("api/Property", form, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var created = await resp.Content.ReadFromJsonAsync<CreatedPropertyProxy>(cancellationToken: ct);
            return created?.PropertyId ?? 0;
        }

        public async Task<bool> UpdatePropertyAsync(int propertyId, PropertyUploadDto dto, List<IFormFile>? images, CancellationToken ct = default)
        {
            var c = Client(withAuth: true);
            using var form = BuildPropertyForm(dto, images);
            var resp = await c.PutAsync($"api/Property/{propertyId}", form, ct);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> DeletePropertyAsync(int propertyId, CancellationToken ct = default)
        {
            var c = Client(withAuth: true);
            var resp = await c.DeleteAsync($"api/Property/{propertyId}", ct);
            return resp.IsSuccessStatusCode;
        }

        private MultipartFormDataContent BuildPropertyForm(PropertyUploadDto dto, List<IFormFile>? images)
        {
            var form = new MultipartFormDataContent();

            // Add text fields
            form.Add(new StringContent(dto.Title ?? ""), nameof(dto.Title));
            form.Add(new StringContent(dto.Description ?? ""), nameof(dto.Description));
            form.Add(new StringContent(dto.Address ?? ""), nameof(dto.Address));
            form.Add(new StringContent(dto.City ?? ""), nameof(dto.City));
            form.Add(new StringContent(dto.Province ?? ""), nameof(dto.Province));
            form.Add(new StringContent(dto.Country ?? ""), nameof(dto.Country));
            form.Add(new StringContent(dto.RentAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(dto.RentAmount));
            form.Add(new StringContent(dto.NumOfBedrooms.ToString()), nameof(dto.NumOfBedrooms));
            form.Add(new StringContent(dto.NumOfBathrooms.ToString()), nameof(dto.NumOfBathrooms));
            form.Add(new StringContent(dto.ParkingType ?? ""), nameof(dto.ParkingType));
            form.Add(new StringContent(dto.NumOfParkingSpots.ToString()), nameof(dto.NumOfParkingSpots));
            form.Add(new StringContent(dto.PetFriendly.ToString()), nameof(dto.PetFriendly));
            form.Add(new StringContent(dto.Status ?? "Available"), nameof(dto.Status));

            // ✅ Add multiple files
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img.Length > 0)
                    {
                        var sc = new StreamContent(img.OpenReadStream());
                        sc.Headers.ContentType = new MediaTypeHeaderValue(img.ContentType ?? "application/octet-stream");
                        form.Add(sc, "images", img.FileName);
                    }
                }
            }

            return form;
        }

        private class CreatedPropertyProxy { public int PropertyId { get; set; } }

        // -------- Admin/Manager/Caretaker: Maintenance (auth, mapped) --------
        public async Task<List<MaintenanceDto>> GetMaintenanceAsync(CancellationToken ct = default)
        {
            var c = Client(withAuth: true); // needs Admin/PropertyManager/Caretaker
            var res = await c.GetAsync("api/Maintenance", ct);
            if (!res.IsSuccessStatusCode)
                return new List<MaintenanceDto>();

            var apiItems = await res.Content.ReadFromJsonAsync<List<MaintenanceApiRead>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct
            ) ?? new List<MaintenanceApiRead>();

            var list = new List<MaintenanceDto>(apiItems.Count);
            foreach (var m in apiItems)
            {
                list.Add(new MaintenanceDto
                {
                    MaintenanceId = m.MaintenanceId,
                    Issue = m.Description ?? "",         // API Description → DTO Issue
                    Priority = m.Urgency,                   // API Urgency     → DTO Priority
                    Status = m.Status,
                    PropertyName = m.Property?.Title,
                    PropertyId = m.PropertyId,
                    ReportedBy = m.Tenant == null
                                      ? null
                                      : $"{(m.Tenant.FirstName ?? "").Trim()} {(m.Tenant.LastName ?? "").Trim()}".Trim(),
                    TenantId = m.TenantId,
                    AssignedCaretakerId = m.AssignedCaretakerId,
                    AssignedCaretakerName = m.Caretaker == null
                                              ? null
                                              : $"{(m.Caretaker.FirstName ?? "").Trim()} {(m.Caretaker.LastName ?? "").Trim()}".Trim(),
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                    ProofData = m.ProofData,
                    ProofType = m.ProofType
                });
            }

            return list;
        }

        // Internal API read models that match your backend JSON (Property + User navigations)
        private sealed class MaintenanceApiRead
        {
            public int MaintenanceId { get; set; }
            public int TenantId { get; set; }
            public int PropertyId { get; set; }
            public int? AssignedCaretakerId { get; set; }

            public string? Description { get; set; }
            public string? Category { get; set; }
            public string? Urgency { get; set; }
            public string? Status { get; set; }

            public byte[]? ProofData { get; set; }
            public string? ProofType { get; set; }

            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }

            public PropertyApiRead? Property { get; set; }
            public UserApiRead? Tenant { get; set; }
            public UserApiRead? Caretaker { get; set; }
        }

        private sealed class PropertyApiRead
        {
            public int PropertyId { get; set; }
            public string? Title { get; set; }
        }

        private sealed class UserApiRead
        {
            public int UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
        }

        // -------- Admin/Manager: Payments (auth, mapped) --------
        public async Task<List<PaymentDto>> GetPaymentsAsync(CancellationToken ct = default)
        {
            var c = Client(withAuth: true); // needs Admin/PropertyManager
            var res = await c.GetAsync("api/Payment", ct);
            if (!res.IsSuccessStatusCode) return new List<PaymentDto>();

            var apiList = await res.Content.ReadFromJsonAsync<List<PaymentApiRead>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct
            ) ?? new List<PaymentApiRead>();

            var list = new List<PaymentDto>(apiList.Count);
            foreach (var p in apiList)
            {
                list.Add(new PaymentDto
                {
                    PaymentId = p.PaymentId,
                    TenantId = p.TenantId,
                    Amount = p.Amount,
                    Date = p.Date,
                    Status = p.Status
                });
            }
            return list;
        }

        private sealed class PaymentApiRead
        {
            public int PaymentId { get; set; }
            public int TenantId { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
            public string? Status { get; set; }
            public byte[]? ProofData { get; set; }
            public string? ProofType { get; set; }
        }

        // -------- Admin/Manager: Users (auth) --------
        public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default)
        {
            var c = Client(withAuth: true);
            var res = await c.GetAsync("api/Users", ct);
            if (!res.IsSuccessStatusCode) return new List<UserDto>();

            return await res.Content.ReadFromJsonAsync<List<UserDto>>(
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct
                   ) ?? new List<UserDto>();
        }
        public async Task<List<LeaseDto>> GetLeasesAsync(CancellationToken ct = default)
        {
            var c = Client(withAuth: true); // typically Admin/PropertyManager
            var res = await c.GetAsync("api/Lease", ct);
            if (!res.IsSuccessStatusCode) return new List<LeaseDto>();

            var apiList = await res.Content.ReadFromJsonAsync<List<LeaseApiRead>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct
            ) ?? new List<LeaseApiRead>();

            var list = new List<LeaseDto>(apiList.Count);
            foreach (var l in apiList)
            {
                list.Add(new LeaseDto
                {
                    LeaseId = l.LeaseId,
                    TenantId = l.TenantId,
                    PropertyId = l.PropertyId,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    Status = l.Status  
                });
            }
            return list;
        }

        // internal API read model (minimal, matches your API)
        private sealed class LeaseApiRead
        {
            public int LeaseId { get; set; }
            public int TenantId { get; set; }
            public int PropertyId { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? Status { get; set; }
        }

        public async Task<bool> BroadcastAsync(string audience, string message, CancellationToken ct = default)
{
    var c = Client(withAuth: true);

    // Get current sender (UserId) from cookie claims
    int senderId = 0;
    var claimsUser = _http.HttpContext?.User;
    var idClaim = claimsUser?.Claims.FirstOrDefault(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(idClaim, out senderId)) senderId = 0; // fallback

    // Map audience -> TargetRole (null = all users, per API code)
    string? targetRole = string.Equals(audience, "All", StringComparison.OrdinalIgnoreCase) ? null : audience;

    var payload = new
    {
        SenderId = senderId,       // FK to Users table
        MessageText = message,     // REQUIRED
        TargetRole = targetRole,   // null => all users
        IsBroadcast = true         // safe to set; API also sets it
    };

    // Call the dedicated broadcast endpoint
    var res = await c.PostAsJsonAsync("api/Message/broadcast", payload, ct);

    // Optional: read body for diagnostics if it fails
    // var body = await res.Content.ReadAsStringAsync(ct); // log if needed

    return res.IsSuccessStatusCode;
}


    }
}
