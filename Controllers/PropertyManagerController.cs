using EliteRentals.Models;
using EliteRentals.Models.DTOs;
using EliteRentals.Models.ViewModels;
using EliteRentals.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Linq; // for LINQ
using System.Security.Claims;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;

namespace EliteRentals.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class PropertyManagerController : Controller
    {
        private readonly IEliteApi _api;
        private readonly IHttpClientFactory _clientFactory;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public PropertyManagerController(IEliteApi api, IHttpClientFactory clientFactory)
        {
            _api = api;
            _clientFactory = clientFactory;
        }

        // ===========================
        // PROPERTIES
        // ===========================

        // List ALL properties
        public async Task<IActionResult> ManagerProperties(CancellationToken ct)
        {
            var all = await _api.GetPropertiesAsync(ct);
            return View(all);
        }

        // CREATE
        [HttpGet]
        public IActionResult ManagerPropertyCreate()
        {
            return View(new PropertyUploadDto { Status = "Available" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyCreate(PropertyUploadDto dto, List<IFormFile>? images)
        {
            if (!ModelState.IsValid)
            {
                // Validation failed, stay on the form
                return View(dto);
            }

            try
            {
                // Call your API service to create the property
                var propertyId = await _api.CreatePropertyAsync(dto, images);

                if (propertyId.HasValue)
                {
                    // Creation succeeded → redirect to property list
                    TempData["SuccessMessage"] = "Property created successfully!";
                    return RedirectToAction("ManagerProperties");
                }
                else
                {
                    // Creation failed → show error on the same form
                    ModelState.AddModelError(string.Empty, "Could not create property. Try again.");
                    return View(dto);
                }
            }
            catch (Exception ex)
            {
                // Optional: log ex
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Try again.");
                return View(dto);
            }
        }



        // EDIT
        [HttpGet]
        public async Task<IActionResult> ManagerPropertyEdit(int id, CancellationToken ct)
        {
            var p = await _api.GetPropertyAsync(id, ct);
            if (p == null) return NotFound();

            var vm = new PropertyUploadDto
            {
                Title = p.Title ?? "",
                Description = p.Description ?? "",
                Address = p.Address ?? "",
                City = p.City ?? "",
                Province = p.Province ?? "",
                Country = p.Country ?? "",
                RentAmount = p.RentAmount,
                NumOfBedrooms = p.NumOfBedrooms,
                NumOfBathrooms = p.NumOfBathrooms,
                ParkingType = p.ParkingType ?? "",
                NumOfParkingSpots = p.NumOfParkingSpots,
                PetFriendly = p.PetFriendly,
                Status = (p.Status ?? "Available").Equals("Occupied", System.StringComparison.OrdinalIgnoreCase) ? "Occupied" : "Available"
            };
            ViewData["PropertyId"] = p.PropertyId;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyEdit(int id, PropertyUploadDto form, List<IFormFile>? images, CancellationToken ct)
        {
            NormalizeStatus(form);
            if (!ModelState.IsValid) { ViewData["PropertyId"] = id; return View(form); }

            var ok = await _api.UpdatePropertyAsync(id, form, images, ct);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Update failed.");
                ViewData["PropertyId"] = id;
                return View(form);
            }

            TempData["ManagerPropertyMsg"] = "Property updated.";
            return RedirectToAction(nameof(ManagerProperties));
        }

        // DELETE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyDelete(int id, CancellationToken ct)
        {
            var ok = await _api.DeletePropertyAsync(id, ct);
            TempData["ManagerPropertyMsg"] = ok ? "Property deleted." : "Delete failed.";
            return RedirectToAction(nameof(ManagerProperties));
        }

        // ===========================
        // APPLICATIONS
        // ===========================

        // DTO the site will bind to for app list/details
        public class RentalApplicationDto
        {
            public int ApplicationId { get; set; }
            public int PropertyId { get; set; }
            public string ApplicantName { get; set; } = "";
            public string Email { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Status { get; set; } = "Pending";
            public DateTime CreatedAt { get; set; }
        }

        // List Applications
        [HttpGet]
        public async Task<IActionResult> ManagerApplications()
        {
            var client = await CreateApiClient();
            try
            {
                var resp = await client.GetAsync("api/rentalapplications");
                if (!resp.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Failed to load applications.";
                    return View(Enumerable.Empty<RentalApplicationDto>());
                }

                var json = await resp.Content.ReadAsStringAsync();
                var apps = JsonSerializer.Deserialize<List<RentalApplicationDto>>(json, _jsonOptions) ?? new();
                apps = apps.OrderByDescending(a => a.ApplicationId).ToList();
                return View(apps);
            }
            catch (Exception)
            {
                ViewBag.Error = "Unexpected error loading applications.";
                return View(Enumerable.Empty<RentalApplicationDto>());
            }
        }
        [HttpGet]
        public async Task<IActionResult> ViewApplication(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/rentalapplications/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load application.";
                return RedirectToAction(nameof(ManagerApplications));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var app = System.Text.Json.JsonSerializer.Deserialize<EliteRentals.Models.DTOs.RentalApplicationDto>(json, _jsonOptions);
            return View(app);
        }

        // View one Application (optional details view)
        /*[HttpGet]
        public async Task<IActionResult> ViewApplication(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/rentalapplications/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load application.";
                return RedirectToAction(nameof(ManagerApplications));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var app = JsonSerializer.Deserialize<RentalApplicationDto>(json, _jsonOptions);
            return View(app);
        }
*/
        // Approve / Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApplicationStatus(int id, string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                TempData["Error"] = "Invalid status.";
                return RedirectToAction(nameof(ManagerApplications));
            }

            var client = await CreateApiClient();
            var payload = new { Status = status };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/rentalapplications/{id}/status", content);
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update application status.";
            }

            return RedirectToAction(nameof(ManagerApplications));
        }

        // ===========================
        // LEASES
        // ===========================

        [HttpGet]
        public async Task<IActionResult> ManagerLeases()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/lease");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load leases.";
                return View(new List<LeaseDto>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var leases = JsonSerializer.Deserialize<List<LeaseDto>>(json, _jsonOptions) ?? new List<LeaseDto>();
            return View(leases);
        }

        [HttpGet]
        public async Task<IActionResult> AddLease(int? propertyId)
        {
            ViewBag.Tenants = await FetchUnassignedTenants();
            ViewBag.Properties = await FetchAvailableProperties();

            return View(new LeaseCreateUpdateDto
            {
                PropertyId = propertyId ?? 0,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddMonths(12),
                Status = "Active"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLease(LeaseCreateUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Tenants = await FetchUnassignedTenants();
                ViewBag.Properties = await FetchAvailableProperties();
                return View(dto);
            }

            var client = await CreateApiClient();
            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("api/lease", content);
            var respBody = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"Lease POST returned: {resp.StatusCode}, body: {respBody}");

            if (resp.IsSuccessStatusCode)
            {
                // Show a toast after successful creation
                TempData["Toast"] = "Lease created successfully!";
                return RedirectToAction("ManagerLeases");
            }

            // Handle errors gracefully
            TempData["Error"] = $"Failed to create lease. ({resp.StatusCode}) {respBody}";
            ViewBag.Tenants = await FetchUnassignedTenants();
            ViewBag.Properties = await FetchAvailableProperties();
            return View(dto);
        }

        // GET: Edit Lease
        [HttpGet]
        public async Task<IActionResult> EditLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction(nameof(ManagerLeases));

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);
            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction(nameof(ManagerLeases));
            }

            ViewBag.Tenants = await FetchTenants();
            ViewBag.Properties = await FetchProperties();

            var dto = new LeaseCreateUpdateDto
            {
                LeaseId = lease.LeaseId,
                PropertyId = lease.PropertyId,
                TenantId = lease.TenantId,
                StartDate = lease.StartDate,
                EndDate = lease.EndDate,
                Deposit = lease.Deposit,
                Status = lease.Status
            };

            return View(dto);
        }

        // POST: Edit Lease
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLease(LeaseCreateUpdateDto dto)
        {
            var client = await CreateApiClient();

            var updatePayload = new
            {
                LeaseId = dto.LeaseId,
                PropertyId = dto.PropertyId,
                TenantId = dto.TenantId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Deposit = dto.Deposit,
                Status = dto.Status
            };

            var json = JsonSerializer.Serialize(updatePayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/lease/{dto.LeaseId}", content);

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Failed to update lease. ({resp.StatusCode})";
                ViewBag.Tenants = await FetchTenants();
                ViewBag.Properties = await FetchProperties();
                return View(dto);
            }

            return RedirectToAction(nameof(ManagerLeases));
        }

        [HttpGet]
        public async Task<IActionResult> DeleteLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction(nameof(ManagerLeases));

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);

            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction(nameof(ManagerLeases));
            }

            ViewBag.Tenants = await FetchTenants();
            ViewBag.Properties = await FetchProperties();

            return View(lease);
        }

        [HttpPost, ActionName("DeleteLease")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeaseConfirmed(int id)
        {
            var client = await CreateApiClient();
            await client.DeleteAsync($"api/lease/{id}");
            return RedirectToAction(nameof(ManagerLeases));
        }

        [HttpGet]
        public async Task<IActionResult> ViewLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction(nameof(ManagerLeases));

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);
            return View(lease);
        }

        // ARCHIVE Lease
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveLease(int id)
        {
            var client = await CreateApiClient();
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PutAsync($"api/lease/archive/{id}", null);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to archive lease.";
            else
                TempData["Success"] = "Lease archived successfully.";

            return RedirectToAction("ManagerLeases");
        }

        // RESTORE Lease
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreLease(int id)
        {
            var client = await CreateApiClient();
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PutAsync($"api/lease/restore/{id}", null);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to restore lease.";
            else
                TempData["Success"] = "Lease restored successfully.";

            return RedirectToAction("ArchivedLeases");
        }

        // ARCHIVED Lease list
        [HttpGet]
        public async Task<IActionResult> ArchivedLeases()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/lease/archived");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load archived leases.";
                return View(new List<LeaseDto>());
            }

            var json = await resp.Content.ReadAsStringAsync();

            var leases = JsonSerializer.Deserialize<List<LeaseDto>>(json,
                new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                    PropertyNameCaseInsensitive = true
                }) ?? new List<LeaseDto>();

            return View(leases);
        }



        // DELETE PERMANENT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeasePermanent(int id)
        {
            var client = await CreateApiClient();
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.DeleteAsync($"api/lease/{id}");

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to delete lease permanently.";
            else
                TempData["Success"] = "Lease permanently deleted.";

            return RedirectToAction("ArchivedLeases");
        }


        // ===========================
        // MAINTENANCE
        // ===========================

        [HttpGet]
        public async Task<IActionResult> ManagerMaintenance()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/maintenance");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load maintenance requests.";
                ViewBag.Caretakers = new List<Models.UserDto>();
                return View(new List<MonthlyMaintenanceViewModel>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var raw = JsonSerializer.Deserialize<List<Maintenance>>(json, _jsonOptions) ?? new List<Maintenance>();

            var maintenance = raw.Select(m => new MaintenanceDto
            {
                MaintenanceId = m.MaintenanceId,
                Issue = m.Description ?? "N/A",
                PropertyName = m.Property?.Title ?? $"Property #{m.PropertyId}",
                ReportedBy = m.Tenant != null ? $"{m.Tenant.FirstName} {m.Tenant.LastName}" : $"Tenant #{m.TenantId}",
                Status = m.Status ?? "Pending",
                Priority = m.Urgency ?? "Low",
                AssignedCaretakerId = m.AssignedCaretakerId,
                AssignedCaretakerName = m.AssignedCaretaker != null
                    ? $"{m.AssignedCaretaker.FirstName} {m.AssignedCaretaker.LastName}"
                    : null,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                ProofData = m.ProofData,
                ProofType = m.ProofType
            }).ToList();

            ViewBag.Caretakers = await FetchCaretakers();

            // Group by Month + Year based on CreatedAt
            var grouped = maintenance
                .GroupBy(m => new { m.CreatedAt.Year, m.CreatedAt.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthlyMaintenanceViewModel
                {
                    MonthName = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                    Maintenances = g.ToList()
                })
                .ToList();

            return View(grouped);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCaretaker(int maintenanceId, int caretakerId)
        {
            var client = await CreateApiClient();

            var dto = new { AssignedCaretakerId = caretakerId };
            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/maintenance/{maintenanceId}/assign-caretaker", content);
            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to assign caretaker.";

            return RedirectToAction(nameof(ManagerMaintenance));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceStatus(int maintenanceId, string status)
        {
            var client = await CreateApiClient();

            var dto = new { Status = status };
            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/maintenance/{maintenanceId}/status", content);
            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to update status.";

            return RedirectToAction(nameof(ManagerMaintenance));
        }

        [HttpGet]
        public async Task<IActionResult> ViewMaintenance(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/maintenance/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load maintenance details.";
                return RedirectToAction(nameof(ManagerMaintenance));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var m = JsonSerializer.Deserialize<Maintenance>(json, _jsonOptions);

            string? caretakerName = null;

            if (m?.AssignedCaretakerId != null && m.AssignedCaretaker == null)
            {
                var caretakerResp = await client.GetAsync($"api/users/{m.AssignedCaretakerId}");
                if (caretakerResp.IsSuccessStatusCode)
                {
                    var caretakerJson = await caretakerResp.Content.ReadAsStringAsync();
                    var caretaker = JsonSerializer.Deserialize<Models.DTOs.UserDto>(caretakerJson, _jsonOptions);
                    if (caretaker != null)
                    {
                        caretakerName = $"{caretaker.FirstName} {caretaker.LastName}";
                    }
                }
            }

            var dto = new MaintenanceDto
            {
                MaintenanceId = m!.MaintenanceId,
                Issue = m.Description ?? "N/A",
                PropertyName = m.Property?.Title ?? $"Property #{m.PropertyId}",
                ReportedBy = m.Tenant != null ? $"{m.Tenant.FirstName} {m.Tenant.LastName}" : $"Tenant #{m.TenantId}",
                Status = m.Status ?? "Pending",
                Priority = m.Urgency ?? "Low",
                AssignedCaretakerId = m.AssignedCaretakerId,
                AssignedCaretakerName = m.AssignedCaretaker != null
                    ? $"{m.AssignedCaretaker.FirstName} {m.AssignedCaretaker.LastName}"
                    : caretakerName,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                ProofData = m.ProofData,
                ProofType = m.ProofType
            };

            return View(dto);
        }

        // ===========================
        // PAYMENTS
        // ===========================

        
        [HttpGet]
        public async Task<IActionResult> ManagerPayments()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/payment");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load payments.";
                return View(new List<MonthlyPaymentsViewModel>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var payments = JsonSerializer.Deserialize<List<PaymentDto>>(json, _jsonOptions) ?? new List<PaymentDto>();

            // Fetch tenants for display names
            var tenants = await FetchTenants();
            foreach (var p in payments)
            {
                var tenant = tenants.FirstOrDefault(t => t.UserId == p.TenantId);
                if (tenant != null)
                    p.TenantName = $"{tenant.FirstName} {tenant.LastName}";
            }

            // Group by month and year
            var grouped = payments
                .GroupBy(p => new { p.Date.Year, p.Date.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthlyPaymentsViewModel
                {
                    MonthName = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                    Payments = g.ToList()
                })
                .ToList();

            return View(grouped);
        }

        [HttpGet]
        public async Task<IActionResult> ViewPayment(int id)
        {
            var client = await CreateApiClient();
            var response = await client.GetAsync($"api/payment/{id}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Unable to fetch payment details.";
                return RedirectToAction(nameof(ManagerPayments));
            }

            var json = await response.Content.ReadAsStringAsync();
            var payment = JsonSerializer.Deserialize<PaymentDto>(json, _jsonOptions);

            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentStatus(int id, string status)
        {
            var client = await CreateApiClient();

            var dto = new { Status = status };
            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/payment/{id}/status", content);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to update payment status.";

            return RedirectToAction(nameof(ManagerPayments));
        }

        // ===========================
        // SYSTEM USERS (Tenants)
        // ===========================

        [HttpGet]
        public async Task<IActionResult> ManagerTenants()
        {
            var token = HttpContext.Session.GetString("JWT");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login", "Account");

            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("api/users");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load tenants.";
                return View(new List<Models.DTOs.UserDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<Models.DTOs.UserDto>>(json, _jsonOptions) ?? new();

            var tenants = users.Where(u => u.Role == "Tenant").ToList();
            return View(tenants);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.PatchAsync($"api/users/{id}/status", null);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update tenant status.";
            }

            return RedirectToAction(nameof(ManagerTenants));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"api/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load tenant.";
                return RedirectToAction(nameof(ManagerTenants));
            }

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<Models.DTOs.UserDto>(json, _jsonOptions);

            if (user == null || user.Role != "Tenant")
                return RedirectToAction(nameof(ManagerTenants));

            return View("EditUser", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(Models.DTOs.UserDto user)
        {
            if (user.Role != "Tenant")
                return RedirectToAction(nameof(ManagerTenants));

            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"api/users/{user.UserId}", content);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update tenant.";
                return View("EditUser", user);
            }

            return RedirectToAction(nameof(ManagerTenants));
        }

        [HttpGet]
        public IActionResult AddUser()
        {
            var tenant = new Models.DTOs.UserDto { Role = "Tenant" };
            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(Models.DTOs.UserDto user)
        {
            user.Role = "Tenant"; // force role as Tenant

            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Generate a temporary password
            user.Password = $"{user.FirstName}@{Guid.NewGuid():N}".Substring(0, 12);

            var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/users/signup", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to create tenant.";
                return View(user);
            }

            ViewBag.Success = "Tenant created successfully!";
            ViewBag.GeneratedPassword = user.Password;

            return View(new Models.DTOs.UserDto { Role = "Tenant" });
        }

        // ===========================
        // NAV / SHELL PAGES
        // ===========================

        [HttpGet]
        public async Task<IActionResult> ManagerDashboard(CancellationToken ct)
        {
            var vm = new EliteRentals.Models.ViewModels.ManagerDashboardViewModel();

            // 1) Properties via IEliteApi
            var properties = await _api.GetPropertiesAsync(ct) ?? new List<PropertyReadDto>();
            vm.TotalProperties = properties.Count;
            vm.PropertiesAvailable = properties.Count(p => string.Equals(p.Status, "Available", System.StringComparison.OrdinalIgnoreCase));
            vm.PropertiesOccupied = properties.Count(p => string.Equals(p.Status, "Occupied", System.StringComparison.OrdinalIgnoreCase));
            vm.RecentProperties = properties.OrderByDescending(p => p.PropertyId).Take(5).ToList();

            // 2) Leases
            var client = await CreateApiClient();
            try
            {
                var leasesResp = await client.GetAsync("api/lease", ct);
                if (leasesResp.IsSuccessStatusCode)
                {
                    var leasesJson = await leasesResp.Content.ReadAsStringAsync(ct);
                    var leases = System.Text.Json.JsonSerializer.Deserialize<List<LeaseDto>>(leasesJson, _jsonOptions) ?? new();
                    vm.TotalLeases = leases.Count;
                    vm.ActiveLeases = leases.Count(l => string.Equals(l.Status, "Active", System.StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { /* ignore, leave defaults */ }

            // 3) Maintenance
            try
            {
                var mResp = await client.GetAsync("api/maintenance", ct);
                if (mResp.IsSuccessStatusCode)
                {
                    var mJson = await mResp.Content.ReadAsStringAsync(ct);
                    var maint = System.Text.Json.JsonSerializer.Deserialize<List<EliteRentals.Models.Maintenance>>(mJson, _jsonOptions) ?? new();

                    vm.OpenMaintenance = maint.Count(m =>
                        string.Equals(m.Status ?? "Pending", "Pending", System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.Status ?? "", "In Progress", System.StringComparison.OrdinalIgnoreCase));

                    vm.RecentMaintenance = maint
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(5)
                        .ToList();
                }
            }
            catch { /* ignore */ }

            // 4) Applications
            try
            {
                var aResp = await client.GetAsync("api/rentalapplications", ct);
                if (aResp.IsSuccessStatusCode)
                {
                    var aJson = await aResp.Content.ReadAsStringAsync(ct);
                    var apps = System.Text.Json.JsonSerializer.Deserialize<List<EliteRentals.Models.DTOs.RentalApplicationDto>>(aJson, _jsonOptions) ?? new();

                    vm.PendingApplications = apps.Count(a => string.Equals(a.Status, "Pending", System.StringComparison.OrdinalIgnoreCase));
                    vm.RecentApplications = apps.OrderByDescending(a => a.ApplicationId).Take(5).ToList();
                }
            }
            catch { /* ignore */ }

            // 5) Optional payments (last 30d)
            try
            {
                var pResp = await client.GetAsync("api/payment", ct);
                if (pResp.IsSuccessStatusCode)
                {
                    var pJson = await pResp.Content.ReadAsStringAsync(ct);
                    var pays = System.Text.Json.JsonSerializer.Deserialize<List<PaymentDto>>(pJson, _jsonOptions) ?? new();
                    var cutoff = DateTime.UtcNow.AddDays(-30);
                    var last30 = pays.Where(p => p.Date >= cutoff).ToList();

                    vm.PaymentsCount30d = last30.Count;
                    vm.PaymentsTotal30d = last30.Sum(p => p.Amount);
                }
            }
            catch { /* ignore */ }

            return View(vm);
        }

        //====================================================
        //MESSAGES
        //====================================================

        public async Task<IActionResult> ManagerMessages()
        {
            try
            {
                var propertymanagerId = GetCurrentUserId();
                var client = await CreateApiClient();

                var model = new AdminMessagesViewModel
                {
                    CurrentUserId = propertymanagerId,
                    CurrentUserName = HttpContext.Session.GetString("UserName") ?? "PropertyManager"
                };

                // Load users
                var usersResponse = await client.GetAsync("api/users");
                if (usersResponse.IsSuccessStatusCode)
                {
                    var usersJson = await usersResponse.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<Models.UserDto>>(usersJson, _jsonOptions) ?? new();
                    model.Users = users.Where(u => u.UserId != propertymanagerId).ToList();
                }

                // Load Inbox (only non-archived)
                var inboxResponse = await client.GetAsync($"api/Message/inbox/{propertymanagerId}");
                var inbox = new List<MessageDto>();
                if (inboxResponse.IsSuccessStatusCode)
                {
                    var inboxJson = await inboxResponse.Content.ReadAsStringAsync();
                    inbox = JsonSerializer.Deserialize<List<MessageDto>>(inboxJson, _jsonOptions) ?? new();
                    inbox = inbox.Where(m => !m.ArchivedDate.HasValue).ToList(); // ✅ filter out archived
                }

                // Load Sent (only non-archived)
                var sentResponse = await client.GetAsync($"api/Message/sent/{propertymanagerId}");
                var sent = new List<MessageDto>();
                if (sentResponse.IsSuccessStatusCode)
                {
                    var sentJson = await sentResponse.Content.ReadAsStringAsync();
                    sent = JsonSerializer.Deserialize<List<MessageDto>>(sentJson, _jsonOptions) ?? new();
                    sent = sent.Where(m => !m.ArchivedDate.HasValue).ToList(); // ✅ filter out archived
                }


                model.InboxMessages = inbox;
                model.SentMessages = sent;

                // Convert timestamps to local time
                foreach (var msg in model.InboxMessages.Concat(model.SentMessages))
                {
                    msg.Timestamp = msg.Timestamp.ToLocalTime();
                }

                // Detect unread messages for notification light
                model.HasUnreadMessages = model.InboxMessages.Any(m => !m.IsRead);

                // Build conversations (excluding archived messages)
                model.Conversations = await BuildConversations(inbox.Concat(sent).ToList(), propertymanagerId, client);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading messages: {ex.Message}";
                return View(new AdminMessagesViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(Models.ViewModels.SendMessageRequest request)
        {
            try
            {
                var propertymanagerId = GetCurrentUserId();
                var client = await CreateApiClient();

                var message = new
                {
                    SenderId = propertymanagerId,
                    ReceiverId = request.ReceiverId,
                    MessageText = request.MessageText,
                    Timestamp = DateTime.UtcNow,
                    IsChatbot = false
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/Message", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Message sent successfully!";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to send message: {error}";
                }

                return RedirectToAction("ManagerMessages");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending message: {ex.Message}";
                return RedirectToAction("ManagerMessages");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendBroadcast(string MessageText, string TargetRole)
        {
            try
            {
                var propertymanagerId = GetCurrentUserId();
                var client = await CreateApiClient();

                var message = new
                {
                    SenderId = propertymanagerId,
                    ReceiverId = (int?)null,
                    MessageText = MessageText,
                    Timestamp = DateTime.UtcNow,
                    IsChatbot = false,
                    IsBroadcast = true,
                    TargetRole = string.IsNullOrWhiteSpace(TargetRole) ? null : TargetRole
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/Message/broadcast", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Broadcast sent successfully!";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to send broadcast: {error}";
                }

                return RedirectToAction("ManagerMessages");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending broadcast: {ex.Message}";
                return RedirectToAction("ManagerMessages");
            }
        }



        // GET: View conversation with a specific user
        public async Task<IActionResult> ViewConversation(int userId)
        {
            try
            {
                var propertymanagerId = GetCurrentUserId();
                var client = await CreateApiClient();

                // Get the conversation
                var conversationResponse = await client.GetAsync($"api/Message/conversation/{propertymanagerId}/{userId}");
                if (!conversationResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Failed to load conversation.";
                    return RedirectToAction("ManagerMessages");
                }

                var conversationJson = await conversationResponse.Content.ReadAsStringAsync();
                var messages = JsonSerializer.Deserialize<List<MessageDto>>(conversationJson, _jsonOptions) ?? new List<MessageDto>();

                // Get user info for the other user
                var otherUserName = await GetUserName(userId, client);

                var model = new Models.ViewModels.ConversationDetailViewModel
                {
                    OtherUserId = userId,
                    OtherUserName = otherUserName,
                    Messages = messages,
                    CurrentUserId = propertymanagerId
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading conversation: {ex.Message}";
                return RedirectToAction("ManagerMessages");
            }
        }

        private async Task<List<Models.ViewModels.ConversationDto>> BuildConversations(List<MessageDto> messages, int currentUserId, HttpClient client)
        {
            var conversations = new Dictionary<int, Models.ViewModels.ConversationDto>();

            foreach (var message in messages)
            {
                // ✅ Skip broadcasts (no ReceiverId or IsBroadcast flag)
                if (!message.ReceiverId.HasValue || message.IsBroadcast)
                    continue;

                var otherUserId = message.SenderId == currentUserId
                    ? message.ReceiverId.Value
                    : message.SenderId;

                var otherUserName = await GetUserName(otherUserId, client);

                if (!conversations.ContainsKey(otherUserId))
                {
                    conversations[otherUserId] = new Models.ViewModels.ConversationDto
                    {
                        OtherUserId = otherUserId,
                        OtherUserName = otherUserName,
                        LastMessage = message.MessageText,
                        LastMessageTimestamp = message.Timestamp,
                        IsChatbot = message.IsChatbot,
                        UnreadCount = 0
                    };
                }
                else
                {
                    var existing = conversations[otherUserId];
                    if (message.Timestamp > existing.LastMessageTimestamp)
                    {
                        existing.LastMessage = message.MessageText;
                        existing.LastMessageTimestamp = message.Timestamp;
                        existing.IsChatbot = message.IsChatbot;
                    }
                }
            }

            return conversations.Values
                .OrderByDescending(c => c.LastMessageTimestamp)
                .ToList();
        }

        [HttpGet]
        public async Task<IActionResult> CheckNewMessages()
        {
            var propertymanagerId = GetCurrentUserId();
            var client = await CreateApiClient();
            var response = await client.GetAsync($"api/Message/inbox/{propertymanagerId}");

            if (!response.IsSuccessStatusCode)
                return Json(new { hasNewMessages = false });

            var json = await response.Content.ReadAsStringAsync();
            var inbox = JsonSerializer.Deserialize<List<MessageDto>>(json, _jsonOptions) ?? new();

            bool hasNew = inbox.Any(m => !m.IsRead);
            return Json(new { hasNewMessages = hasNew });
        }

        // ARCHIVE
        [HttpPost]
        public async Task<IActionResult> ArchiveMessage(int id)
        {
            try
            {
                var client = await CreateApiClient();
                // Send an empty body with proper content type
                var resp = await client.PutAsync($"api/message/archive/{id}", new StringContent(""));

                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to archive message: {errorMsg}";
                }
                else
                {
                    TempData["Success"] = "Message archived successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Exception: {ex.Message}";
            }

            return RedirectToAction("ArchivedMessages");
        }

        // RESTORE
        [HttpPost]
        public async Task<IActionResult> RestoreMessage(int id)
        {
            try
            {
                var client = await CreateApiClient();
                var resp = await client.PutAsync($"api/message/restore/{id}", new StringContent(""));

                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to restore message: {errorMsg}";
                }
                else
                {
                    TempData["Success"] = "Message restored successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Exception: {ex.Message}";
            }

            return RedirectToAction("ArchivedMessages");
        }




        [HttpGet]
        public async Task<IActionResult> ArchivedMessages()
        {
            var propertymanagerId = GetCurrentUserId();
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/message/archived/{propertymanagerId}");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load archived messages.";
                return View(new List<MessageDto>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonSerializer.Deserialize<List<MessageDto>>(json, _jsonOptions) ?? new List<MessageDto>();
            return View(messages);
        }


        // DELETE (Permanent)
        [HttpPost]
        public async Task<IActionResult> DeleteMessagePermanent(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.DeleteAsync($"api/message/{id}");

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to permanently delete message.";
            else
                TempData["Success"] = "Message permanently deleted.";

            return RedirectToAction("ArchivedMessages");
        }


        // Helper method to get user name by ID
        private async Task<string> GetUserName(int userId, HttpClient client)
        {
            try
            {
                var response = await client.GetAsync($"api/users/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var userJson = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<Models.UserDto>(userJson, _jsonOptions);
                    if (user != null)
                    {
                        return $"{user.FirstName} {user.LastName}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user name for {userId}: {ex.Message}");
            }

            return $"User {userId}";
        }

        [HttpGet] public IActionResult ManagerEscalations() => View();
        [HttpGet] public IActionResult ManagerSettings() => View();

        // ===========================
        // Helpers
        // ===========================
        private void NormalizeStatus(PropertyUploadDto form)
        {
            // restrict to Available or Occupied only
            form.Status = string.Equals(form.Status, "Occupied", System.StringComparison.OrdinalIgnoreCase)
                ? "Occupied"
                : "Available";
        }

        private async Task<HttpClient> CreateApiClient()
        {
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await Task.FromResult(client);
        }

        private async Task<List<TenantDto>> FetchTenants()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/users");
            if (!resp.IsSuccessStatusCode) return new List<TenantDto>();

            var json = await resp.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<TenantDto>>(json, _jsonOptions) ?? new();
            return users.Where(u => string.IsNullOrEmpty(u.Role) || u.Role == "Tenant").ToList();
        }

        private async Task<List<PropertyDto>> FetchProperties()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/property");
            if (!resp.IsSuccessStatusCode) return new List<PropertyDto>();

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PropertyDto>>(json, _jsonOptions) ?? new();
        }

        private async Task<List<Models.DTOs.UserDto>> FetchCaretakers()
        {
            try
            {
                var client = await CreateApiClient();
                var response = await client.GetAsync("api/users");
                if (!response.IsSuccessStatusCode) return new List<Models.DTOs.UserDto>();

                var json = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<Models.DTOs.UserDto>>(json, _jsonOptions) ?? new();

                return users
                    .Where(u => !string.IsNullOrEmpty(u.Role) &&
                                u.Role.Equals("Caretaker", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                return new List<Models.DTOs.UserDto>();
            }
        }

        private int GetCurrentUserId()
        {
            try
            {
                // Method 1: Get from User Claims (most reliable)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userIdFromClaim))
                {
                    return userIdFromClaim;
                }

                // Method 2: Get from Session (fallback)
                var userIdFromSession = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdFromSession) && int.TryParse(userIdFromSession, out int sessionUserId))
                {
                    return sessionUserId;
                }

                // Method 3: Get from JWT token (if stored)
                var token = HttpContext.Session.GetString("JWT");
                if (!string.IsNullOrEmpty(token))
                {
                    var userIdFromToken = ExtractUserIdFromToken(token);
                    if (userIdFromToken.HasValue)
                    {
                        // Store in session for future use
                        HttpContext.Session.SetString("UserId", userIdFromToken.Value.ToString());
                        return userIdFromToken.Value;
                    }
                }

                throw new Exception("Unable to determine current user ID");
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error getting current user ID: {ex.Message}");
                throw;
            }
        }

        private int? ExtractUserIdFromToken(string token)
        {
            try
            {
                // Simple JWT parsing - you might want to use a proper JWT library
                var parts = token.Split('.');
                if (parts.Length != 3) return null;

                var payload = parts[1];
                // Add padding if needed
                while (payload.Length % 4 != 0)
                    payload += '=';

                var payloadBytes = Convert.FromBase64String(payload);
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);

                using var document = JsonDocument.Parse(payloadJson);
                if (document.RootElement.TryGetProperty("nameid", out var nameIdElement) &&
                    nameIdElement.ValueKind == JsonValueKind.String &&
                    int.TryParse(nameIdElement.GetString(), out int userId))
                {
                    return userId;
                }

                // Try alternative claim names
                if (document.RootElement.TryGetProperty("sub", out var subElement) &&
                    subElement.ValueKind == JsonValueKind.String &&
                    int.TryParse(subElement.GetString(), out int subUserId))
                {
                    return subUserId;
                }

                if (document.RootElement.TryGetProperty("userId", out var userIdElement) &&
                    userIdElement.ValueKind == JsonValueKind.Number)
                {
                    return userIdElement.GetInt32();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<TenantDto>> FetchUnassignedTenants()
        {
            var allTenants = await FetchTenants(); // original method
            var client = await CreateApiClient();

            var leaseResp = await client.GetAsync("api/lease");
            if (!leaseResp.IsSuccessStatusCode) return allTenants;

            var leaseJson = await leaseResp.Content.ReadAsStringAsync();
            var leases = JsonSerializer.Deserialize<List<LeaseDto>>(leaseJson, _jsonOptions) ?? new List<LeaseDto>();

            var tenantIdsWithLease = leases
                .Where(l => l.Status == "Active" || l.Status == "Pending Approval")
                .Select(l => l.TenantId)
                .ToHashSet();

            return allTenants.Where(t => !tenantIdsWithLease.Contains(t.UserId)).ToList();
        }

        private async Task<List<PropertyDto>> FetchAvailableProperties()
        {
            var allProperties = await FetchProperties(); // original method
            return allProperties.Where(p => p.Status == "Available").ToList();
        }

        // ===========================
    // MANAGER CHATBOT (PM side)
    // ===========================

    public class PMChatAskRequest
    {
        public string Text { get; set; } = "";
    }

    public class PMChatAskResponse
    {
        public string Reply { get; set; } = "";
        public string Intent { get; set; } = "";
        public bool IsHtml { get; set; } = false;
        public string? Html { get; set; }
    }

    public class PMBroadcastRequest
    {
        public string Audience { get; set; } = "All"; // All, Tenant, Caretaker, PropertyManager
        public string Text { get; set; } = "";
    }

        [HttpGet]
        public IActionResult ManagerChatbot() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerChatbotBroadcast([FromBody] PMBroadcastRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Message is required." });

            // Send via API broadcast you already use on PM side
            var client = await CreateApiClient();
            var payload = new
            {
                SenderId = GetCurrentUserId(),
                ReceiverId = (int?)null,
                MessageText = req.Text.Trim(),
                Timestamp = DateTime.UtcNow,
                IsChatbot = false,
                IsBroadcast = true,
                TargetRole = string.IsNullOrWhiteSpace(req.Audience) ? null : req.Audience
            };

            var res = await client.PostAsJsonAsync("api/Message/broadcast", payload);
            if (!res.IsSuccessStatusCode)
                return StatusCode(502, new { error = "Broadcast failed via API." });

            return Ok(new { message = "Broadcast sent." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerChatbotAsk([FromBody] PMChatAskRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Empty message" });

            var answer = await GenerateManagerBotReplyAsync(req.Text);

            // (optional) log Q/A to Messages
            try
            {
                var client = await CreateApiClient();
                var me = GetCurrentUserId();
                var now = DateTime.UtcNow;
                await client.PostAsJsonAsync("api/Message", new {
                    SenderId = me, ReceiverId = 0, MessageText = req.Text, Timestamp = now, IsChatbot = false
                });
                await client.PostAsJsonAsync("api/Message", new {
                    SenderId = me, ReceiverId = 0, MessageText = answer.Reply ?? answer.Html, Timestamp = now, IsChatbot = true
                });
            }
            catch { /* non-blocking */ }

            return Ok(answer);
        }

        private async Task<PMChatAskResponse> GenerateManagerBotReplyAsync(string text)
        {
            var msg = (text ?? "").Trim().ToLowerInvariant();
            var client = await CreateApiClient();

            // Small helpers to pull data directly from your API
            async Task<List<EliteRentals.Models.DTOs.PropertyReadDto>> GetProps()
            {
                var res = await client.GetAsync("api/property");
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<EliteRentals.Models.DTOs.PropertyReadDto>>() ?? new();
            }
            async Task<List<EliteRentals.Models.DTOs.PaymentDto>> GetPays()
            {
                var res = await client.GetAsync("api/payment");
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<EliteRentals.Models.DTOs.PaymentDto>>() ?? new();
            }
            async Task<List<EliteRentals.Models.DTOs.LeaseDto>> GetLeases()
            {
                var res = await client.GetAsync("api/lease");
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<EliteRentals.Models.DTOs.LeaseDto>>() ?? new();
            }
            async Task<List<EliteRentals.Models.Maintenance>> GetMaint()
            {
                var res = await client.GetAsync("api/maintenance");
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<EliteRentals.Models.Maintenance>>() ?? new();
            }
            async Task<List<EliteRentals.Models.DTOs.UserDto>> GetUsers()
            {
                var res = await client.GetAsync("api/users");
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<EliteRentals.Models.DTOs.UserDto>>() ?? new();
            }

            string linkMaint = Url.Action("ManagerMaintenance", "PropertyManager")!;
            string linkPays = Url.Action("ManagerPayments", "PropertyManager")!;
            string linkProps = Url.Action("ManagerProperties", "PropertyManager")!;
            string linkLeases = Url.Action("ManagerLeases", "PropertyManager")!;
            string linkMsgs = Url.Action("ManagerMessages", "PropertyManager")!;

            // ---------- Intents ----------
            if (msg.Contains("mainten") || msg.Contains("repair") || msg.Contains("fix"))
            {
                try
                {
                    var items = await GetMaint();
                    static string S(string? x) => (x ?? "").Trim();

                    int pending = items.Count(m => string.Equals(S(m.Status), "Pending", StringComparison.OrdinalIgnoreCase));
                    int inProg = items.Count(m => string.Equals(S(m.Status), "In Progress", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(S(m.Status), "InProgress", StringComparison.OrdinalIgnoreCase));
                    int open = pending + inProg;

                    var rows = items
                        .Where(m => S(m.Status).Equals("Pending", StringComparison.OrdinalIgnoreCase)
                                 || S(m.Status).Equals("In Progress", StringComparison.OrdinalIgnoreCase)
                                 || S(m.Status).Equals("InProgress", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(6)
                        .Select(m => new {
                            m.MaintenanceId,
                            Property = m.Property?.Title ?? $"Property #{m.PropertyId}",
                            ReportedBy = m.Tenant != null ? $"{m.Tenant.FirstName} {m.Tenant.LastName}" : $"Tenant #{m.TenantId}",
                            Priority = string.IsNullOrWhiteSpace(m.Urgency) ? "—" : m.Urgency,
                            Status = m.Status ?? "",
                            Created = m.CreatedAt.ToString("yyyy-MM-dd")
                        }).ToList();

                    var sb = new System.Text.StringBuilder();
                    sb.Append($@"
<div class=""mb-2""><strong>Maintenance:</strong> {pending} pending, {inProg} in-progress • {open} open.</div>
<table class=""table table-sm table-striped mb-2"">
  <thead><tr><th>ID</th><th>Property</th><th>Reported By</th><th>Priority</th><th>Status</th><th>Created</th></tr></thead>
  <tbody>");
                    foreach (var r in rows)
                    {
                        sb.Append($@"<tr>
  <td>#{r.MaintenanceId}</td>
  <td>{System.Net.WebUtility.HtmlEncode(r.Property)}</td>
  <td>{System.Net.WebUtility.HtmlEncode(r.ReportedBy)}</td>
  <td>{System.Net.WebUtility.HtmlEncode(r.Priority)}</td>
  <td>{System.Net.WebUtility.HtmlEncode(r.Status)}</td>
  <td>{r.Created}</td>
</tr>");
                    }
                    sb.Append($@"</tbody></table>
<a class=""btn btn-sm btn-primary"" href=""{linkMaint}"">Open Maintenance</a>");

                    return new PMChatAskResponse { Intent = "maintenance.summary", IsHtml = true, Html = sb.ToString() };
                }
                catch
                {
                    return new PMChatAskResponse { Intent = "maintenance.summary", Reply = "Maintenance lookup failed unexpectedly." };
                }
            }

            if ((msg.Contains("overdue") && msg.Contains("payment")) || msg.Contains("payment summary"))
            {
                try
                {
                    var pays = await GetPays();
                    var users = await GetUsers();
                    var nameMap = users.ToDictionary(u => u.UserId, u => $"{(u.FirstName ?? "").Trim()} {(u.LastName ?? "").Trim()}".Trim());
                    static string S(string? x) => (x ?? "").Trim();

                    var overdue = pays.Where(p => !string.Equals(S(p.Status), "Paid", StringComparison.OrdinalIgnoreCase)).ToList();
                    var overdueTotal = overdue.Sum(p => p.Amount);

                    var latest = pays
                        .OrderBy(p => string.Equals(S(p.Status), "Paid", StringComparison.OrdinalIgnoreCase)) // unpaid first
                        .ThenByDescending(p => p.Date)
                        .Take(6)
                        .Select(p => new {
                            p.PaymentId,
                            Tenant = nameMap.TryGetValue(p.TenantId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"Tenant {p.TenantId}",
                            Amount = p.Amount.ToString("C"),
                            Status = S(p.Status),
                            Date = p.Date.ToString("yyyy-MM-dd")
                        });

                    var sb = new System.Text.StringBuilder();
                    sb.Append($@"<div class=""mb-2""><strong>Payments:</strong> Overdue total <strong>{overdueTotal.ToString("C")}</strong>.</div>
<table class=""table table-sm table-striped mb-2""><thead>
<tr><th>ID</th><th>Tenant</th><th>Amount</th><th>Status</th><th>Date</th></tr></thead><tbody>");
                    foreach (var r in latest)
                    {
                        sb.Append($@"<tr>
<td>#{r.PaymentId}</td><td>{System.Net.WebUtility.HtmlEncode(r.Tenant)}</td>
<td>{r.Amount}</td><td>{System.Net.WebUtility.HtmlEncode(r.Status)}</td><td>{r.Date}</td>
</tr>");
                    }
                    sb.Append($@"</tbody></table>
<a class=""btn btn-sm btn-primary"" href=""{linkPays}"">Open Payments</a>");

                    return new PMChatAskResponse { Intent = "payments.overdue", IsHtml = true, Html = sb.ToString() };
                }
                catch
                {
                    return new PMChatAskResponse { Intent = "payments.overdue", Reply = "Payments lookup failed unexpectedly." };
                }
            }

            if ((msg.Contains("property") && (msg.Contains("stats") || msg.Contains("summary"))) || msg.Contains("vacanc"))
            {
                try
                {
                    var props = await GetProps();
                    static string S(string? x) => (x ?? "").Trim();

                    int total = props.Count;
                    int available = props.Count(p => string.Equals(S(p.Status), "Available", StringComparison.OrdinalIgnoreCase));
                    int occupied = props.Count(p => string.Equals(S(p.Status), "Occupied", StringComparison.OrdinalIgnoreCase));
                    int vacant = available;

                    var latest = props.OrderByDescending(p => p.PropertyId).Take(6).Select(p => new {
                        p.PropertyId, p.Title, p.Status, p.RentAmount
                    });

                    var sb = new System.Text.StringBuilder();
                    sb.Append($@"<div class=""mb-2""><strong>Properties:</strong> {total} total • {available} available • {occupied} occupied • {vacant} vacant.</div>
<table class=""table table-sm table-striped mb-2"">
<thead><tr><th>ID</th><th>Title</th><th>Status</th><th>Rent</th></tr></thead><tbody>");
                    foreach (var r in latest)
                    {
                        sb.Append($@"<tr>
<td>#{r.PropertyId}</td>
<td>{System.Net.WebUtility.HtmlEncode(r.Title ?? $"Property {r.PropertyId}")}</td>
<td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
<td>{r.RentAmount.ToString("C")}</td>
</tr>");
                    }
                    sb.Append($@"</tbody></table>
<a class=""btn btn-sm btn-primary"" href=""{linkProps}"">Open Properties</a>");

                    return new PMChatAskResponse { Intent = "property.summary", IsHtml = true, Html = sb.ToString() };
                }
                catch
                {
                    return new PMChatAskResponse { Intent = "property.summary", Reply = "Property lookup failed unexpectedly." };
                }
            }

            if ((msg.Contains("lease") && msg.Contains("expir")) || msg.Contains("lease summary"))
            {
                try
                {
                    var leases = await GetLeases();
                    var users = await GetUsers();
                    var props = await GetProps();

                    var userName = users.ToDictionary(u => u.UserId, u => $"{(u.FirstName ?? "").Trim()} {(u.LastName ?? "").Trim()}".Trim());
                    var propTitle = props.ToDictionary(p => p.PropertyId, p => (p.Title ?? $"Property {p.PropertyId}").Trim());

                    var now = DateTime.UtcNow.Date;
                    int in30 = leases.Count(l => l.EndDate.Date > now && l.EndDate.Date <= now.AddDays(30));
                    int in60 = leases.Count(l => l.EndDate.Date > now.AddDays(30) && l.EndDate.Date <= now.AddDays(60));
                    int in90 = leases.Count(l => l.EndDate.Date > now.AddDays(60) && l.EndDate.Date <= now.AddDays(90));

                    var upcoming = leases.Where(l => l.EndDate.Date >= now)
                        .OrderBy(l => l.EndDate).Take(6)
                        .Select(l => new {
                            l.LeaseId,
                            Tenant = userName.TryGetValue(l.TenantId, out var tn) && !string.IsNullOrWhiteSpace(tn) ? tn : $"Tenant {l.TenantId}",
                            Property = propTitle.TryGetValue(l.PropertyId, out var pt) && !string.IsNullOrWhiteSpace(pt) ? pt : $"Property {l.PropertyId}",
                            EndDate = l.EndDate.ToString("yyyy-MM-dd"),
                            DaysLeft = (l.EndDate.Date - now).Days,
                            l.Status
                        });

                    var sb = new System.Text.StringBuilder();
                    sb.Append($@"<div class=""mb-2""><strong>Leases expiring:</strong> 30d: {in30} • 60d: {in60} • 90d: {in90}</div>
<table class=""table table-sm table-striped mb-2"">
<thead><tr><th>ID</th><th>Tenant</th><th>Property</th><th>End Date</th><th>Days Left</th><th>Status</th></tr></thead><tbody>");
                    foreach (var r in upcoming)
                    {
                        sb.Append($@"<tr>
<td>#{r.LeaseId}</td><td>{System.Net.WebUtility.HtmlEncode(r.Tenant)}</td>
<td>{System.Net.WebUtility.HtmlEncode(r.Property)}</td>
<td>{r.EndDate}</td><td>{r.DaysLeft}</td>
<td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
</tr>");
                    }
                    sb.Append($@"</tbody></table>
<a class=""btn btn-sm btn-primary"" href=""{linkLeases}"">Open Leases</a>");

                    return new PMChatAskResponse { Intent = "lease.expirations", IsHtml = true, Html = sb.ToString() };
                }
                catch
                {
                    return new PMChatAskResponse { Intent = "lease.expirations", Reply = "Lease lookup failed unexpectedly." };
                }
            }

            if (msg.Contains("assign") && (msg.Contains("caretaker") || msg.Contains("tech")))
            {
                return new PMChatAskResponse {
                    Intent = "maintenance.assign",
                    Reply = "Go to Manager → Maintenance, open a ticket, choose a caretaker in the dropdown, click Assign, then update status as work progresses."
                };
            }

            if (msg.Contains("broadcast") || msg.Contains("announcement"))
            {
                var html = $@"
<form id=""bc-form"" class=""vstack gap-2"">
  <div class=""row g-2"">
    <div class=""col-sm-4"">
      <label class=""form-label"">Audience</label>
      <select class=""form-select"" name=""audience"">
        <option value=""All"">All users</option>
        <option value=""Tenant"">Tenants</option>
        <option value=""Caretaker"">Caretakers</option>
        <option value=""PropertyManager"">Property Managers</option>
      </select>
    </div>
    <div class=""col-sm-8"">
      <label class=""form-label"">Message</label>
      <input class=""form-control"" name=""text"" placeholder=""Type your announcement..."" />
    </div>
  </div>
  <div class=""mt-2"">
    <button class=""btn btn-sm btn-primary"" type=""submit"">Send broadcast</button>
    <a class=""btn btn-sm btn-outline-secondary"" href=""{linkMsgs}"">Open Messages</a>
  </div>
</form>";
                return new PMChatAskResponse { Intent = "messages.broadcast", IsHtml = true, Html = html };
            }

            return new PMChatAskResponse {
                Intent = "fallback",
                Reply = "Try “maintenance summary”, “overdue payments”, “property stats”, or “leases expiring”."
            };
        }
    }
}

    