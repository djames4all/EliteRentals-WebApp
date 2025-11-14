using System.Diagnostics;
using EliteRentals.Models;
using EliteRentals.Models.ViewModels;
using EliteRentals.Services;
using Microsoft.AspNetCore.Mvc;

namespace EliteRentals.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IEliteApi _api;
        private readonly IConfiguration _cfg;

        public HomeController(ILogger<HomeController> logger, IEliteApi api, IConfiguration cfg)
        {
            _logger = logger;
            _api = api;
            _cfg = cfg;
        }

        // HOME PAGE
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            try
            {
                var all = await _api.GetPropertiesAsync(ct);

                var vm = new PropertyListViewModel
                {
                    Items = all,
                    TotalCount = all.Count,
                    Query = new PropertyListQuery(),
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading properties for home page");

                var vm = new PropertyListViewModel
                {
                    Items = new List<EliteRentals.Models.DTOs.PropertyReadDto>(),
                    TotalCount = 0,
                    Query = new PropertyListQuery(),
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                };

                return View(vm);
            }
        }

        public IActionResult AboutUs() => View();
        public IActionResult OurServices() => View();
        public IActionResult ContactUs() => View();
        public IActionResult Privacy() => View();


        // CONTACT FORM SUBMISSION + VALIDATION + RANDOM FAILURE
        [HttpPost]
        public IActionResult SendContact(string Name, string Email, string Subject, string Message)
        {
            // VALIDATION
            if (string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Subject) ||
                string.IsNullOrWhiteSpace(Message))
            {
                TempData["Error"] = "Please fill in all fields.";
                return RedirectToAction("ContactUs");
            }

            // 25% CHANCE OF FAKE FAILURE
            var random = new Random();
            bool failed = random.Next(1, 5) == 1; // 1/4 chance

            if (failed)
            {
                TempData["Error"] = "Message failed to send. Please try again.";
                return RedirectToAction("ContactUs");
            }

            // SAVE IN MEMORY
            var contact = new ContactMessage
            {
                Name = Name,
                Email = Email,
                Subject = Subject,
                Message = Message,
                SubmittedAt = DateTime.Now
            };

            InMemoryContactStore.Messages.Add(contact);

            TempData["Success"] = "Your message has been sent successfully!";
            return RedirectToAction("ContactUs");
        }

        // VIEW ALL IN-MEMORY SUBMISSIONS
        public IActionResult ContactSubmissions()
        {
            return View(InMemoryContactStore.Messages);
        }

        // ERROR HANDLER
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
