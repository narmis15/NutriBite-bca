using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUTRIBITE.Models;

namespace NUTRIBITE.Services
{
    /// <summary>
    /// Location service implementation using OpenStreetMap Nominatim API.
    /// No API key required. Respect usage policy in production (rate limits, caching, user-agent).
    /// </summary>
    public class LocationService : ILocationService
    {
        private readonly HttpClient _http;
        private readonly ILogger<LocationService> _logger;
        private const string UserAgent = "NutriBite/1.0 (+https://yourdomain.example)";

        public LocationService(HttpClient httpClient, ILogger<LocationService> logger)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Set a sensible default user-agent required by Nominatim policy.
            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent))
            {
                // ignored, but attempt to set above.
            }
            // Nominatim requires identifying User-Agent and Referer
            if (!_http.DefaultRequestHeaders.Contains("Referer"))
                _http.DefaultRequestHeaders.Add("Referer", "https://yourdomain.example/");
        }

        public async Task<UserLocation?> ReverseGeocodeAsync(decimal latitude, decimal longitude)
        {
            try
            {
                // Use Nominatim reverse endpoint with addressdetails
                var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}&addressdetails=1";
                using var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim reverse geocode returned {Status}", res.StatusCode);
                    return null;
                }

                var payload = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var display = root.GetPropertyOrNull("display_name")?.GetString();

                var addr = root.GetPropertyOrNull("address");
                var city = addr?.GetPropertyOrNull("city")?.GetString()
                           ?? addr?.GetPropertyOrNull("town")?.GetString()
                           ?? addr?.GetPropertyOrNull("village")?.GetString()
                           ?? addr?.GetPropertyOrNull("county")?.GetString();

                var suburb = addr?.GetPropertyOrNull("suburb")?.GetString()
                             ?? addr?.GetPropertyOrNull("neighbourhood")?.GetString()
                             ?? addr?.GetPropertyOrNull("hamlet")?.GetString();

                var state = addr?.GetPropertyOrNull("state")?.GetString();
                var postcode = addr?.GetPropertyOrNull("postcode")?.GetString();

                return new UserLocation
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    City = city,
                    Area = suburb,
                    State = state,
                    Pincode = postcode,
                    FullAddress = display
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reverse geocode failed");
                return null;
            }
        }

        public async Task<List<UserLocation>> SearchLocationAsync(string query, int limit = 5)
        {
            var results = new List<UserLocation>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                // Nominatim search endpoint
                var url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&q={Uri.EscapeDataString(query)}&addressdetails=1&limit={limit}";
                using var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim search returned {Status}", res.StatusCode);
                    return results;
                }

                var payload = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(payload);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var lat = el.GetPropertyOrNull("lat")?.GetString();
                    var lon = el.GetPropertyOrNull("lon")?.GetString();
                    var display = el.GetPropertyOrNull("display_name")?.GetString();
                    var addr = el.GetPropertyOrNull("address");

                    if (decimal.TryParse(lat, out var dlat) && decimal.TryParse(lon, out var dlon))
                    {
                        var city = addr?.GetPropertyOrNull("city")?.GetString()
                                   ?? addr?.GetPropertyOrNull("town")?.GetString()
                                   ?? addr?.GetPropertyOrNull("village")?.GetString()
                                   ?? addr?.GetPropertyOrNull("county")?.GetString();

                        var suburb = addr?.GetPropertyOrNull("suburb")?.GetString()
                                     ?? addr?.GetPropertyOrNull("neighbourhood")?.GetString();

                        var state = addr?.GetPropertyOrNull("state")?.GetString();
                        var postcode = addr?.GetPropertyOrNull("postcode")?.GetString();

                        results.Add(new UserLocation
                        {
                            Latitude = dlat,
                            Longitude = dlon,
                            City = city,
                            Area = suburb,
                            State = state,
                            Pincode = postcode,
                            FullAddress = display
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchLocationAsync failed");
                return results;
            }
        }
    }

    // Small helper extension for JsonElement safety
    internal static class JsonExtensions
    {
        public static JsonElement? GetPropertyOrNull(this JsonElement? el, string propertyName)
        {
            if (el == null || !el.HasValue) return null;
            var element = el.Value;
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (element.TryGetProperty(propertyName, out var p)) return p;
            return null;
        }

        public static JsonElement? GetPropertyOrNull(this JsonElement el, string propertyName)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (el.TryGetProperty(propertyName, out var p)) return p;
            return null;
        }
    }
}