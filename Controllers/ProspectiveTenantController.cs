using Microsoft.AspNetCore.Mvc;

namespace EliteRentals.Controllers
{
    public class ProspectiveTenantController : Controller
    {
        public IActionResult TenantPropertyListings()
        {
            return View();
        }
        public IActionResult TenantApplicationForm()
        {
            return View();
        }
        public IActionResult TenantListingDetails()
        {
            return View();
        }
        public IActionResult AllApartments()
        {
            return View();
        }
        public IActionResult AllHouses()
        {
            return View();
        }
        public IActionResult AllTownhouses()
        {
            return View();
        }
        public IActionResult AllFeatured()
        {
            return View();
        }
    }
}
