using EliteRentals.Models;
using EliteRentals.Models.DTOs;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;


namespace EliteRentals.Controllers
{
    public class AdminController : Controller
    {

        private readonly IHttpClientFactory _clientFactory;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IConfiguration _configuration;

        public AdminController(IHttpClientFactory clientFactory, IConfiguration configuration)
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
        }
        public IActionResult AdminChatbot() => View();
        public IActionResult AdminMessages() => View();
       
        public IActionResult AdminProperties() => View();
        public IActionResult AdminReports() => View();
        public IActionResult AdminSettings() => View();

        public async Task<IActionResult> AdminDashboard()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri("https://eliterentalsapi-czckh7fadmgbgtgf.southafricanorth-01.azurewebsites.net/");

            // 🔐 Attach JWT
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // --- Fetch leases ---
            List<LeaseDto> leases = new();
            try
            {
                var leaseResponse = await client.GetAsync("api/lease");
                if (leaseResponse.IsSuccessStatusCode)
                    leases = await leaseResponse.Content.ReadFromJsonAsync<List<LeaseDto>>() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching leases: {ex.Message}");
            }

            // --- Fetch properties ---
            List<PropertyDto> properties = new();
            try
            {
                var propertyResponse = await client.GetAsync("api/property");
                if (propertyResponse.IsSuccessStatusCode)
                    properties = await propertyResponse.Content.ReadFromJsonAsync<List<PropertyDto>>() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching properties: {ex.Message}");
            }

            // --- Fetch payments ---
            List<PaymentDto> payments = new();
            try
            {
                var paymentResponse = await client.GetAsync("api/payment");
                if (paymentResponse.IsSuccessStatusCode)
                    payments = await paymentResponse.Content.ReadFromJsonAsync<List<PaymentDto>>() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching payments: {ex.Message}");
            }

            // --- Fetch maintenance ---
            List<MaintenanceDto> maintenance = new();
            try
            {
                var maintenanceResponse = await client.GetAsync("api/maintenance");
                if (maintenanceResponse.IsSuccessStatusCode)
                    maintenance = await maintenanceResponse.Content.ReadFromJsonAsync<List<MaintenanceDto>>() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching maintenance: {ex.Message}");
            }

            // --- Occupancy Rate ---
            var leasedPropertyIds = leases
                .Where(l => l.Status?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true)
                .Select(l => l.PropertyId)
                .Distinct()
                .ToHashSet();

            var totalProperties = properties.Count;
            var occupiedProperties = leasedPropertyIds.Count;

            var occupancyRate = totalProperties > 0
                ? (int)((occupiedProperties / (double)totalProperties) * 100)
                : 0;

            // --- Overdue Payments ---
            var overduePayments = payments
                .Where(p => !p.Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true)
                .Sum(p => p.Amount);

            // --- Maintenance KPIs ---
            var activeMaintenance = maintenance.Count(m =>
                m.Status?.Equals("Pending", StringComparison.OrdinalIgnoreCase) == true ||
                m.Status?.Equals("In Progress", StringComparison.OrdinalIgnoreCase) == true);

            var pendingRequests = maintenance.Count(m =>
                m.Status?.Equals("Pending", StringComparison.OrdinalIgnoreCase) == true);

            // --- Recent Activities ---
            var recentActivities = payments.Select(p =>
            {
                var lease = leases.FirstOrDefault(l => l.TenantId == p.TenantId);

                return new RecentActivityDto
                {
                    Tenant = lease?.TenantName ?? "Unknown Tenant",
                    Property = lease?.PropertyTitle ?? "Unknown Property",
                    Action = "Payment",
                    Date = p.Date,
                    Status = p.Status ?? "Unknown"
                };
            })
            .Concat(maintenance.Select(m =>
            {
                var lease = leases.FirstOrDefault(l => l.TenantId == m.TenantId);

                return new RecentActivityDto
                {
                    Tenant = lease?.TenantName ?? "Unknown Tenant",
                    Property = lease?.PropertyTitle ?? "Unknown Property",
                    Action = "Maintenance",
                    Date = m.CreatedAt,
                    Status = m.Status ?? "Pending"
                };
            }))
            .OrderByDescending(a => a.Date)
            .Take(6)
            .ToList();

            // --- Rent Trends (Month + Year) ---
            var rentTrends = payments
                .Where(p => p.Date != default)
                .GroupBy(p => new { p.Date.Year, p.Date.Month })
                .Select(g => new RentTrendDto
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Amount = g.Sum(p => p.Amount)
                })
                .OrderBy(r => DateTime.ParseExact(r.Month, "MMM yyyy", null))
                .ToList();

            // --- Lease Expirations ---
            var leaseExpirations = leases
                .Where(l => l.EndDate != default)
                .GroupBy(l => new { l.EndDate.Year, l.EndDate.Month })
                .Select(g => new LeaseExpirationDto
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Count = g.Count()
                })
                .OrderBy(l => DateTime.ParseExact(l.Month, "MMM yyyy", null))
                .ToList();

            // --- ViewModel ---
            var model = new AdminDashboardViewModel
            {
                OccupancyRate = occupancyRate,
                OverduePayments = overduePayments,
                ActiveMaintenance = activeMaintenance,
                PendingRequests = pendingRequests,
                RecentActivities = recentActivities,
                RentTrends = rentTrends,
                LeaseExpirations = leaseExpirations
            };

            return View(model);
        }


        public async Task<IActionResult> AdminSystemUser()
        {
            var token = HttpContext.Session.GetString("JWT");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login", "Account");

            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("api/users");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load users.";
                return View(new List<EliteRentals.Models.DTOs.UserDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<EliteRentals.Models.DTOs.UserDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });


            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.PatchAsync($"api/users/{id}/status", null);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update user status.";
            }

            return RedirectToAction("AdminSystemUser");
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"api/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load user.";
                return RedirectToAction("AdminSystemUser");
            }

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<EliteRentals.Models.DTOs.UserDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View("EditUser", user); // Create EditUser.cshtml view
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(EliteRentals.Models.DTOs.UserDto user)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"api/users/{user.UserId}", content);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update user.";
                return View("EditUser", user);
            }

            return RedirectToAction("AdminSystemUser");
        }

        [HttpGet]
        public IActionResult AddUser()
        {
            return View(new EliteRentals.Models.DTOs.UserDto());
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(EliteRentals.Models.DTOs.UserDto user)
        {
            var token = HttpContext.Session.GetString("JWT");
            var client = _clientFactory.CreateClient("EliteRentalsAPI");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/users/signup", content);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to create user.";
                return View(user);
            }

            return RedirectToAction("AdminSystemUser");
        }
        // -------------------- LEASES --------------------
        [HttpGet]
        public async Task<IActionResult> AdminLeases()
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

            return RedirectToAction("AdminLeases");
        }

        [HttpGet]
        public async Task<IActionResult> EditLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("AdminLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);

            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction("AdminLeases");
            }

            ViewBag.Tenants = await FetchTenants();
            ViewBag.Properties = await FetchProperties();

            var dto = new LeaseCreateUpdateDto
            {
                PropertyId = lease.PropertyId,
                TenantId = lease.TenantId,
                StartDate = lease.StartDate,
                EndDate = lease.EndDate,
                Deposit = lease.Deposit,
                Status = lease.Status
            };

            ViewBag.LeaseId = id;
            return View(dto);
        }


        [HttpPost]
        public async Task<IActionResult> EditLease(int id, LeaseCreateUpdateDto dto)
        {
            var client = await CreateApiClient();

            // Map DTO to API Lease model
            var lease = new EliteRentals.Models.Lease
            {
                LeaseId = id,
                TenantId = dto.TenantId,
                PropertyId = dto.PropertyId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                MonthlyRent = dto.Deposit ?? 0,    // Map Deposit -> MonthlyRent
                LeaseStatus = dto.Status           // Map Status -> LeaseStatus
            };

            var json = JsonSerializer.Serialize(lease);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"api/lease/{id}", content);

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to update lease.";
                ViewBag.Tenants = await FetchTenants();
                ViewBag.Properties = await FetchProperties();
                return View(dto);
            }

            return RedirectToAction("AdminLeases");
        }



        [HttpGet]
        public async Task<IActionResult> DeleteLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("AdminLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);

            if (lease == null)
            {
                TempData["Error"] = "Lease not found.";
                return RedirectToAction("AdminLeases");
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
            return RedirectToAction("AdminLeases");
        }

        [HttpGet]
        public async Task<IActionResult> ViewLease(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/lease/{id}");
            if (!resp.IsSuccessStatusCode) return RedirectToAction("AdminLeases");

            var json = await resp.Content.ReadAsStringAsync();
            var lease = JsonSerializer.Deserialize<LeaseDto>(json, _jsonOptions);
            return View(lease);
        }

        [HttpGet]
        public async Task<IActionResult> AdminMaintenance()
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

            return RedirectToAction("AdminMaintenance");
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

            return RedirectToAction("AdminMaintenance");
        }

        [HttpGet]
        public async Task<IActionResult> ViewMaintenance(int id)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/maintenance/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to load maintenance details.";
                return RedirectToAction("AdminMaintenance");
            }

            var json = await resp.Content.ReadAsStringAsync();
            var m = JsonSerializer.Deserialize<EliteRentals.Models.Maintenance>(json, _jsonOptions);

            string? caretakerName = null;

            // ✅ If caretaker ID exists but caretaker object is null, fetch it
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
                    : caretakerName, // ✅ fallback name if fetched separately
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                ProofData = m.ProofData,
                ProofType = m.ProofType
            };

            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> AdminPayments()
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
                return RedirectToAction("AdminPayments"); 
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

            return RedirectToAction("AdminPayments");
        }






        // -------------------- HELPERS --------------------
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


    }
}

