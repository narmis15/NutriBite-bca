using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;

using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Controllers
{
    public class CorporateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CorporateController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.Status == "Active" && f.FoodType == "Corporate")
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

                var corporateMeals = new List<Food>
                {
                    new Food { Name = "Deep Work Grain Bowl", Price = 149, Calories = 480, PreparationTime = "20 mins", FoodType = "Corporate", ImagePath = "/images/menu items/deep grain bowl.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Paneer Tikka Wrap (Whole Wheat)", Price = 129, Calories = 420, PreparationTime = "15 mins", FoodType = "Corporate", ImagePath = "/images/menu items/paneer tikka wrap.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Executive Lean Thali", Price = 159, Calories = 500, PreparationTime = "30 mins", FoodType = "Corporate", ImagePath = "/images/menu items/executive lean thali.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Grilled Tofu & Sprout Salad", Price = 139, Calories = 350, PreparationTime = "15 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Grilled tofu salad with sprouts.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Methi-Matar Malai (Low Cream)", Price = 149, Calories = 470, PreparationTime = "25 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Methi-Matar Malai.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Black Chana & Barley Salad", Price = 139, Calories = 430, PreparationTime = "15 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Black Chana & Barley Salad.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Whole Wheat Pesto Paneer Wrap", Price = 135, Calories = 410, PreparationTime = "15 mins", FoodType = "Corporate", ImagePath = "/images/menu items/pesto paneer wrap.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Tandoori Soya Chaap Bowl", Price = 149, Calories = 460, PreparationTime = "20 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Tandoori Soya Chaap Bowl.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Work-Day Dal Khichdi Bowl", Price = 89, Calories = 480, PreparationTime = "20 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Dal khichdi.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Aloo-Matar Tehri", Price = 85, Calories = 520, PreparationTime = "25 mins", FoodType = "Corporate", ImagePath = "/images/menu items/Tehri.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Black Chana Curry & Rotis", Price = 95, Calories = 450, PreparationTime = "25 mins", FoodType = "Corporate", ImagePath = "/images/menu items/black channa with 2 rotis.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Mixed Veg Jowar Upma", Price = 79, Calories = 390, PreparationTime = "15 mins", FoodType = "Corporate", ImagePath = "/images/menu items/jowar upma.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now }
                };

                foreach (var meal in corporateMeals)
                {
                    if (!_context.Foods.Any(f => f.Name == meal.Name && f.FoodType == "Corporate"))
                    {
                        _context.Foods.Add(meal);
                    }
                }
                _context.SaveChanges();
                
                foods = _context.Foods
                    .Include(f => f.Nutritionist)
                    .Where(f => f.Status == "Active" && f.FoodType == "Corporate")
                    .Take(10)
                    .ToList();
            }

            return View(foods);
        }
    }
}
