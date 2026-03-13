using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUTRIBITE.Models;
using NUTRIBITE.Services;

var builder = WebApplication.CreateBuilder(args);

// Use the configured connection string key "DBCS" (points to FoodDeliveryDB in appsettings.json)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DBCS")));

// MVC
builder.Services.AddControllersWithViews();

// 🔥 REQUIRED FOR SESSION
builder.Services.AddDistributedMemoryCache();

// SESSION
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register existing services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<NUTRIBITE.Services.IHealthCalculationService, NUTRIBITE.Services.HealthCalculationService>();

// Razorpay service registration (singleton is safe; client is lightweight)
// Ensure RazorpayService will validate presence of keys at startup when resolved.
builder.Services.AddSingleton<IRazorpayService, RazorpayService>();

// Register location service (add near other builder.Services registrations)
builder.Services.AddHttpClient<NUTRIBITE.Services.ILocationService, NUTRIBITE.Services.LocationService>(client =>
{
    // identify your application for Nominatim policy (server-side header)
    client.DefaultRequestHeaders.Add("User-Agent", "NutriBite/1.0 (https://yourdomain.example)");
    client.DefaultRequestHeaders.Add("Referer", "https://yourdomain.example/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "login",
    pattern: "login",
    defaults: new { controller = "Auth", action = "Login" });

app.MapControllerRoute(
    name: "signup",
    pattern: "signup",
    defaults: new { controller = "Auth", action = "Register" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Public}/{action=Index}/{id?}");

app.Run();