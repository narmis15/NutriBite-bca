using System.Threading.Tasks;
using NUTRIBITE.Models;

namespace NUTRIBITE.Services
{
    /// <summary>
    /// Location service contract: reverse-geocode coordinates to structured address.
    /// </summary>
    public interface ILocationService
    {
        /// <summary>
        /// Reverse geocode latitude/longitude to a UserLocation using a geocoding provider (Nominatim by default).
        /// </summary>
        Task<UserLocation?> ReverseGeocodeAsync(decimal latitude, decimal longitude);
        
        /// <summary>
        /// Forward geocode a free-text query to a list of candidate locations.
        /// Useful for manual search in the UI.
        /// </summary>
        Task<System.Collections.Generic.List<UserLocation>> SearchLocationAsync(string query, int limit = 5);
    }
}