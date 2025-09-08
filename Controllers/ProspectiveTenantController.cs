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
    }
}
