using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUTRIBITE.Models;
using NUTRIBITE.Services;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
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

// Register services (use ADO.NET-based services)
builder.Services.AddScoped<IOrderService, OrderService>();

// Register Health calculation service (service layer)
builder.Services.AddScoped<NUTRIBITE.Services.IHealthCalculationService, NUTRIBITE.Services.HealthCalculationService>();
builder.Services.AddHttpContextAccessor();
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

