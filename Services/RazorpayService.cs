using NUTRIBITE.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Razorpay.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NUTRIBITE.Services;

public class RazorpayService : IRazorpayService, IDisposable
{
    private readonly RazorpayClient _client;
    private readonly ILogger<RazorpayService> _logger;
    private bool _disposed;

    public RazorpayService(IConfiguration configuration, ILogger<RazorpayService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Prefer configuration section "Razorpay" (appsettings) but also accept top-level keys if present.
        var keyId = configuration["Razorpay:KeyId"] ?? configuration["RAZORPAY_KEY_ID"];
        var keySecret = configuration["Razorpay:KeySecret"] ?? configuration["RAZORPAY_KEY_SECRET"];

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
        {
            // Fail fast with clear message. Do NOT log secrets.
            var msg = "Razorpay keys are not configured. Set Razorpay:KeyId and Razorpay:KeySecret in configuration (appsettings.json or environment variables).";
            _logger.LogCritical(msg);
            throw new InvalidOperationException(msg);
        }

        // Initialize client (Razorpay .NET SDK)
        try
        {
            _client = new RazorpayClient(keyId, keySecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Razorpay client.");
            throw;
        }
    }

    public async Task<RazorpayOrderResult> CreateOrderAsync(decimal amount, string currency = "INR", string? receipt = null, IDictionary<string, string>? notes = null)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) currency = "INR";

        // Razorpay expects amount in the smallest currency unit (paise for INR).
        var amountInPaise = Convert.ToInt32(Math.Round(amount * 100m));

        var payload = new Dictionary<string, object>
        {
            ["amount"] = amountInPaise,
            ["currency"] = currency,
            ["receipt"] = receipt ?? $"rcpt_{Guid.NewGuid():N}",
            ["payment_capture"] = 1
        };

        if (notes != null)
        {
            payload["notes"] = notes;
        }

        try
        {
            // The Razorpay SDK call is synchronous; wrap in Task.Run to avoid blocking
            var order = await Task.Run(() => _client.Order.Create(payload));

            var orderId = order["id"]?.ToString() ?? throw new InvalidOperationException("Razorpay order returned no id");
            var returnedAmount = Convert.ToInt32(order["amount"]); // in paise
            var returnedCurrency = order["currency"]?.ToString() ?? currency;
            var returnedReceipt = order["receipt"]?.ToString() ?? payload["receipt"].ToString();

            // Return amount in paise (consumer can convert back to major units if needed)
            var raw = new Dictionary<string, object>();
            foreach (var kv in order.Attributes)
            {
                raw[kv.Key] = kv.Value;
            }

            return new RazorpayOrderResult(orderId, returnedAmount, returnedCurrency, returnedReceipt, raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Razorpay order (amount: {Amount}, currency: {Currency})", amount, currency);
            throw new InvalidOperationException("Failed to create payment order. See server logs for details.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        // RazorpayClient has no managed disposal, but implement pattern if future cleanup required.
        _disposed = true;
    }
}
