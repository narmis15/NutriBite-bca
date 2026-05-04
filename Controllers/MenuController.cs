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
                 "Comfort Thali", "Rice Combo", "Salad Bowl", 
                 "Low Calorie", "Protein Meal" 
             };

            // 🔥 CLEANUP: Remove Jain Thali from database if it exists
            var jainCats = _context.AddCategories.Where(c => c.MealCategory.Contains("Jain Thali")).ToList();
            if (jainCats.Any())
            {
                var jainCatIds = jainCats.Select(c => c.Cid).ToList();
                var jainFoods = _context.Foods.Where(f => (f.CategoryId.HasValue && jainCatIds.Contains(f.CategoryId.Value)) || (f.MealCategoryId.HasValue && jainCatIds.Contains(f.MealCategoryId.Value))).ToList();
                _context.Foods.RemoveRange(jainFoods);
                _context.AddCategories.RemoveRange(jainCats);
                _context.SaveChanges();
            }

            // Define specific items for the requested categories
            var specialCategoryItems = new Dictionary<string, List<(string Name, string Image)>>
            {
                { "Standard Thali", new List<(string, string)> {
                    ("Classic Standard Thali", "/images/Meals/Standard_Thali.jpeg"),
                    ("Punjabi Chole Thali", "/images/Meals/standard1.jpeg"),
                    ("Gujarati Rajwadi Thali", "/images/Meals/standard2.jpeg"),
                    ("North Indian Thali", "/images/Meals/standard3.jpeg"),
                    ("Comfort Veg Thali", "/images/Meals/Standard_Thali.jpeg")
                }},
                { "Special Thali", new List<(string, string)> {
                    ("Royal Special Thali", "/images/Meals/Special_Thali.jpeg"),
                    ("Executive Special Thali", "/images/Meals/special1.jpeg"),
                    ("Gourmet Celebration Thali", "/images/Meals/special2.jpeg"),
                    ("NutriBite Signature Special", "/images/Meals/special3.jpeg"),
                    ("Grand Feast Thali", "/images/Meals/Special_Thali.jpeg")
                }},
                { "Comfort Thali", new List<(string, string)> {
                    ("Homestyle Dal Khichdi Thali", "/images/menu items/Dal khichdi.png"),
                    ("Soft Phulka & Moong Dal Thali", "/images/menu items/chaana and lauki dal with phulka.png"),
                    ("Curd Rice & Pomegranate Thali", "/images/menu items/curd rice with pomegranate.png"),
                    ("Mashed Aloo Baingan Thali", "/images/menu items/baigan bharta.png"),
                    ("Steamed Idli Sambar Thali", "/images/menu items/sambhar idli.png"),
                    ("Methi Thepla & Dahi Thali", "/images/menu items/methi thelpa with dahi.png")
                }},
                { "Rice Combo", new List<(string, string)> {
                    ("Aloo Matar Tehri", "/images/menu items/Tehri.png"),
                    ("Kadhi Chawal Special", "/images/menu items/kadhii chawal.png"),
                    ("Rajma Chawal Bowl", "/images/menu items/quick bowl rice.png"),
                    ("Veg Tawa Pulao", "/images/menu items/tawa pulao.png"),
                    ("Paneer Biryani Box", "/images/menu items/exam topper combo.png")
                }},
                { "Classical Thali", new List<(string, string)> {
                    ("Traditional Awadhi Thali", "/images/Meals/Classical_Thali.jpeg"),
                    ("Banarasi Satvik Thali", "/images/Meals/Standard_Thali.jpeg"),
                    ("Royal Rajwadi Thali", "/images/Meals/Special_Thali.jpeg"),
                    ("North Indian Feast", "/images/Meals/Deluxe_Thali.jpeg"),
                    ("Heritage Veg Platter", "/images/Meals/Jain_Thali.jpeg")
                }},
                { "Deluxe Thali", new List<(string, string)> {
                    ("Maharaja Premium Thali", "/images/Meals/Deluxe_Thali.jpeg"),
                    ("Executive Special Thali", "/images/menu items/executive lean thali.png"),
                    ("Gourmet Veg Celebration", "/images/Meals/Special_Thali.jpeg"),
                    ("NutriBite Signature Deluxe", "/images/Meals/Classical_Thali.jpeg"),
                    ("Royal Kitchen Selection", "/images/Meals/Standard_Thali.jpeg")
                }},
                { "Salad Bowl", new List<(string, string)> {
                    ("Fresh Garden Salad", "/images/menu items/Grilled tofu salad with sprouts.png"),
                    ("Mixed Greens Bowl", "/images/menu items/deep grain bowl.png"),
                    ("Nutritious Sprout Salad", "/images/menu items/mixed sprouts.png"),
                    ("High Fiber Veggie Bowl", "/images/Meals/salads.jpeg"),
                    ("Green Energy Bowl", "/images/Meals/low-calorie.jpg")
                }},
                { "Low Calorie", new List<(string, string)> {
                    ("Quinoa & Sprouts Bowl", "/images/Meals/low-calorie.jpg"),
                    ("Grilled Tofu Salad", "/images/menu items/Grilled tofu salad with sprouts.png"),
                    ("Deep Work Grain Bowl", "/images/menu items/deep grain bowl.png"),
                    ("Sprouted Moong Box", "/images/menu items/mixed sprouts.png"),
                    ("Lean Green Veggie Bowl", "/images/Meals/salads.jpeg")
                }},
                { "Protein Meal", new List<(string, string)> {
                    ("Paneer Bhurji with Phulka", "/images/menu items/paneer bhurji with 3 phulkas.png"),
                    ("Soya Chunk High Protein Curry", "/images/menu items/soya chunks curry.png"),
                    ("Black Chana Power Bowl", "/images/menu items/black channa with 2 rotis.png"),
                    ("Tandoori Soya Chaap Bowl", "/images/menu items/Tandoori Soya Chaap Bowl.png"),
                    ("Whole Wheat Pesto Paneer Wrap", "/images/menu items/pesto paneer wrap.png"),
                    ("Egg Protein Feast", "/images/menu items/egg thali.png")
                }}
            };

            foreach (var catName in categoryNames)
            {
                var cat = _context.AddCategories.FirstOrDefault(c => (c.MealCategory != null && c.MealCategory.ToLower() == catName.ToLower()) || (c.ProductCategory != null && c.ProductCategory.ToLower() == catName.ToLower()));
                if (cat == null)
                {
                    string mealPic = catName.Replace(" ", "_") + ".jpeg";
                    if (catName == "Salad Bowl") mealPic = "salads.jpeg"; // Specific fix for salad bowl image
                    
                    cat = new AddCategory { MealCategory = catName, ProductCategory = catName, ProductPic = mealPic, MealPic = mealPic, CreatedAt = DateTime.Now };
                    _context.AddCategories.Add(cat);
                    _context.SaveChanges();
                }
                else if (catName == "Salad Bowl")
                {
                    // Update existing Salad Bowl category image
                    cat.MealPic = "salads.jpeg";
                    cat.ProductPic = "salads.jpeg";
                    _context.AddCategories.Update(cat);
                    _context.SaveChanges();
                }

                var mealCat = _context.MealCategories.FirstOrDefault(m => m.MealCategoryName.ToLower() == catName.ToLower());
                if (mealCat == null)
                {
                    mealCat = new MealCategory { MealCategoryName = catName };
                    _context.MealCategories.Add(mealCat);
                    _context.SaveChanges();
                }

                if (specialCategoryItems.ContainsKey(catName))
                {
                    // For these categories, we want EXACTLY the specific items from our list
                    var items = specialCategoryItems[catName];
                    var allExistingInCategory = _context.Foods
                        .Where(f => f.MealCategoryId == mealCat.MealCategoryId || f.CategoryId == cat.Cid)
                        .ToList();
                    
                    // 1. First, mark EVERYTHING in this category as Inactive to start fresh
                    foreach (var f in allExistingInCategory)
                    {
                        f.Status = "Inactive";
                    }
                    _context.SaveChanges();

                    // 2. Now, update or add the 5-6 items we want and set them to Active
                    for (int i = 0; i < items.Count; i++)
                    {
                        var targetName = items[i].Name;
                        var targetImage = items[i].Image;

                        // Try to find an existing item to reuse (to avoid FK issues)
                        var existing = allExistingInCategory.ElementAtOrDefault(i);
                        
                        if (existing != null)
                        {
                            existing.Name = targetName;
                            existing.ImagePath = targetImage;
                            existing.Status = "Active";
                            existing.Price = 140 + (new Random().Next(5, 50));
                            existing.Description = $"Premium and healthy {targetName} prepared fresh for your goals.";
                            _context.Foods.Update(existing);
                        }
                        else
                        {
                            var newFood = new Food
                            {
                                Name = targetName,
                                Price = 140 + (new Random().Next(5, 50)),
                                Description = $"Premium and healthy {targetName} prepared fresh for your goals.",
                                MealCategoryId = mealCat.MealCategoryId,
                                CategoryId = cat.Cid,
                                VendorId = vendor.VendorId,
                                ImagePath = targetImage,
                                Calories = 350 + (new Random().Next(10, 150)),
                                PreparationTime = "25 mins",
                                Status = "Active",
                                CreatedAt = DateTime.Now,
                                Protein = 15 + new Random().Next(1, 10),
                                Carbs = 40 + new Random().Next(1, 20),
                                Fat = 10 + new Random().Next(1, 5)
                            };
                            _context.Foods.Add(newFood);
                        }
                    }

                    _context.SaveChanges();
                }
                else
                {
                    // Generic seeding for other categories if they have fewer than 10 items
                    int currentCount = _context.Foods.Count(f => f.MealCategoryId == mealCat.MealCategoryId || f.CategoryId == cat.Cid);
                    if (currentCount < 10)
                    {
                        var newFoods = new List<Food>();
                        for (int i = currentCount + 1; i <= 10; i++)
                        {
                            string foodName = $"{catName} Option {i}";
                            string imagePath = "/images/Meals/Standard_Thali.jpeg";
                            if (catName.ToLower().Contains("salad")) imagePath = "/images/Meals/salads.jpeg";
                            else if (catName.ToLower().Contains("rice")) imagePath = "/images/menu items/Tehri.png";

                            newFoods.Add(new Food
                            {
                                Name = foodName,
                                Price = 120 + (i * 5),
                                Description = $"Wholesome {catName} prepared with fresh ingredients.",
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
                .Where(f => ((f.CategoryId.HasValue && categoryIds.Contains(f.CategoryId.Value)) 
                         || (f.MealCategoryId.HasValue && categoryIds.Contains(f.MealCategoryId.Value))
                         || (f.ProductCategoryId.HasValue && categoryIds.Contains(f.ProductCategoryId.Value)))
                         && f.Status == "Active")
                .Take(15)
                .ToList();

            // Fetch vendor names for the foods
            var vendorIds = foods.Where(f => f.VendorId.HasValue).Select(f => f.VendorId.Value).Distinct().ToList();
            var vendors = _context.VendorSignups.Where(v => vendorIds.Contains(v.VendorId)).ToDictionary(v => v.VendorId, v => v.VendorName);
            ViewBag.Vendors = vendors;

            // 🥘 SPECIAL FIX: THALI CATEGORY
            if (categoryName.ToLower().Contains("thali"))
            {
                // Ensure only thali items are shown if the category is 'Thali'
                // We filter strictly for items that actually have 'Thali' in their name
                foods = foods.Where(f => 
                    f.Name.ToLower().Contains("thali") || 
                    (f.Description != null && f.Description.ToLower().Contains("thali"))
                ).ToList();
            }

            // 🍚 SPECIAL FIX: RICE CATEGORY
            if (categoryName.ToLower().Contains("rice"))
            {
                // Ensure only rice/khichdi/pulao items are shown
                foods = foods.Where(f => 
                    f.Name.ToLower().Contains("rice") || 
                    f.Name.ToLower().Contains("khichdi") || 
                    f.Name.ToLower().Contains("pulao") || 
                    f.Name.ToLower().Contains("biryani") || 
                    (f.Description != null && (f.Description.ToLower().Contains("rice") || f.Description.ToLower().Contains("khichdi")))
                ).ToList();
            }

            // � SPECIAL FIX: SALAD CATEGORY
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
