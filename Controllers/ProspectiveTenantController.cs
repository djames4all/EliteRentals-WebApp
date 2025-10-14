using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using EliteRentals.Services;
using EliteRentals.Models.ViewModels;

namespace EliteRentals.Controllers
{
    public class ProspectiveTenantController : Controller
    {
        private readonly IEliteApi _api;
        private readonly IConfiguration _cfg;

        public ProspectiveTenantController(IEliteApi api, IConfiguration cfg)
        {
            _api = api;
            _cfg = cfg;
        }

        // LISTINGS
        public async Task<IActionResult> TenantPropertyListings([FromQuery] PropertyListQuery query, CancellationToken ct)
        {
            var all = await _api.GetPropertiesAsync(ct);

            if (!string.IsNullOrWhiteSpace(query.Location))
            {
                var key = query.Location.Trim().ToLowerInvariant();
                all = all.Where(p =>
                    (p.Address ?? "").ToLower().Contains(key) ||
                    (p.City ?? "").ToLower().Contains(key) ||
                    (p.Province ?? "").ToLower().Contains(key) ||
                    (p.Country ?? "").ToLower().Contains(key)
                ).ToList();
            }

            if (query.MinBedrooms.HasValue)
                all = all.Where(p => p.NumOfBedrooms >= query.MinBedrooms.Value).ToList();

            if (query.PetFriendly.HasValue)
                all = all.Where(p => p.PetFriendly == query.PetFriendly.Value).ToList();

            all = query.SortBy switch
            {
                "lowest"  => all.OrderBy(p => p.RentAmount).ToList(),
                "highest" => all.OrderByDescending(p => p.RentAmount).ToList(),
                _         => all.OrderByDescending(p => p.PropertyId).ToList(), // newest
            };

            var total = all.Count;
            var page = Math.Max(1, query.Page);
            var size = Math.Clamp(query.PageSize, 3, 48);
            var items = all.Skip((page - 1) * size).Take(size).ToList();

            var vm = new PropertyListViewModel
            {
                Items = items,
                TotalCount = total,
                Query = new PropertyListQuery
                {
                    Location = query.Location,
                    MinBedrooms = query.MinBedrooms,
                    PetFriendly = query.PetFriendly,
                    SortBy = query.SortBy,
                    Page = page,
                    PageSize = size
                },
                ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
            };

            return View(vm);
        }

        // DETAILS
        public async Task<IActionResult> TenantListingDetails(int id, CancellationToken ct)
        {
            if (id <= 0) return View("TenantListingNotFound", 0);

            var property = await _api.GetPropertyAsync(id, ct);
            if (property == null) return View("TenantListingNotFound", id);

            ViewData["ApiBaseUrl"] = _cfg["ApiSettings:BaseUrl"] ?? "";
            return View(property);
        }

        // APPLICATION - GET (block if property is not "Available")
        public async Task<IActionResult> TenantApplicationForm(int propertyId, CancellationToken ct)
        {
            if (propertyId <= 0) return View("TenantListingNotFound", 0);

            var property = await _api.GetPropertyAsync(propertyId, ct);
            if (property == null) return View("TenantListingNotFound", propertyId);

            if (!string.Equals(property.Status, "Available", StringComparison.OrdinalIgnoreCase))
            {
                TempData["AppSubmitted"] = "This property is not available for applications.";
                return RedirectToAction(nameof(TenantListingDetails), new { id = propertyId });
            }

            var vm = new RentalApplicationPageViewModel
            {
                Property = property,
                Form = new RentalApplicationCreateViewModel { PropertyId = property.PropertyId },
                ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
            };

            return View(vm);
        }

        // APPLICATION - POST (multipart to API) + authoritative availability re-check
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TenantApplicationForm(
            RentalApplicationCreateViewModel form,
            IFormFile? document,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var propBad = await _api.GetPropertyAsync(form.PropertyId, ct) ?? new();
                return View(new RentalApplicationPageViewModel
                {
                    Property = propBad,
                    Form = form,
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                });
            }

            // Authoritative re-check before calling the API
            var propForPost = await _api.GetPropertyAsync(form.PropertyId, ct);
            if (propForPost == null)
            {
                ModelState.AddModelError(string.Empty, "The selected property no longer exists.");
                return View(new RentalApplicationPageViewModel
                {
                    Property = new(),
                    Form = form,
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                });
            }
            if (!string.Equals(propForPost.Status, "Available", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "This property is not accepting applications.");
                return View(new RentalApplicationPageViewModel
                {
                    Property = propForPost,
                    Form = form,
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                });
            }

            var createdId = await _api.CreateRentalApplicationAsync(
                form.PropertyId, form.ApplicantName, form.Email, form.Phone, document, ct);

            if (createdId == null)
            {
                ModelState.AddModelError(string.Empty, "Could not submit your application. Please try again.");
                var propFail = await _api.GetPropertyAsync(form.PropertyId, ct) ?? new();
                return View(new RentalApplicationPageViewModel
                {
                    Property = propFail,
                    Form = form,
                    ApiBaseUrl = _cfg["ApiSettings:BaseUrl"] ?? ""
                });
            }

            TempData["AppSubmitted"] = "Your application was submitted successfully.";
            return RedirectToAction(nameof(TenantListingDetails), new { id = form.PropertyId });
        }

        // Optional category pages (not wired to API type yet)
        public IActionResult AllApartments() => View();
        public IActionResult AllHouses() => View();
        public IActionResult AllTownhouses() => View();
        public IActionResult AllFeatured() => View();
    }
}
