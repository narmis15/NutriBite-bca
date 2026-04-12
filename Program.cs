using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using global::NUTRIBITE.Services;
using global::NUTRIBITE.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using global::NUTRIBITE.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // Required for LocationService and others using HttpClient

// Database Context
var connectionString = builder.Configuration.GetConnectionString("DBCS");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Custom Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentDistributionService, PaymentDistributionService>();
builder.Services.AddScoped<ICategorisationService, CategorisationService>();
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<IRecipeAnalysisService, RecipeAnalysisService>();
builder.Services.AddScoped<IHealthCalculationService, HealthCalculationService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IRazorpayService, RazorpayService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddSignalR();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Ensure database is up to date
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // 1. Add IsBulk to Carttables if missing
        context.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Carttables]') AND name = N'IsBulk')
            BEGIN
                ALTER TABLE [Carttables] ADD [IsBulk] BIT NOT NULL DEFAULT 0;
            END
        ");

        // 2. Add MealType to DailyCalorieEntry if missing
        context.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DailyCalorieEntry]') AND name = N'MealType')
            BEGIN
                ALTER TABLE [DailyCalorieEntry] ADD [MealType] NVARCHAR(50) NOT NULL DEFAULT 'Other';
            END
        ");

        // 3. Add BulkItemId to OrderItems if missing
        context.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrderItems]') AND name = N'BulkItemId')
            BEGIN
                ALTER TABLE [OrderItems] ADD [BulkItemId] INT NULL;
            END
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database sync error: " + ex.Message);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Public}/{action=Index}/{id?}");

app.MapHub<AnalyticsHub>("/analyticsHub");

app.Run();
