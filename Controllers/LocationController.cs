using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NUTRIBITE.Models;
using NUTRIBITE.Services;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    /// <summary>
    /// Handles saving/reading user location; controller stores values in session.
    /// </summary>
    public class LocationController : Controller
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(ILocationService locationService, ILogger<LocationController> logger)
        {
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET /Location/GetCurrentLocation
        // Returns stored session location if available (prevents repeated prompting).
        [HttpGet]
        public IActionResult GetCurrentLocation()
        {
            try
            {
                var lat = HttpContext.Session.GetString("UserLatitude");
                var lon = HttpContext.Session.GetString("UserLongitude");
                var city = HttpContext.Session.GetString("UserCity");
                var address = HttpContext.Session.GetString("UserAddress");
                var pincode = HttpContext.Session.GetString("UserPincode");
                var area = HttpContext.Session.GetString("UserArea");
                var state = HttpContext.Session.GetString("UserState");

                if (string.IsNullOrEmpty(lat) || string.IsNullOrEmpty(lon))
                    return Json(new { success = false });

                return Json(new
                {
                    success = true,
                    latitude = lat,
                    longitude = lon,
                    city,
                    area,
                    state,
                    pincode,
                    address
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrentLocation failed");
                return Json(new { success = false });
            }
        }

        // POST /Location/SaveLocation
        // Body: { latitude, longitude }
        // Returns structured address and stores it in session.
        [HttpPost]
        public async Task<IActionResult> SaveLocation([FromBody] SaveLocationRequest req)
        {
            if (req == null)
                return BadRequest(new { success = false, message = "Invalid payload" });

            try
            {
                // validate inputs
                if (req.Latitude is null || req.Longitude is null)
                    return BadRequest(new { success = false, message = "Coordinates required" });

                // Reverse geocode
                var location = await _locationService.ReverseGeocodeAsync(req.Latitude.Value, req.Longitude.Value);
                if (location == null)
                    return StatusCode(502, new { success = false, message = "Geocoding failed" });

                // Save into session
                HttpContext.Session.SetString("UserLatitude", location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("UserLongitude", location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("UserCity", location.City ?? "");
                HttpContext.Session.SetString("UserArea", location.Area ?? "");
                HttpContext.Session.SetString("UserState", location.State ?? "");
                HttpContext.Session.SetString("UserPincode", location.Pincode ?? "");
                HttpContext.Session.SetString("UserAddress", location.FullAddress ?? "");

                return Json(new { success = true, location });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveLocation failed");
                return StatusCode(500, new { success = false, message = "Failed to save location" });
            }
        }

        // Optional: allow manual save from UI search result
        [HttpPost]
        public IActionResult SaveLocationManual([FromBody] SaveLocationManualRequest req)
        {
            if (req == null) return BadRequest(new { success = false });

            try
            {
                HttpContext.Session.SetString("UserLatitude", req.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("UserLongitude", req.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("UserCity", req.City ?? "");
                HttpContext.Session.SetString("UserArea", req.Area ?? "");
                HttpContext.Session.SetString("UserState", req.State ?? "");
                HttpContext.Session.SetString("UserPincode", req.Pincode ?? "");
                HttpContext.Session.SetString("UserAddress", req.FullAddress ?? "");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveLocationManual failed");
                return StatusCode(500, new { success = false });
            }
        }

        // Request DTOs
        public class SaveLocationRequest
        {
            public decimal? Latitude { get; set; }
            public decimal? Longitude { get; set; }
        }

        public class SaveLocationManualRequest
        {
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
            public string? City { get; set; }
            public string? Area { get; set; }
            public string? State { get; set; }
            public string? Pincode { get; set; }
            public string? FullAddress { get; set; }
        }
    }
}