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

        private void SaveToStorage(string lat, string lon, string city, string area, string state, string pincode, string address)
        {
            // Save to Session
            HttpContext.Session.SetString("UserLatitude", lat);
            HttpContext.Session.SetString("UserLongitude", lon);
            HttpContext.Session.SetString("UserCity", city);
            HttpContext.Session.SetString("UserArea", area);
            HttpContext.Session.SetString("UserState", state);
            HttpContext.Session.SetString("UserPincode", pincode);
            HttpContext.Session.SetString("UserAddress", address);

            // Save to Persistent Cookies (valid for 30 days)
            var options = new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) };
            HttpContext.Response.Cookies.Append("UserLatitude", lat, options);
            HttpContext.Response.Cookies.Append("UserLongitude", lon, options);
            HttpContext.Response.Cookies.Append("UserCity", city, options);
            HttpContext.Response.Cookies.Append("UserArea", area, options);
            HttpContext.Response.Cookies.Append("UserState", state, options);
            HttpContext.Response.Cookies.Append("UserPincode", pincode, options);
            HttpContext.Response.Cookies.Append("UserAddress", address, options);
        }

        // GET /Location/GetCurrentLocation
        // Returns stored session/cookie location if available (prevents repeated prompting).
        [HttpGet]
        public IActionResult GetCurrentLocation()
        {
            try
            {
                var lat = HttpContext.Session.GetString("UserLatitude") ?? HttpContext.Request.Cookies["UserLatitude"];
                var lon = HttpContext.Session.GetString("UserLongitude") ?? HttpContext.Request.Cookies["UserLongitude"];
                var city = HttpContext.Session.GetString("UserCity") ?? HttpContext.Request.Cookies["UserCity"];
                var address = HttpContext.Session.GetString("UserAddress") ?? HttpContext.Request.Cookies["UserAddress"];
                var pincode = HttpContext.Session.GetString("UserPincode") ?? HttpContext.Request.Cookies["UserPincode"];
                var area = HttpContext.Session.GetString("UserArea") ?? HttpContext.Request.Cookies["UserArea"];
                var state = HttpContext.Session.GetString("UserState") ?? HttpContext.Request.Cookies["UserState"];

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
        // Returns structured address and stores it.
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

                SaveToStorage(
                    location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    location.City ?? "",
                    location.Area ?? "",
                    location.State ?? "",
                    location.Pincode ?? "",
                    location.FullAddress ?? ""
                );

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
                SaveToStorage(
                    req.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    req.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    req.City ?? "",
                    req.Area ?? "",
                    req.State ?? "",
                    req.Pincode ?? "",
                    req.FullAddress ?? ""
                );

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