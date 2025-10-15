using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EliteRentals.Services;
using EliteRentals.Models.DTOs;

namespace EliteRentals.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class PropertyManagerController : Controller
    {
        private readonly IEliteApi _api;

        public PropertyManagerController(IEliteApi api)
        {
            _api = api;
        }

        // ====== PROPERTIES ======

        // List ALL properties in the DB (as requested)
        public async Task<IActionResult> ManagerProperties(CancellationToken ct)
        {
            var all = await _api.GetPropertiesAsync(ct);
            return View(all);
        }

        // CREATE
        [HttpGet]
        public IActionResult ManagerPropertyCreate()
        {
            return View(new PropertyUploadDto { Status = "Available" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyCreate(PropertyUploadDto form, IFormFile? image, CancellationToken ct)
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
            return RedirectToAction(nameof(ManagerProperties));
        }

        // EDIT
        [HttpGet]
        public async Task<IActionResult> ManagerPropertyEdit(int id, CancellationToken ct)
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
                Status = (p.Status ?? "Available").Equals("Occupied", StringComparison.OrdinalIgnoreCase) ? "Occupied" : "Available"
            };
            ViewData["PropertyId"] = p.PropertyId;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyEdit(int id, PropertyUploadDto form, IFormFile? image, CancellationToken ct)
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

            TempData["ManagerPropertyMsg"] = "Property updated.";
            return RedirectToAction(nameof(ManagerProperties));
        }

        // DELETE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerPropertyDelete(int id, CancellationToken ct)
        {
            var ok = await _api.DeletePropertyAsync(id, ct);
            TempData["ManagerPropertyMsg"] = ok ? "Property deleted." : "Delete failed.";
            return RedirectToAction(nameof(ManagerProperties));
        }

        // ---------- helpers ----------
        private void NormalizeStatus(PropertyUploadDto form)
        {
            // restrict to Available or Occupied only
            if (string.Equals(form.Status, "Occupied", StringComparison.OrdinalIgnoreCase))
                form.Status = "Occupied";
            else
                form.Status = "Available";
        }

        // other dashboard pages remain as you had them
        public IActionResult ManagerDashboard() => View();
        public IActionResult ManagerEscalations() => View();
        public IActionResult ManagerLeases() => View();
        public IActionResult ManagerMaintenance() => View();
        public IActionResult ManagerSettings() => View();
    }
}
