using Microsoft.AspNetCore.Mvc;

namespace EliteRentals.Controllers
{
    public class PropertyManagerController : Controller
    {
        public IActionResult ManagerDashboard()
        {
            return View();
        }
        public IActionResult ManagerEscalations()
        {
            return View();
        }
        public IActionResult ManagerLeases()
        {
            return View();
        }
        public IActionResult ManagerMaintenance()
        {
            return View();
        }
        public IActionResult ManagerProperties()
        {
            return View();
        }
        public IActionResult ManagerSettings()
        {
            return View();
        }
    }
}
