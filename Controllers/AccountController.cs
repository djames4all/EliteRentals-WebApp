using EliteRentals.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EliteRentals.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _configuration;

        public AccountController(IHttpClientFactory clientFactory, IConfiguration configuration)
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
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

            await SignInWithCookie(loginResponse);
            return RedirectToDashboard(loginResponse.User.Role);
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

        private async Task SignInWithCookie(LoginResponseDto loginResponse)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, loginResponse.User.UserId.ToString()),
                new Claim(ClaimTypes.Name, loginResponse.User.FirstName),
                new Claim(ClaimTypes.Email, loginResponse.User.Email),
                new Claim(ClaimTypes.Role, loginResponse.User.Role),
                new Claim("JWT", loginResponse.Token)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Session storage
            HttpContext.Session.SetString("UserId", loginResponse.User.UserId.ToString());
            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);
        }

        // 🔹 GOOGLE LOGIN FLOW
        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return RedirectToAction("Login");

            var idToken = result.Properties.GetTokenValue("id_token");

            // Use the live Azure API base URL
            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"]
                ?? "https://eliterentalsapi-czckh7fadmgbgtgf.southafricanorth-01.azurewebsites.net/";

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync(
                $"{apiBaseUrl}api/Users/sso",
                new { Provider = "Google", Token = idToken }
            );

            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login");

            var responseBody = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Store UserId in session for Google login too
            HttpContext.Session.SetString("UserId", loginResponse.User.UserId.ToString());
            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, loginResponse.User.UserId.ToString()),
        new Claim(ClaimTypes.Name, loginResponse.User.FirstName),
        new Claim(ClaimTypes.Email, loginResponse.User.Email),
        new Claim(ClaimTypes.Role, loginResponse.User.Role),
        new Claim("JWT", loginResponse.Token)
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToDashboard(loginResponse.User.Role);
        }

        private IActionResult RedirectToDashboard(string role) =>
            role switch
            {
                "Tenant" => RedirectToAction("Index", "Home"),
                "Caretaker" => RedirectToAction("Index", "Home"),
                "PropertyManager" => RedirectToAction("ManagerDashboard", "PropertyManager"),
                "Admin" => RedirectToAction("AdminDashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
    }
}
