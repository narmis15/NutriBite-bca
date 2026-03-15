using NUTRIBITE.Services;
namespace NUTRIBITE.Services;

public record RazorpayOrderResult(string OrderId, int Amount, string Currency, string Receipt, System.Collections.Generic.IDictionary<string, object> Raw);

public interface IRazorpayService
{
    /// <summary>
    /// Creates a Razorpay order. Amount should be in major currency units (e.g. INR rupees).
    /// The service will convert amount to paise for Razorpay.
    /// </summary>
    /// <param name="amount">Amount in major units (e.g. 499.50 => ₹499.50)</param>
    /// <param name="currency">Currency code, default INR</param>
    /// <param name="receipt">Optional receipt id</param>
    /// <param name="notes">Optional notes</param>
    Task<RazorpayOrderResult> CreateOrderAsync(decimal amount, string currency = "INR", string? receipt = null, System.Collections.Generic.IDictionary<string, string>? notes = null);
}
