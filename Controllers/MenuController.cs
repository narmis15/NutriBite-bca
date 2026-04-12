using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NUTRIBITE.Models;
using System.Collections.Generic;
using System;

namespace NUTRIBITE.Controllers
{
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================
        // GET: /Menu
        // Show all meal categories
        // ============================
        public IActionResult Index()
        {
            var categories = _context.AddCategories
                .Where(c => c.MealCategory != null)
                .OrderBy(c => c.MealCategory)
                .ToList();

            return View(categories);
        }

        private void SeedCategoryData()
        {
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == "system@nutribite.com");
            if (vendor == null)
            {
                vendor = new VendorSignup { VendorName = "NutriBite Kitchen", Email = "system@nutribite.com", PasswordHash = "system_protected", IsApproved = true, CreatedAt = DateTime.Now };
                _context.VendorSignups.Add(vendor);
                _context.SaveChanges();
            }

            var categoryNames = new[] { 
                 "Standard Thali", "Special Thali", "Deluxe Thali", "Classical Thali", 
                 "Comfort Thali", "Jain Thali", "Rice Combo", "Salad Bowl", 
                 "Low Calorie", "Protein Meal" 
             };

            foreach (var catName in categoryNames)
             {
                 var cat = _context.AddCategories.FirstOrDefault(c => (c.MealCategory != null && c.MealCategory.ToLower() == catName.ToLower()) || (c.ProductCategory != null && c.ProductCategory.ToLower() == catName.ToLower()));
                 if (cat == null)
                 {
                     cat = new AddCategory { MealCategory = catName, ProductCategory = catName, ProductPic = catName.Replace(" ", "_") + ".jpeg", MealPic = catName.Replace(" ", "_") + ".jpeg", CreatedAt = DateTime.Now };
                     _context.AddCategories.Add(cat);
                     _context.SaveChanges();
                 }

                 // Also ensure it exists in MealCategory table to satisfy FK
                 var mealCat = _context.MealCategories.FirstOrDefault(m => m.MealCategoryName.ToLower() == catName.ToLower());
                 if (mealCat == null)
                 {
                     mealCat = new MealCategory { MealCategoryName = catName };
                     _context.MealCategories.Add(mealCat);
                     _context.SaveChanges();
                 }
 
                 int currentCount = _context.Foods.Count(f => f.MealCategoryId == mealCat.MealCategoryId || f.CategoryId == cat.Cid);
                 if (currentCount < 12)
                 {
                     var newFoods = new List<Food>();
                     for (int i = currentCount + 1; i <= 12; i++)
                     {
                         string foodName = $"{catName} Option {i}";
                         string imagePath = "/images/Meals/Standard_Thali.jpeg";
                         if (catName.ToLower().Contains("salad")) imagePath = "/images/Meals/salads.jpeg";
                         else if (catName.ToLower().Contains("rice")) imagePath = "/images/menu items/Tehri.png";
 
                         newFoods.Add(new Food
                         {
                             Name = foodName,
                             Price = 120 + (i * 5),
                             Description = $"Delicious and wholesome {catName} prepared with fresh ingredients.",
                             MealCategoryId = mealCat.MealCategoryId,
                             CategoryId = cat.Cid,
                             VendorId = vendor.VendorId,
                             ImagePath = imagePath,
                             Calories = 400 + (i * 10),
                             PreparationTime = "25 mins",
                             Status = "Active",
                             CreatedAt = DateTime.Now,
                             Protein = 15 + i,
                             Carbs = 40 + i,
                             Fat = 10 + i
                         });
                     }
                     _context.Foods.AddRange(newFoods);
                     _context.SaveChanges();
                 }
             }
        }

        // ============================
        // GET: /Menu/Category
        // Show foods inside category
        // ============================
        public IActionResult Category(string name)
        {
            if (string.IsNullOrEmpty(name))
                return NotFound();

            if (name.Equals("Salads", StringComparison.OrdinalIgnoreCase))
                name = "Salad Bowl";

            // Ensure categories and foods are seeded with 10-15 items
            SeedCategoryData();

            // Find all matching category records by name (to handle duplicates in AddCategory table)
            var categories = _context.AddCategories
                .Where(c => c.MealCategory.Trim().ToLower() == name.Trim().ToLower() || 
                            c.ProductCategory.Trim().ToLower() == name.Trim().ToLower())
                .ToList();

            // If no category found by name, check if it's a direct food name search
            if (!categories.Any())
            {
                var food = _context.Foods
                    .Include(f => f.Nutritionist)
                    .FirstOrDefault(f => f.Name.Trim().ToLower() == name.Trim().ToLower());

                if (food != null)
                {
                    ViewBag.CategoryName = food.Name;
                    return View("Category", new List<Food> { food });
                }

                return Content("Category or Food Not Found: " + name);
            }

            var categoryIds = categories.Select(c => c.Cid).ToList();
            var categoryName = categories.First().MealCategory ?? categories.First().ProductCategory;

            // 1. Primary Query: Find foods directly linked to these category IDs
            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Include(f => f.Recipe)
                .Where(f => (f.CategoryId.HasValue && categoryIds.Contains(f.CategoryId.Value)) 
                         || (f.MealCategoryId.HasValue && categoryIds.Contains(f.MealCategoryId.Value))
                         || (f.ProductCategoryId.HasValue && categoryIds.Contains(f.ProductCategoryId.Value)))
                .Take(15)
                .ToList();

            // Fetch vendor names for the foods
            var vendorIds = foods.Where(f => f.VendorId.HasValue).Select(f => f.VendorId.Value).Distinct().ToList();
            var vendors = _context.VendorSignups.Where(v => vendorIds.Contains(v.VendorId)).ToDictionary(v => v.VendorId, v => v.VendorName);
            ViewBag.Vendors = vendors;

            // 🥗 SPECIAL FIX: SALAD CATEGORY
            if (categoryName.ToLower().Contains("salad"))
            {
                // Ensure only salad items are shown if the category is 'Salad'
                // We'll filter strictly by name/description for this category
                foods = foods.Where(f => 
                    f.Name.ToLower().Contains("salad") || 
                    (f.Description != null && f.Description.ToLower().Contains("salad"))
                ).ToList();
            }

            // 💡 Ensure AI Recommendations work: If list is empty but it's a food name, return that food
            if (!foods.Any())
            {
                var singleFood = _context.Foods
                    .Include(f => f.Nutritionist)
                    .FirstOrDefault(f => f.Name.Trim().ToLower() == name.Trim().ToLower());
                
                if (singleFood != null)
                {
                    ViewBag.CategoryName = singleFood.Name;
                    return View("Category", new List<Food> { singleFood });
                }
            }

            // 2. Secondary Query: If still very few items, try name-based matching as a broad fallback
            if (foods.Count < 3)
            {
                var searchName = categoryName.Split(' ').First(); // e.g., "Classical"
                var extraFoods = _context.Foods
                    .Include(f => f.Nutritionist)
                    .Where(f => (f.Name.Contains(searchName) || (f.Description != null && f.Description.Contains(searchName)))
                             && !foods.Select(existing => existing.Id).Contains(f.Id))
                    .ToList();
                
                foods.AddRange(extraFoods);
            }

            // 3. Auto-seed demo data ONLY if absolutely NO items are found
            if (!foods.Any())
            {
                var systemVendor = _context.VendorSignups.FirstOrDefault(v => v.Email == "system@nutribite.com");
                if (systemVendor == null)
                {
                    systemVendor = new VendorSignup 
                    { 
                        VendorName = "NutriBite Kitchen", 
                        Email = "system@nutribite.com", 
                        PasswordHash = "system_protected", 
                        IsApproved = true, 
                        CreatedAt = DateTime.Now,
                        Phone = "0000000000",
                        Address = "NutriBite Headquarters"
                    };
                    _context.VendorSignups.Add(systemVendor);
                    _context.SaveChanges();
                }

                var demoFoods = new List<Food>();
                var firstCid = categoryIds.First();

                for (int i = 1; i <= 10; i++) // Create more than 3 for demo
                {
                    demoFoods.Add(new Food
                    {
                        Name = $"{categoryName} Special - Option {i}",
                        Description = $"A delicious and healthy {categoryName} prepared with fresh ingredients.",
                        Price = 120 + (i * 15),
                        Calories = 400 + (i * 40),
                        Protein = 15 + (i * 2),
                        Carbs = 50 + (i * 5),
                        Fat = 10 + i,
                        PreparationTime = "25 mins",
                        FoodType = "Veg",
                        ImagePath = $"/images/Meals/Standard_Thali.jpeg",
                        VendorId = systemVendor.VendorId,
                        Status = "Active",
                        CreatedAt = DateTime.Now,
                        IsVerified = true,
                        CategoryId = firstCid
                    });
                }

                _context.Foods.AddRange(demoFoods);
                _context.SaveChanges();
                foods = demoFoods;
            }

            ViewBag.CategoryName = categoryName;
            return View(foods);
        }

        public IActionResult Test()
        {
            return Content("Menu Controller Working");
        }
    }
}
