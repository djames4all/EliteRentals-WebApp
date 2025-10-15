using EliteRentals.Models;
using EliteRentals.Models.DTOs;
using EliteRentals.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace EliteRentals.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class PropertyManagerController : Controller
    {
        private readonly IEliteApi _api;

        private readonly IHttpClientFactory _clientFactory;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public PropertyManagerController(IEliteApi api, IHttpClientFactory clientFactory)
        {
            _api = api;
            _clientFactory = clientFactory;
        }

        // ====== PROPERTIES ======

        // List ALL properties in the DB (as requested)
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
        public async Task<IActionResult> ManagerPropertyCreate(PropertyUploadDto form, IFormFile? image, CancellationToken ct)
        {
            NormalizeStatus(form);
            if (!ModelState.IsValid) return View(form);

            var id = await _api.CreatePropertyAsync(form, image, ct);
            if (id == null)
            {
                ModelState.AddModelError(string.Empty, "Could not create property. Check your input and try again.");
                return View(form);
            }

            TempData["ManagerPropertyMsg"] = "Property created successfully.";
            return RedirectToAction(nameof(ManagerProperties));
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
                Status = (p.Status ?? "Available").Equals("Occupied", StringComparison.OrdinalIgnoreCase) ? "Occupied" : "Available"
            };
            ViewData["PropertyId"] = p.PropertyId;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyEdit(int id, PropertyUploadDto form, IFormFile? image, CancellationToken ct)
        {
            NormalizeStatus(form);
            if (!ModelState.IsValid) { ViewData["PropertyId"] = id; return View(form); }

            var ok = await _api.UpdatePropertyAsync(id, form, image, ct);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Update failed. Please try again.");
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


        //---------------------LEASES------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> ManagerLeases()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/lease"); // matches API
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
        public async Task<IActionResult> AddLease()
        {
            ViewBag.Tenants = await FetchTenants();
            ViewBag.Properties = await FetchProperties();
            return View(new LeaseCreateUpdateDto
            {
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddMonths(12)
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddLease(LeaseCreateUpdateDto dto)
        {
            var client = await CreateApiClient();
            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("api/lease", content);

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to create lease.";
                ViewBag.Tenants = await FetchTenants();
                ViewBag.Properties = await FetchProperties();
                return View(dto);
            }

            return RedirectToAction("ManagerLeases");
        }
        // GET: Edit Lease
        [HttpGet]
        public async Task<IActionResult> EditLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("ManagerLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);
            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction("ManagerLeases");
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
        public async Task<IActionResult> EditLease(LeaseCreateUpdateDto dto)
        {
            var client = await CreateApiClient();

            // attach JWT
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Only send allowed fields
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

            Console.WriteLine($"➡️ PUT api/lease/{dto.LeaseId} payload: {json}");

            var resp = await client.PutAsync($"api/lease/{dto.LeaseId}", content);

            Console.WriteLine($"⬅️ Response Status: {resp.StatusCode}");

            if (!resp.IsSuccessStatusCode)
            {
                var respBody = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Response Body: {respBody}");
                TempData["Error"] = $"Failed to update lease. ({resp.StatusCode})";

                ViewBag.Tenants = await FetchTenants();
                ViewBag.Properties = await FetchProperties();
                return View(dto);
            }

            Console.WriteLine("✅ Lease updated successfully!");
            return RedirectToAction("ManagerLeases");
        }



        [HttpGet]
        public async Task<IActionResult> DeleteLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("ManagerLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);

            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction("ManagerLeases");
            }

            // Fetch tenants and properties to display their names
            ViewBag.Tenants = await FetchTenants();
            ViewBag.Properties = await FetchProperties();

            return View(lease);
        }


        [HttpPost, ActionName("DeleteLease")]
        public async Task<IActionResult> DeleteLeaseConfirmed(int id)
        {
            var client = await CreateApiClient();
            await client.DeleteAsync($"api/lease/{id}");
            return RedirectToAction("ManagerLeases");
        }

        [HttpGet]
        public async Task<IActionResult> ViewLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("ManagerLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);
            return View(lease);
        }

        //=================MAINTENANCE=============================
        [HttpGet]
        public async Task<IActionResult> ManagerMaintenance()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/maintenance");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load maintenance requests.";
                ViewBag.Caretakers = new List<Models.UserDto>();
                return View(new List<MaintenanceDto>());
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

            // ✅ Correct assignment (no extra semicolon / new list)
            ViewBag.Caretakers = await FetchCaretakers();

            return View(maintenance);
        }




        [HttpPost]
        public async Task<IActionResult> AssignCaretaker(int maintenanceId, int caretakerId)
        {
            var client = await CreateApiClient();

            var dto = new { AssignedCaretakerId = caretakerId };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/maintenance/{maintenanceId}/assign-caretaker", content);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to assign caretaker.";

            return RedirectToAction("ManagerMaintenance");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMaintenanceStatus(int maintenanceId, string status)
        {
            var client = await CreateApiClient();

            var dto = new { Status = status };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/maintenance/{maintenanceId}/status", content);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to update status.";

            return RedirectToAction("ManagerMaintenance");
        }

        [HttpGet]
        public async Task<IActionResult> ViewMaintenance(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/maintenance/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load maintenance details.";
                return RedirectToAction("ManagerMaintenance");
            }

            var json = await resp.Content.ReadAsStringAsync();
            var m = JsonSerializer.Deserialize<EliteRentals.Models.Maintenance>(json, _jsonOptions);

            string? caretakerName = null;

            // If caretaker ID exists but caretaker object is null, fetch it
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

            var dto = new EliteRentals.Models.DTOs.MaintenanceDto
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
                    : caretakerName,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                ProofData = m.ProofData,
                ProofType = m.ProofType
            };

            return View(dto);
        }

        //================PAYMENTS===============================

        [HttpGet]
        public async Task<IActionResult> ManagerPayments()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/payment");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load payments.";
                return View(new List<PaymentDto>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var payments = JsonSerializer.Deserialize<List<PaymentDto>>(json, _jsonOptions) ?? new List<PaymentDto>();

            // Optionally fetch tenants to display names
            var tenants = await FetchTenants();

            // Combine tenant name into each payment
            foreach (var p in payments)
            {
                var tenant = tenants.FirstOrDefault(t => t.UserId == p.TenantId);
                if (tenant != null)
                {
                    p.TenantName = $"{tenant.FirstName} {tenant.LastName}";
                }
            }

            return View(payments);
        }

        [HttpGet]
        public async Task<IActionResult> ViewPayment(int id)
        {
            var client = await CreateApiClient();
            var response = await client.GetAsync($"api/payment/{id}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Unable to fetch payment details.";
                return RedirectToAction("ManagerPayments");
            }

            var json = await response.Content.ReadAsStringAsync();
            var payment = JsonSerializer.Deserialize<PaymentDto>(json, _jsonOptions);

            return View(payment);
        }


        [HttpPost]
        public async Task<IActionResult> UpdatePaymentStatus(int id, string status)
        {
            var client = await CreateApiClient();

            var dto = new { Status = status };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/payment/{id}/status", content);

            if (!resp.IsSuccessStatusCode)
                TempData["Error"] = "Failed to update payment status.";

            return RedirectToAction("ManagerPayments");
        }

        //======================SYSTEM USERS===============================================

        public async Task<IActionResult> ManagerTenants()
        {
            var token = HttpContext.Session.GetString("JWT");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login", "Account");

            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("api/users");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load tenants.";
                return View(new List<EliteRentals.Models.DTOs.UserDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<EliteRentals.Models.DTOs.UserDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Filter only tenants
            var tenants = users?.Where(u => u.Role == "Tenant").ToList() ?? new List<EliteRentals.Models.DTOs.UserDto>();

            return View(tenants);
        }

        [HttpPost]
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

            return RedirectToAction("ManagerTenants");
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
                return RedirectToAction("ManagerTenants");
            }

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<EliteRentals.Models.DTOs.UserDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Only allow editing if user is a tenant
            if (user.Role != "Tenant")
                return RedirectToAction("ManagerTenants");

            return View("EditUser", user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(EliteRentals.Models.DTOs.UserDto user)
        {
            if (user.Role != "Tenant")
                return RedirectToAction("ManagerTenants");

            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"api/users/{user.UserId}", content);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update tenant.";
                return View("EditUser", user);
            }

            return RedirectToAction("ManagerTenants");
        }

        [HttpGet]
        public IActionResult AddUser()
        {
            var tenant = new EliteRentals.Models.DTOs.UserDto
            {
                Role = "Tenant"
            };
            return View(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(EliteRentals.Models.DTOs.UserDto user)
        {
            user.Role = "Tenant"; // force role as Tenant

            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Generate a temporary password
            user.Password = $"{user.FirstName}@{Guid.NewGuid():N}".Substring(0, 12);

            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/users/signup", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to create tenant.";
                return View(user);
            }

            ViewBag.Success = "Tenant created successfully!";
            ViewBag.GeneratedPassword = user.Password;

            return View(new EliteRentals.Models.DTOs.UserDto { Role = "Tenant" });
        }



        // ---------- helpers ----------
        private void NormalizeStatus(PropertyUploadDto form)
        {
            // restrict to Available or Occupied only
            if (string.Equals(form.Status, "Occupied", StringComparison.OrdinalIgnoreCase))
                form.Status = "Occupied";
            else
                form.Status = "Available";
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
            var users = JsonSerializer.Deserialize<List<TenantDto>>(json, _jsonOptions) ?? new List<TenantDto>();
            return users.Where(u => string.IsNullOrEmpty(u.Role) || u.Role == "Tenant").ToList();
        }

        private async Task<List<PropertyDto>> FetchProperties()
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync("api/property");
            if (!resp.IsSuccessStatusCode) return new List<PropertyDto>();

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PropertyDto>>(json, _jsonOptions) ?? new List<PropertyDto>();
        }

        private async Task<List<Models.DTOs.UserDto>> FetchCaretakers()
        {
            try
            {
                var client = await CreateApiClient();
                var response = await client.GetAsync("api/users");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[FetchCaretakers] Failed API call: {response.StatusCode}");
                    return new List<Models.DTOs.UserDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<Models.DTOs.UserDto>>(json, _jsonOptions) ?? new List<Models.DTOs.UserDto>();

                Console.WriteLine($"[FetchCaretakers] Found {users.Count} users.");

                var caretakers = users
                    .Where(u => !string.IsNullOrEmpty(u.Role) &&
                                u.Role.Equals("Caretaker", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Console.WriteLine($"[FetchCaretakers] Filtered {caretakers.Count} caretakers.");
                return caretakers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FetchCaretakers] Error: {ex.Message}");
                return new List<Models.DTOs.UserDto>();
            }
        }


        // other dashboard pages remain as you had them
        public IActionResult ManagerDashboard() => View();
        public IActionResult ManagerEscalations() => View();
        public IActionResult ManagerSettings() => View();

    }
}
