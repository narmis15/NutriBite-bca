using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;

using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Controllers
{
    public class ElderlyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ElderlyController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.Status == "Active" && f.FoodType == "Elderly")
                .Take(10)
                .ToList();

            // Auto-seed if less than 10 items
            if (foods.Count < 10)
            {
                var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == "system@nutribite.com");
                if (vendor == null)
                {
                    vendor = new VendorSignup { VendorName = "NutriBite Kitchen", Email = "system@nutribite.com", PasswordHash = "system_protected", IsApproved = true, CreatedAt = DateTime.Now };
                    _context.VendorSignups.Add(vendor);
                    _context.SaveChanges();
                }

                var elderlyMeals = new List<Food>
                {
                    new Food { Name = "Soft Moong Dal Khichdi", Price = 85, Calories = 420, PreparationTime = "20 mins", FoodType = "Elderly", ImagePath = "/images/menu items/Dal khichdi.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Oats & Moong Dal Cheela", Price = 80, Calories = 380, PreparationTime = "15 mins", FoodType = "Elderly", ImagePath = "/images/menu items/moong and oats chilla.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Lauki & Chana Dal with Phulka", Price = 90, Calories = 450, PreparationTime = "25 mins", FoodType = "Elderly", ImagePath = "/images/menu items/chaana and lauki dal with phulka.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Vegetable Dalia", Price = 75, Calories = 410, PreparationTime = "20 mins", FoodType = "Elderly", ImagePath = "/images/menu items/vegetable daliya.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Methi Soft Thepla with Dahi", Price = 80, Calories = 390, PreparationTime = "15 mins", FoodType = "Elderly", ImagePath = "/images/menu items/methi thelpa with dahi.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Curd Rice (Mild Tadka)", Price = 70, Calories = 370, PreparationTime = "10 mins", FoodType = "Elderly", ImagePath = "/images/menu items/curd rice with pomegranate.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Pumpkin Mash & Phulka", Price = 75, Calories = 400, PreparationTime = "20 mins", FoodType = "Elderly", ImagePath = "/images/menu items/pumpkin mash with phulkas.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Steamed Idli with Mild Sambar", Price = 85, Calories = 350, PreparationTime = "15 mins", FoodType = "Elderly", ImagePath = "/images/menu items/sambhar idli.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Mashed Aloo-Baingan Bharta", Price = 80, Calories = 430, PreparationTime = "20 mins", FoodType = "Elderly", ImagePath = "/images/menu items/baigan bharta.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Warm Apple & Cinnamon Stew", Price = 50, Calories = 240, PreparationTime = "15 mins", FoodType = "Elderly", ImagePath = "/images/menu items/cinnamon apple stew.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Carrot & Ginger Soup", Price = 65, Calories = 180, PreparationTime = "20 mins", FoodType = "Elderly", ImagePath = "/images/menu items/carrot soup.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Ragi Malt Porridge", Price = 55, Calories = 220, PreparationTime = "10 mins", FoodType = "Elderly", ImagePath = "/images/menu items/jowar upma.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now }
                };

                foreach (var meal in elderlyMeals)
                {
                    if (!_context.Foods.Any(f => f.Name == meal.Name && f.FoodType == "Elderly"))
                    {
                        _context.Foods.Add(meal);
                    }
                }
                _context.SaveChanges();
                
                foods = _context.Foods
                    .Include(f => f.Nutritionist)
                    .Where(f => f.Status == "Active" && f.FoodType == "Elderly")
                    .Take(10)
                    .ToList();
            }
            // Force update images just in case they were seeded wrongly
            // Force update images to v2 to bypass browser cache
            var updates = new Dictionary<string, string> {
                { "Oats & Moong Dal Cheela", "/images/menu items/moong_cheela_v2.png" },
                { "Lauki & Chana Dal with Phulka", "/images/menu items/lauki_dal_v2.png" },
                { "Vegetable Dalia", "/images/menu items/dalia_v2.png" },
                { "Methi Soft Thepla with Dahi", "/images/menu items/thepla_v2.png" },
                { "Pumpkin Mash & Phulka", "/images/menu items/pumpkin_v2.png" },
                { "Steamed Idli with Mild Sambar", "/images/menu items/idli_v2.png" },
                { "Carrot & Ginger Soup", "/images/menu items/carrot soup.png" },
                { "Ragi Malt Porridge", "/images/menu items/ragi porridge.png" }
            };

            bool changed = false;
            foreach (var kvp in updates) {
                var item = _context.Foods.FirstOrDefault(f => f.Name == kvp.Key && f.FoodType == "Elderly");
                if (item != null && item.ImagePath != kvp.Value) {
                    item.ImagePath = kvp.Value;
                    changed = true;
                }
            }
            if (changed) _context.SaveChanges();

            return View(foods);
        }
    }
}
