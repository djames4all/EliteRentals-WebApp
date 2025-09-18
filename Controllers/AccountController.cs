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

        private async Task SignInWithCookie(LoginResponseDto loginResponse)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, loginResponse.User.UserId.ToString()),
        new Claim(ClaimTypes.Name, loginResponse.User.FirstName),
        new Claim(ClaimTypes.Email, loginResponse.User.Email),
        new Claim(ClaimTypes.Role, loginResponse.User.Role),
        new Claim("JWT", loginResponse.Token) // keep token available for API calls
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Also keep session values if you want to read them later
            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);
        }


        // Start Google login
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

            using var client = _clientFactory.CreateClient();
            var response = await client.PostAsJsonAsync("https://localhost:7196/api/Users/sso",
                new { Provider = "Google", Token = idToken });

            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login");

            var responseBody = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Sign in with cookie
            await SignInWithCookie(loginResponse);

            // Save session values so navbar can detect login
            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);

            return RedirectToDashboard(loginResponse.User.Role);
        }





        private void SaveSession(LoginResponseDto loginResponse)
        {
            HttpContext.Session.SetString("JWT", loginResponse.Token);
            HttpContext.Session.SetString("UserRole", loginResponse.User.Role);
            HttpContext.Session.SetString("UserName", loginResponse.User.FirstName);
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

