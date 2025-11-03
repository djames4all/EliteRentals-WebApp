using EliteRentals.Models;
using EliteRentals.Models.DTOs;
using EliteRentals.Models.ViewModels;
using EliteRentals.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Linq;


namespace EliteRentals.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {

        private readonly IHttpClientFactory _clientFactory;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IConfiguration _configuration;
        private readonly EliteRentals.Services.EmailService _emailService;
        private readonly IEliteApi _api;

        public AdminController(IHttpClientFactory clientFactory, IConfiguration configuration, EmailService emailService,IEliteApi api)
        {
            _api = api;
            _clientFactory = clientFactory;
            _configuration = configuration;
            _emailService = emailService;
        }
        //public IActionResult AdminChatbot() => View();
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

            var now = DateTime.UtcNow;

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

            // --- Lease Health Summary ---
            var expiring30 = leases.Count(l => l.EndDate <= now.AddDays(30) && l.EndDate > now);
            var expiring60 = leases.Count(l => l.EndDate <= now.AddDays(60) && l.EndDate > now.AddDays(30));
            var expiring90 = leases.Count(l => l.EndDate <= now.AddDays(90) && l.EndDate > now.AddDays(60));

            var leasesMissingDocuments = leases.Count(l => l.DocumentData == null || l.DocumentData.Length == 0);

            var leasesWithOverduePayments = leases.Count(l =>
                payments.Any(p => p.TenantId == l.TenantId && !p.Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true));

            // --- Maintenance Aging Tracker ---
            var maintenanceAgingBuckets = maintenance
                .Where(m => m.Status != "Completed")
                .GroupBy(m => (DateTime.UtcNow - m.CreatedAt).Days / 7)
                .Select(g => new MaintenanceAgingDto
                {
                    WeeksOpen = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.WeeksOpen)
                .ToList();

            // --- Alerts & Notifications ---
            var leasesExpiringSoon = leases
                .Where(l => l.EndDate <= now.AddDays(30) && l.EndDate > now)
                .Select(l => new AlertDto
                {
                    Type = "Lease Expiring",
                    Message = $"{l.TenantName} - {l.PropertyTitle} expires on {l.EndDate:dd MMM yyyy}",
                    Severity = "warning"
                }).ToList();

            var propertiesWithNoLease = properties
                .Where(p => !leases.Any(l => l.PropertyId == p.PropertyId && l.Status?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true))
                .Select(p => new AlertDto
                {
                    Type = "Vacant Property",
                    Message = $"{p.Title} has no active lease",
                    Severity = "danger"
                }).ToList();

            var tenantsWithMultipleOverdues = payments
                .Where(p => !p.Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true)
                .GroupBy(p => p.TenantId)
                .Where(g => g.Count() >= 2)
                .Select(g => new AlertDto
                {
                    Type = "Overdue Payments",
                    Message = $"Tenant ID {g.Key} has {g.Count()} overdue payments",
                    Severity = "danger"
                }).ToList();

            var alerts = leasesExpiringSoon
                .Concat(propertiesWithNoLease)
                .Concat(tenantsWithMultipleOverdues)
                .ToList();

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

            // --- Rent Trends ---
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
                LeaseExpirations = leaseExpirations,
                ExpiringLeases30Days = expiring30,
                ExpiringLeases60Days = expiring60,
                ExpiringLeases90Days = expiring90,
                LeasesMissingDocuments = leasesMissingDocuments,
                LeasesWithOverduePayments = leasesWithOverduePayments,
                MaintenanceAgingBuckets = maintenanceAgingBuckets,
                Alerts = alerts
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportActivities()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri("https://eliterentalsapi-czckh7fadmgbgtgf.southafricanorth-01.azurewebsites.net/");
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var leases = await client.GetFromJsonAsync<List<LeaseDto>>("api/lease");
            var payments = await client.GetFromJsonAsync<List<PaymentDto>>("api/payment");
            var maintenance = await client.GetFromJsonAsync<List<MaintenanceDto>>("api/maintenance");

            var activities = payments.Select(p =>
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
            .ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Tenant,Property,Action,Date,Status");
            foreach (var act in activities)
            {
                csv.AppendLine($"\"{Escape(act.Tenant)}\",\"{Escape(act.Property)}\",\"{Escape(act.Action)}\",\"{act.Date:yyyy-MM-dd}\",\"{Escape(act.Status)}\"");

            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "RecentActivities.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportMaintenance()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri("https://eliterentalsapi-czckh7fadmgbgtgf.southafricanorth-01.azurewebsites.net/");
            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var leases = await client.GetFromJsonAsync<List<LeaseDto>>("api/lease");
            var maintenance = await client.GetFromJsonAsync<List<MaintenanceDto>>("api/maintenance");

            var csv = new StringBuilder();
            csv.AppendLine("Tenant,Property,Status,CreatedAt");

            foreach (var m in maintenance)
            {
                var lease = leases.FirstOrDefault(l => l.TenantId == m.TenantId);
                var tenant = lease?.TenantName ?? "Unknown Tenant";
                var property = lease?.PropertyTitle ?? "Unknown Property";
                csv.AppendLine($"\"{Escape(tenant)}\",\"{Escape(property)}\",\"{Escape(m.Status)}\",\"{m.CreatedAt:yyyy-MM-dd}\"");

            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "MaintenanceRequests.csv");
        }

        //======================SYSTEM USERS===============================================

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
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Generate temporary password
            user.Password = $"{user.FirstName}@{Guid.NewGuid():N}".Substring(0, 12);

            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/users/signup", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Failed to create user.";
                return View(user);
            }

            // Send email with temp password
            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Your New Account",
                    $"Hello {user.FirstName},\n\nYour temporary password is: {user.Password}\nPlease log in and change it immediately."
                );

                ViewBag.Success = "User created successfully! Temp password emailed to user.";
            }
            catch (Exception ex)
            {
                ViewBag.Warning = "User created, but failed to send email: " + ex.Message;
            }

            // Clear form for new user
            return View(new EliteRentals.Models.DTOs.UserDto());
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
        // GET: Edit Lease
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

            return RedirectToAction("AdminLeases");
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



        //=================MAINTENANCE==============================================

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

        // ===================== ADMIN • PROPERTIES =====================

        // LIST
        public async Task<IActionResult> AdminProperties(CancellationToken ct)
        {
            var all = await _api.GetPropertiesAsync(ct);
            return View(all);
        }

        // VIEW DETAILS
        [HttpGet]
        public async Task<IActionResult> AdminPropertyView(int id, CancellationToken ct)
        {
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/property/{id}", ct);
            if (!resp.IsSuccessStatusCode)
            {
                TempData["AdminPropertyErr"] = "Unable to load property.";
                return RedirectToAction(nameof(AdminProperties));
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var p = JsonSerializer.Deserialize<PropertyReadDto>(json, _jsonOptions);
            return View(p); // Views/Admin/AdminPropertyView.cshtml
        }

        // CREATE (GET)
        [HttpGet]
        public IActionResult AdminPropertyCreate()
        {
            return View(new PropertyUploadDto { Status = "Available" }); // Views/Admin/AdminPropertyCreate.cshtml
        }

        // CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminPropertyCreate(PropertyUploadDto form, IFormFile? image, CancellationToken ct)
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
            return RedirectToAction(nameof(AdminProperties));
        }

        // EDIT (GET)
        [HttpGet]
        public async Task<IActionResult> AdminPropertyEdit(int id, CancellationToken ct)
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

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminPropertyEdit(int id, PropertyUploadDto form, IFormFile? image, CancellationToken ct)
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

            TempData["AdminPropertyMsg"] = "Property updated.";
            return RedirectToAction(nameof(AdminProperties));
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminPropertyDelete(int id, CancellationToken ct)
        {
            var ok = await _api.DeletePropertyAsync(id, ct);
            TempData["AdminPropertyMsg"] = ok ? "Property deleted." : "Delete failed.";
            return RedirectToAction(nameof(AdminProperties));
        }

        // Ensure this helper exists in AdminController
        private void NormalizeStatus(PropertyUploadDto form)
        {
            form.Status = string.Equals(form.Status, "Occupied", StringComparison.OrdinalIgnoreCase) ? "Occupied" : "Available";
        }

        // GET: Admin Messages page - Server-side implementation
        public async Task<IActionResult> AdminMessages()
        {
            try
            {
                var adminId = GetCurrentUserId();
                var client = await CreateApiClient();

                var model = new AdminMessagesViewModel
                {
                    CurrentUserId = adminId,
                    CurrentUserName = HttpContext.Session.GetString("UserName") ?? "Admin"
                };

                // Load users
                var usersResponse = await client.GetAsync("api/users");
                if (usersResponse.IsSuccessStatusCode)
                {
                    var usersJson = await usersResponse.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<Models.UserDto>>(usersJson, _jsonOptions) ?? new();
                    model.Users = users.Where(u => u.UserId != adminId).ToList();
                }

                // Load Inbox (only non-archived)
                var inboxResponse = await client.GetAsync($"api/Message/inbox/{adminId}");
                var inbox = new List<MessageDto>();
                if (inboxResponse.IsSuccessStatusCode)
                {
                    var inboxJson = await inboxResponse.Content.ReadAsStringAsync();
                    inbox = JsonSerializer.Deserialize<List<MessageDto>>(inboxJson, _jsonOptions) ?? new();
                    inbox = inbox.Where(m => !m.ArchivedDate.HasValue).ToList(); // ✅ filter out archived
                }

                // Load Sent (only non-archived)
                var sentResponse = await client.GetAsync($"api/Message/sent/{adminId}");
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
                model.Conversations = await BuildConversations(inbox.Concat(sent).ToList(), adminId, client);

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
                var adminId = GetCurrentUserId();
                var client = await CreateApiClient();

                // ✅ Map DTO to API Message entity
                var message = new
                {
                    SenderId = adminId,
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

                return RedirectToAction("AdminMessages");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending message: {ex.Message}";
                return RedirectToAction("AdminMessages");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendBroadcast(string MessageText, string TargetRole)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var client = await CreateApiClient();

                // ✅ Map DTO to API Message entity
                var message = new
                {
                    SenderId = adminId,
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

                return RedirectToAction("AdminMessages");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending broadcast: {ex.Message}";
                return RedirectToAction("AdminMessages");
            }
        }




        // GET: View conversation with a specific user
        public async Task<IActionResult> ViewConversation(int userId)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var client = await CreateApiClient();

                // Get the conversation
                var conversationResponse = await client.GetAsync($"api/Message/conversation/{adminId}/{userId}");
                if (!conversationResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Failed to load conversation.";
                    return RedirectToAction("AdminMessages");
                }

                var conversationJson = await conversationResponse.Content.ReadAsStringAsync();
                var messages = JsonSerializer.Deserialize<List<MessageDto>>(conversationJson, _jsonOptions) ?? new List<MessageDto>();

                // Get user info for the other user
                var otherUserName = await GetUserName(userId, client);

                var model = new ConversationDetailViewModel
                {
                    OtherUserId = userId,
                    OtherUserName = otherUserName,
                    Messages = messages,
                    CurrentUserId = adminId
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading conversation: {ex.Message}";
                return RedirectToAction("AdminMessages");
            }
        }

        private async Task<List<ConversationDto>> BuildConversations(List<MessageDto> messages, int currentUserId, HttpClient client)
        {
            var conversations = new Dictionary<int, ConversationDto>();

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
                    conversations[otherUserId] = new ConversationDto
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
            var adminId = GetCurrentUserId();
            var client = await CreateApiClient();
            var response = await client.GetAsync($"api/Message/inbox/{adminId}");

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
            var adminId = GetCurrentUserId();
            var client = await CreateApiClient();
            var resp = await client.GetAsync($"api/message/archived/{adminId}");

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

        // -------------------- HELPERS --------------------


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

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
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

        private string GenerateChatbotResponse(string userMessage)
        {
            // Simple chatbot logic - you can enhance this
            userMessage = userMessage.ToLower();

            if (userMessage.Contains("hello") || userMessage.Contains("hi"))
                return "Hello! How can I help you today?";
            else if (userMessage.Contains("rent") || userMessage.Contains("property"))
                return "I can help you with property rentals. Please contact our admin for more details.";
            else if (userMessage.Contains("maintenance"))
                return "For maintenance requests, please submit a maintenance ticket through your tenant portal.";
            else if (userMessage.Contains("payment"))
                return "Payment issues? Please check your tenant portal or contact our billing department.";
            else
                return "Thank you for your message. Our team will get back to you shortly.";
        }

        // Helper method to generate PDF
        private IActionResult GeneratePdf(List<RecentActivityDto> records, string title, string filePrefix)
        {
            using var ms = new MemoryStream();
            var doc = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
            iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
            doc.Open();

            var titleFont = FontFactory.GetFont("Arial", 16, iTextSharp.text.Font.BOLD);
            var tableFont = FontFactory.GetFont("Arial", 12);

            doc.Add(new Paragraph(title, titleFont));
            doc.Add(new Paragraph($"Generated on {DateTime.Now:dd MMM yyyy}\n\n"));

            var table = new PdfPTable(5) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 3, 3, 2, 2, 2 });

            void AddHeader(string text) => table.AddCell(new PdfPCell(new Phrase(text, tableFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
            AddHeader("Tenant"); AddHeader("Property"); AddHeader("Action"); AddHeader("Date"); AddHeader("Status");

            foreach (var r in records)
            {
                table.AddCell(new PdfPCell(new Phrase(r.Tenant, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(r.Property, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(r.Action, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(r.Date.ToString("yyyy-MM-dd"), tableFont)));
                table.AddCell(new PdfPCell(new Phrase(r.Status, tableFont)));
            }

            doc.Add(table);
            doc.Close();

            return File(ms.ToArray(), "application/pdf", $"{filePrefix}_{DateTime.Now:yyyyMMdd}.pdf");
        }
        // ================== EXPORT REPORTS ==================

        private HttpClient GetApiClientWithJwt()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri("https://eliterentalsapi-czckh7fadmgbgtgf.southafricanorth-01.azurewebsites.net/api/");

            var token = HttpContext.Session.GetString("JWT");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            client.Timeout = TimeSpan.FromMinutes(5);
            return client;
        }

        // Export Recent Activities as PDF
        [HttpGet]
        public async Task<IActionResult> ExportActivitiesReportPdf(DateTime? startDate, DateTime? endDate, int? propertyId, string status)
        {
            var client = GetApiClientWithJwt();

            var leases = await client.GetFromJsonAsync<List<LeaseDto>>("lease");
            var maintenance = await client.GetFromJsonAsync<List<MaintenanceDto>>("maintenance");
            var payments = await client.GetFromJsonAsync<List<PaymentDto>>("payment");

            var records = new List<RecentActivityDto>();

            // Combine maintenance and payments
            records.AddRange(maintenance.Select(m =>
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
            }));

            records.AddRange(payments.Select(p =>
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
            }));

            // Apply filters
            if (startDate.HasValue) records = records.Where(r => r.Date >= startDate.Value).ToList();
            if (endDate.HasValue) records = records.Where(r => r.Date <= endDate.Value).ToList();
            if (propertyId.HasValue)
            {
                var propTitle = leases.FirstOrDefault(l => l.PropertyId == propertyId)?.PropertyTitle ?? "";
                records = records.Where(r => r.Property.Contains(propTitle)).ToList();
            }
            if (!string.IsNullOrEmpty(status)) records = records.Where(r => r.Status == status).ToList();

            return GeneratePdf(records, "Recent Activities Report", "RecentActivities");
        }

        // Export Maintenance Requests as PDF
        [HttpGet]
        public async Task<IActionResult> ExportMaintenanceReportPdf(DateTime? startDate, DateTime? endDate, int? propertyId, string status)
        {
            var client = GetApiClientWithJwt();

            var leases = await client.GetFromJsonAsync<List<LeaseDto>>("lease");
            var maintenance = await client.GetFromJsonAsync<List<MaintenanceDto>>("maintenance");

            var records = maintenance.Select(m =>
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
            }).ToList();

            if (startDate.HasValue) records = records.Where(r => r.Date >= startDate.Value).ToList();
            if (endDate.HasValue) records = records.Where(r => r.Date <= endDate.Value).ToList();
            if (propertyId.HasValue)
            {
                var propTitle = leases.FirstOrDefault(l => l.PropertyId == propertyId)?.PropertyTitle ?? "";
                records = records.Where(r => r.Property.Contains(propTitle)).ToList();
            }
            if (!string.IsNullOrEmpty(status)) records = records.Where(r => r.Status == status).ToList();

            return GeneratePdf(records, "Maintenance Requests Report", "MaintenanceRequests");
        }

        // Export Payments as PDF
        [HttpGet]
        public async Task<IActionResult> ExportPaymentsReportPdf(DateTime? startDate, DateTime? endDate, int? propertyId, string status)
        {
            var client = GetApiClientWithJwt();

            var leases = await client.GetFromJsonAsync<List<LeaseDto>>("lease");
            var payments = await client.GetFromJsonAsync<List<PaymentDto>>("payment");

            var records = payments.Select(p =>
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
            }).ToList();

            if (startDate.HasValue) records = records.Where(r => r.Date >= startDate.Value).ToList();
            if (endDate.HasValue) records = records.Where(r => r.Date <= endDate.Value).ToList();
            if (propertyId.HasValue)
            {
                var propTitle = leases.FirstOrDefault(l => l.PropertyId == propertyId)?.PropertyTitle ?? "";
                records = records.Where(r => r.Property.Contains(propTitle)).ToList();
            }
            if (!string.IsNullOrEmpty(status)) records = records.Where(r => r.Status == status).ToList();

            return GeneratePdf(records, "Payments Report", "Payments");
        }

// ⬇️ inside the AdminController class
// (1) Small DTOs for request/response
public class AdminChatAskRequest
{
    public string Text { get; set; } = "";
}
        public class AdminChatAskResponse
        {
            public string Reply { get; set; } = "";
            public string Intent { get; set; } = "";

            // NEW:
            public bool IsHtml { get; set; } = false;  // if true, the view will render Html as-is
            public string? Html { get; set; }          // optional rich content
        }
public class AdminBroadcastRequest
{
    public string Audience { get; set; } = "All";   // All, Tenant, Caretaker, PropertyManager
    public string Text { get; set; } = "";
}
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> AdminChatbotBroadcast([FromBody] AdminBroadcastRequest req)
{
    if (string.IsNullOrWhiteSpace(req?.Text))
        return BadRequest(new { error = "Message is required." });

    var ok = await _api.BroadcastAsync(req.Audience ?? "All", req.Text.Trim());
    if (!ok) return StatusCode(502, new { error = "Broadcast failed via API." });



    return Ok(new { message = "Broadcast sent." });
}


// (2) Route to the Chatbot page (view already exists)
[Authorize(Roles = "Admin")]
[HttpGet]
public IActionResult AdminChatbot()
{
    return View();
}

// (3) Main chat endpoint
[Authorize(Roles = "Admin")]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AdminChatbotAsk([FromBody] AdminChatAskRequest req)
{
    if (string.IsNullOrWhiteSpace(req?.Text))
        return BadRequest(new { error = "Empty message" });

    var answer = await GenerateAdminBotReplyAsync(req.Text);

    // optional: log both sides to API messages (skip if API down)
    try
    {
        var api = await CreateApiClientAsync();
        var me = await GetCurrentUserIdAsync(); // returns int?; may be null if not mapped
        if (me.HasValue)
        {
            var now = DateTime.UtcNow;
            await api.PostAsJsonAsync("api/Message", new {
                SenderId = me.Value,
                ReceiverId = 0, // system
                MessageText = req.Text,
                Timestamp = now,
                IsChatbot = false
            });
            await api.PostAsJsonAsync("api/Message", new {
                SenderId = me.Value,
                ReceiverId = 0,
                MessageText = answer.Reply,
                Timestamp = now,
                IsChatbot = true,
                IsBroadcast = false
            });
        }
    }
    catch { /* non-blocking */ }

    return Ok(answer);
}

// (4) The “brain”: intent routing + live KPIs from your API
private async Task<AdminChatAskResponse> GenerateAdminBotReplyAsync(string text)
{
    var msg = (text ?? "").Trim().ToLowerInvariant();
    var api = await CreateApiClientAsync();

    // --- helpers that call your API ---
    async Task<(int total, int available, int occupied, int vacant)> PropertyStats()
    {
        var props = await GetJsonAsync<List<PropertyRead>>(api, "api/Property");
        int total = props?.Count ?? 0;
        int occupied = props?.Count(p => string.Equals(p.Status, "Occupied", StringComparison.OrdinalIgnoreCase)) ?? 0;
        int available = props?.Count(p => string.Equals(p.Status, "Available", StringComparison.OrdinalIgnoreCase)) ?? 0;
        int vacant = total - occupied;
        return (total, available, occupied, vacant);
    }

    async Task<(int pending, int inProgress, int open)> MaintenanceStats()
    {
        var items = await GetJsonAsync<List<MaintenanceRead>>(api, "api/Maintenance");
        int pending = items?.Count(m => string.Equals(m.Status ?? "Pending","Pending", StringComparison.OrdinalIgnoreCase)) ?? 0;
        int inProg  = items?.Count(m => string.Equals(m.Status ?? "","In Progress", StringComparison.OrdinalIgnoreCase)) ?? 0;
        return (pending, inProg, (pending + inProg));
    }

    async Task<decimal> OverduePaymentsTotal()
    {
        var pays = await GetJsonAsync<List<PaymentRead>>(api, "api/Payment");
        return pays?.Where(p => !string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount) ?? 0m;
    }

    async Task<(int in30, int in60, int in90)> LeaseExpiring()
    {
        var leases = await GetJsonAsync<List<LeaseRead>>(api, "api/Lease");
        var now = DateTime.UtcNow;
        int in30 = leases?.Count(l => l.EndDate > now && l.EndDate <= now.AddDays(30)) ?? 0;
        int in60 = leases?.Count(l => l.EndDate > now.AddDays(30) && l.EndDate <= now.AddDays(60)) ?? 0;
        int in90 = leases?.Count(l => l.EndDate > now.AddDays(60) && l.EndDate <= now.AddDays(90)) ?? 0;
        return (in30, in60, in90);
    }

    // --- intents ---
    if (msg.Contains("mainten") || msg.Contains("repair") || msg.Contains("fix"))
{
    try
    {
        var items = await _api.GetMaintenanceAsync() ?? new List<MaintenanceDto>();

        static string S(string? s) => (s ?? "").Trim();

        int pending = items.Count(m => string.Equals(S(m.Status), "Pending", StringComparison.OrdinalIgnoreCase));
        int inProg  = items.Count(m => string.Equals(S(m.Status), "In Progress", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(S(m.Status), "InProgress", StringComparison.OrdinalIgnoreCase));
        int open    = pending + inProg;

        var openRows = items
            .Where(m => string.Equals(S(m.Status), "Pending", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(m.Status), "In Progress", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(m.Status), "InProgress", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .Select(m => new {
                m.MaintenanceId,
                m.PropertyId,
                Property   = m.PropertyName,
                TenantId   = m.TenantId,
                ReportedBy = m.ReportedBy,
                Priority   = string.IsNullOrWhiteSpace(m.Priority) ? "—" : m.Priority,
                Status     = m.Status,
                Created    = m.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append($@"
<div class=""mb-2""><strong>Maintenance:</strong> {pending} pending, {inProg} in-progress • {open} open.</div>
<table class=""table table-sm table-striped mb-2"">
  <thead>
    <tr>
      <th>ID</th>
      <th>Property</th>
      <th>Reported By</th>
      <th>Priority</th>
      <th>Status</th>
      <th>Created</th>
    </tr>
  </thead>
  <tbody>");

        foreach (var r in openRows)
        {
            sb.Append($@"
    <tr>
      <td>#{r.MaintenanceId}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Property ?? $"Property {r.PropertyId}")}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.ReportedBy ?? $"Tenant {r.TenantId}")}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Priority ?? "—")}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
      <td>{r.Created}</td>
    </tr>");
        }

        sb.Append(@"
  </tbody>
</table>
<a class=""btn btn-sm btn-primary"" href=""__MAINT_URL__"">Open Maintenance</a>");

        return new AdminChatAskResponse {
            Intent = "maintenance.summary",
            IsHtml = true,
            Html   = sb.ToString()
        };
    }
    catch
    {
        return new AdminChatAskResponse {
            Intent = "maintenance.summary",
            Reply  = "Maintenance lookup failed unexpectedly."
        };
    }
}

if ((msg.Contains("overdue") && msg.Contains("payment")) || msg.Contains("payment summary"))
{
    try
    {
        var pays  = await _api.GetPaymentsAsync() 
                    ?? new List<EliteRentals.Models.DTOs.PaymentDto>();
        var users = await _api.GetUsersAsync()    
                    ?? new List<EliteRentals.Models.DTOs.UserDto>();

        // Build a quick lookup: userId -> "First Last"
        var nameMap = users.ToDictionary(
            u => u.UserId,
            u => $"{(u.FirstName ?? "").Trim()} {(u.LastName ?? "").Trim()}".Trim()
        );

        static string S(string? s) => (s ?? "").Trim();

        // Overdue = anything not "Paid"
        var overdueList  = pays.Where(p => !string.Equals(S(p.Status), "Paid", StringComparison.OrdinalIgnoreCase)).ToList();
        var overdueTotal = overdueList.Sum(p => p.Amount);

        // Latest 6 (unpaid first, then newest)
        var latest = pays
            .OrderBy(p => string.Equals(S(p.Status), "Paid", StringComparison.OrdinalIgnoreCase)) // unpaid first
            .ThenByDescending(p => p.Date)
            .Take(6)
            .Select(p => new {
                p.PaymentId,
                TenantName = nameMap.TryGetValue(p.TenantId, out var n) && !string.IsNullOrWhiteSpace(n)
                                ? n
                                : $"Tenant {p.TenantId}",
                Amount = p.Amount.ToString("C"),
                Status = S(p.Status),
                Date   = p.Date.ToString("yyyy-MM-dd")
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append($@"
<div class=""mb-2""><strong>Payments:</strong> Overdue total <strong>{overdueTotal.ToString("C")}</strong>.</div>
<table class=""table table-sm table-striped mb-2"">
  <thead>
    <tr>
      <th>ID</th>
      <th>Tenant</th>
      <th>Amount</th>
      <th>Status</th>
      <th>Date</th>
    </tr>
  </thead>
  <tbody>");

        foreach (var r in latest)
        {
            sb.Append($@"
    <tr>
      <td>#{r.PaymentId}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.TenantName)}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Amount)}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
      <td>{r.Date}</td>
    </tr>");
        }

        sb.Append(@"
  </tbody>
</table>
<a class=""btn btn-sm btn-primary"" href=""__PAY_URL__"">Open Payments</a>");

        return new AdminChatAskResponse {
            Intent = "payments.overdue",
            IsHtml = true,
            Html   = sb.ToString()
        };
    }
    catch
    {
        return new AdminChatAskResponse {
            Intent = "payments.overdue",
            Reply  = "Payments lookup failed unexpectedly."
        };
    }
}

if ((msg.Contains("property") && (msg.Contains("stats") || msg.Contains("summary"))) || msg.Contains("vacanc"))
{
    try
    {
        var props = await _api.GetPropertiesAsync() ?? new List<EliteRentals.Models.DTOs.PropertyReadDto>();

        static string S(string? s) => (s ?? "").Trim();

        int total     = props.Count;
        int available = props.Count(p => string.Equals(S(p.Status), "Available", StringComparison.OrdinalIgnoreCase));
        int occupied  = props.Count(p => string.Equals(S(p.Status), "Occupied",  StringComparison.OrdinalIgnoreCase));
        int vacant    = available; // treating "Available" as "Vacant"

        // latest properties (most recently created/added → fallback to id desc)
        var latest = props
            .OrderByDescending(p => p.PropertyId)
            .Take(6)
            .Select(p => new {
                p.PropertyId,
                Title  = p.Title,
                Status = p.Status,
                Rent   = p.RentAmount
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append($@"
<div class=""mb-2""><strong>Properties:</strong> {total} total • {available} available • {occupied} occupied • {vacant} vacant.</div>
<table class=""table table-sm table-striped mb-2"">
  <thead>
    <tr>
      <th>ID</th>
      <th>Title</th>
      <th>Status</th>
      <th>Rent</th>
    </tr>
  </thead>
  <tbody>");

        foreach (var r in latest)
        {
            sb.Append($@"
    <tr>
      <td>#{r.PropertyId}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Title ?? $"Property {r.PropertyId}")}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
      <td>{r.Rent.ToString("C")}</td>
    </tr>");
        }

        sb.Append(@"
  </tbody>
</table>
<a class=""btn btn-sm btn-primary"" href=""__PROP_URL__"">Open Properties</a>");

        return new AdminChatAskResponse {
            Intent = "property.summary",
            IsHtml = true,
            Html   = sb.ToString()
        };
    }
    catch
    {
        return new AdminChatAskResponse {
            Intent = "property.summary",
            Reply  = "Property lookup failed unexpectedly."
        };
    }
}



            if ((msg.Contains("lease") && msg.Contains("expir")) || msg.Contains("lease summary"))
{
    try
    {
        var leases = await _api.GetLeasesAsync() ?? new List<EliteRentals.Models.DTOs.LeaseDto>();
        var users  = await _api.GetUsersAsync()  ?? new List<EliteRentals.Models.DTOs.UserDto>();
        var props  = await _api.GetPropertiesAsync() ?? new List<EliteRentals.Models.DTOs.PropertyReadDto>();

        // Lookups for names/titles
        var userName = users.ToDictionary(
            u => u.UserId,
            u => $"{(u.FirstName ?? "").Trim()} {(u.LastName ?? "").Trim()}".Trim()
        );
        var propTitle = props.ToDictionary(
            p => p.PropertyId,
            p => (p.Title ?? $"Property {p.PropertyId}").Trim()
        );

        var now = DateTime.UtcNow.Date;
        int in30 = leases.Count(l => l.EndDate.Date > now && l.EndDate.Date <= now.AddDays(30));
        int in60 = leases.Count(l => l.EndDate.Date > now.AddDays(30) && l.EndDate.Date <= now.AddDays(60));
        int in90 = leases.Count(l => l.EndDate.Date > now.AddDays(60) && l.EndDate.Date <= now.AddDays(90));

        // Next 6 upcoming expirations
        var upcoming = leases
            .Where(l => l.EndDate.Date >= now)
            .OrderBy(l => l.EndDate)
            .Take(6)
            .Select(l => new {
                l.LeaseId,
                Tenant = userName.TryGetValue(l.TenantId, out var tn) && !string.IsNullOrWhiteSpace(tn) ? tn : $"Tenant {l.TenantId}",
                Property = propTitle.TryGetValue(l.PropertyId, out var pt) && !string.IsNullOrWhiteSpace(pt) ? pt : $"Property {l.PropertyId}",
                EndDate = l.EndDate.ToString("yyyy-MM-dd"),
                DaysLeft = (l.EndDate.Date - now).Days,
                Status = l.Status
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append($@"
<div class=""mb-2""><strong>Leases expiring:</strong> 30d: {in30} • 60d: {in60} • 90d: {in90}</div>
<table class=""table table-sm table-striped mb-2"">
  <thead>
    <tr>
      <th>ID</th>
      <th>Tenant</th>
      <th>Property</th>
      <th>End Date</th>
      <th>Days Left</th>
      <th>Status</th>
    </tr>
  </thead>
  <tbody>");

        foreach (var r in upcoming)
        {
            sb.Append($@"
    <tr>
      <td>#{r.LeaseId}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Tenant)}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Property)}</td>
      <td>{r.EndDate}</td>
      <td>{r.DaysLeft}</td>
      <td>{System.Net.WebUtility.HtmlEncode(r.Status ?? "")}</td>
    </tr>");
        }

        sb.Append(@"
  </tbody>
</table>
<a class=""btn btn-sm btn-primary"" href=""__LEASE_URL__"">Open Leases</a>");

        return new AdminChatAskResponse {
            Intent = "lease.expirations",
            IsHtml = true,
            Html   = sb.ToString()
        };
    }
    catch
    {
        return new AdminChatAskResponse {
            Intent = "lease.expirations",
            Reply  = "Lease lookup failed unexpectedly."
        };
    }
}


    if (msg.Contains("assign") && (msg.Contains("caretaker") || msg.Contains("tech")))
    {
        return new() {
            Intent = "maintenance.assign",
            Reply = "Go Admin → Maintenance, open a ticket, pick a caretaker in the dropdown, click Assign, then update status as work progresses."
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
  <div>
    <button class=""btn btn-sm btn-primary"" type=""submit"">Send broadcast</button>
    <a class=""btn btn-sm btn-outline-secondary"" href=""__MSG_URL__"">Open Messages</a>
  </div>
</form>";
    return new AdminChatAskResponse {
        Intent = "messages.broadcast",
        IsHtml = true,
        Html = html
    };
}

    // fallback
    return new() { Intent = "fallback", Reply = "I didn’t catch that. Try “maintenance summary”, “overdue payments”, “property stats”, or “leases expiring”." };
}

// (5) Minimal typed readers for JSON mapping (shape matches your API outputs)
private record PropertyRead(int PropertyId, string? Title, string? Status);
private record MaintenanceRead(int MaintenanceId, string? Status);
private record PaymentRead(int PaymentId, decimal Amount, string Status);
private record LeaseRead(int LeaseId, DateTime EndDate);

// (6) HTTP helpers — pulls JWT from session if available
private async Task<HttpClient> CreateApiClientAsync()
{
    var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client  = factory.CreateClient("EliteRentalsAPI");

    // Try to get a stored JWT from session or auth ticket
    var token = HttpContext.Session.GetString("JwtToken");
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Accept JSON
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return await Task.FromResult(client);
}

private async Task<T?> GetJsonAsync<T>(HttpClient client, string path)
{
    try
    {
        using var res = await client.GetAsync(path);
        if (!res.IsSuccessStatusCode) return default;
        var stream = await res.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });
    }
    catch { return default; }
}

private async Task<int?> GetCurrentUserIdAsync()
{
    // common claim types: "userId", "nameid", "sub". Adjust if needed.
    var idClaim = User?.Claims?.FirstOrDefault(c => c.Type is "userId" or "nameid" or "sub")?.Value;
    return await Task.FromResult(int.TryParse(idClaim, out var id) ? id : (int?)null);
}


    }
    

}

