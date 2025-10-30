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

        // Loads all properties, picks 3 random in the view
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
        public IActionResult AboutUs()
        {
            return View();
        }
        public IActionResult OurServices()
        {
            return View();
        }

        public IActionResult ContactUs()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
