using EliteRentals.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EliteRentals.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public AccountController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _clientFactory.CreateClient("EliteRentalsAPI");

            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/Users/login", content);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);

            // Redirect based on role
            return loginResponse.User.Role switch
            {
                "Tenant" => RedirectToAction("Index", "Home"),
                "Caretaker" => RedirectToAction("Index", "Home"),
                "PropertyManager" => RedirectToAction("ManagerDashboard", "PropertyManager"),
                "Admin" => RedirectToAction("AdminDashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _clientFactory.CreateClient("EliteRentalsAPI");

            // Directly send RegisterDto (matches API)
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/Users/signup", content);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Registration failed");
                return View(model);
            }

            return RedirectToAction("Login");
        }

        //[HttpGet]
        //public async Task<IActionResult> GoogleResponse()
        //{
        //    var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        //    if (!result.Succeeded) return RedirectToAction("Login");

        //    var claims = result.Principal.Identities.First().Claims;
        //    var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        //    var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        //    var client = _clientFactory.CreateClient("EliteRentalsAPI");

        //    var ssoPayload = new
        //    {
        //        Provider = "Google",
        //        Token = result.Properties.GetTokenValue("id_token") ?? "",
        //        Email = email,
        //        FirstName = name?.Split(" ").FirstOrDefault() ?? "",
        //        LastName = name?.Split(" ").Skip(1).FirstOrDefault() ?? "",
        //        Role = "Tenant"
        //    };

        //    var json = JsonSerializer.Serialize(ssoPayload);
        //    var content = new StringContent(json, Encoding.UTF8, "application/json");

        //    var response = await client.PostAsync("api/Users/sso", content);
        //    if (!response.IsSuccessStatusCode) return RedirectToAction("Login");

        //    var responseBody = await response.Content.ReadAsStringAsync();
        //    var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(
        //        responseBody,
        //        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        //    HttpContext.Session.SetString("JWT", loginResponse.Token);
        //    HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
        //    HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);

        //    return RedirectToAction("Index", "Home");
        //}


    }
}
