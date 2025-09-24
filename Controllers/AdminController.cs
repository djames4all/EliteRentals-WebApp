using EliteRentals.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using EliteRentals.Models.DTOs;


namespace EliteRentals.Controllers
{
    public class AdminController : Controller
    {

        private readonly IHttpClientFactory _clientFactory;

        public AdminController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }
        public IActionResult AdminDashboard() => View();
        public IActionResult AdminChatbot() => View();
        public IActionResult AdminLeases() => View();
        public IActionResult AdminMaintenance() => View();
        public IActionResult AdminMessages() => View();
        public IActionResult AdminPayments() => View();
        public IActionResult AdminProperties() => View();
        public IActionResult AdminReports() => View();
        public IActionResult AdminSettings() => View();

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





    }
}
