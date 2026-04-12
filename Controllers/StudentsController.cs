using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;

using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.Status == "Active" && f.FoodType == "Student")
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

                var studentMeals = new List<Food>
                {
                    new Food { Name = "Exam Topper Combo", Price = 119, Calories = 630, PreparationTime = "25 mins", FoodType = "Student", ImagePath = "/images/menu items/exam topper combo.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Quick Bite Rice Bowl", Price = 89, Calories = 450, PreparationTime = "15 mins", FoodType = "Student", ImagePath = "/images/menu items/quick bowl rice.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Pocket Friendly Thali", Price = 75, Calories = 510, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/pocket friendly thali.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Protein-Packed Soya Chunk Curry", Price = 95, Calories = 520, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/soya chunks curry.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Paneer Bhurji with 3 Phulkas", Price = 119, Calories = 610, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/paneer bhurji with 3 phulkas.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Egg Thali", Price = 109, Calories = 580, PreparationTime = "25 mins", FoodType = "Student", ImagePath = "/images/menu items/egg thali.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Late Night Khichdi Bowl", Price = 69, Calories = 420, PreparationTime = "15 mins", FoodType = "Student", ImagePath = "/images/menu items/Dal khichdi.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Curd Rice with Pomegranate", Price = 65, Calories = 390, PreparationTime = "10 mins", FoodType = "Student", ImagePath = "/images/menu items/curd rice with pomegranate.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Veg Tawa Pulav", Price = 79, Calories = 470, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/tawa pulao.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Kadhi Chawal", Price = 85, Calories = 510, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/kadhii chawal.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Aloo Paratha + Curd", Price = 90, Calories = 600, PreparationTime = "20 mins", FoodType = "Student", ImagePath = "/images/menu items/aloo paratha with curd.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Sprouted Moong Salad Box", Price = 50, Calories = 280, PreparationTime = "10 mins", FoodType = "Student", ImagePath = "/images/menu items/mixed sprouts.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now },
                    new Food { Name = "Flash Breakfast Smoothie", Price = 65, Calories = 350, PreparationTime = "10 mins", FoodType = "Student", ImagePath = "/images/menu items/fresh smoothie.png", VendorId = vendor.VendorId, Status = "Active", CreatedAt = DateTime.Now }
                };

                foreach (var meal in studentMeals)
                {
                    if (!_context.Foods.Any(f => f.Name == meal.Name && f.FoodType == "Student"))
                    {
                        _context.Foods.Add(meal);
                    }
                }
                _context.SaveChanges();
                
                foods = _context.Foods
                    .Include(f => f.Nutritionist)
                    .Where(f => f.Status == "Active" && f.FoodType == "Student")
                    .Take(10)
                    .ToList();
            }

            return View(foods);
        }
    }
}