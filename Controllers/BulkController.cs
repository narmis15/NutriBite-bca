using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;
using System.Collections.Generic;
using System;

namespace NUTRIBITE.Controllers
{
    public class BulkController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BulkController(ApplicationDbContext context)
        {
            _context = context;
            SeedBulkItems();
        }

        private void SeedBulkItems()
        {
            // Always ensure junk food is removed from BulkItems
            var junkKeywords = new[] { "Burger", "Pizza", "Fries", "Samosa", "Cake", "Truffle", "Coffee", "Wings", "Nuggets", "Chips", "Chakli", "Wafers", "Cutlet" };
            var junkItems = _context.BulkItems.AsEnumerable().Where(b => junkKeywords.Any(k => b.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) || (b.Description != null && junkKeywords.Any(k => b.Description.Contains(k, StringComparison.OrdinalIgnoreCase)))).ToList();
            if (junkItems.Any())
            {
                _context.BulkItems.RemoveRange(junkItems);
                _context.SaveChanges();
            }

            if (_context.BulkItems.Count() < 10)
            {
                var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == "system@nutribite.com");
                if (vendor == null)
                {
                    vendor = new VendorSignup { VendorName = "NutriBite Kitchen", Email = "system@nutribite.com", PasswordHash = "system_protected", IsApproved = true, CreatedAt = DateTime.Now };
                    _context.VendorSignups.Add(vendor);
                    _context.SaveChanges();
                }

                var items = new List<BulkItem>
                {
                    // === MEALS (10+ Items) ===
                    new BulkItem { Name = "Office Thali Pack", Category = "Meals", Description = "Complete North Indian thali with 2 sabzi, dal, rice, and 4 rotis.", Price = 180, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/bulk-meal1.webp", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Executive Health Meal", Category = "Meals", Description = "Premium meal box with paneer, dal makhani, pulao and healthy fruit salad.", Price = 250, IsVeg = true, MOQ = 5, ImagePath = "/images/bulk/bulk-meal1.webp", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Chicken Curry Combo Bulk", Category = "Meals", Description = "Home-style chicken curry with steamed brown rice and salad.", Price = 220, IsVeg = false, MOQ = 10, ImagePath = "/images/bulk/bulk-meal1.webp", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Grilled Paneer Platter", Category = "Meals", Description = "Herb-marinated grilled paneer served with sautéed vegetables and quinoa.", Price = 210, IsVeg = true, MOQ = 8, ImagePath = "/images/bulk/m1.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Lentil & Spinach Stew", Category = "Meals", Description = "Wholesome red lentil stew with fresh spinach and multi-grain bread.", Price = 160, IsVeg = true, MOQ = 12, ImagePath = "/images/bulk/m2.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Brown Rice & Dal Tadka", Category = "Meals", Description = "Simple and healthy yellow dal tadka with fiber-rich brown rice.", Price = 140, IsVeg = true, MOQ = 15, ImagePath = "/images/bulk/m3.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Fish Fillet with Greens", Category = "Meals", Description = "Steamed fish fillet seasoned with herbs, served with steamed broccoli.", Price = 280, IsVeg = false, MOQ = 5, ImagePath = "/images/bulk/m4.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Veggie Loaded Quinoa", Category = "Meals", Description = "Quinoa cooked with seasonal vegetables and light olive oil.", Price = 190, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/m5.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "High Protein Chickpea Curry", Category = "Meals", Description = "Spicy and nutritious chickpea curry served with 2 phulkas.", Price = 150, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/m6.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Roasted Tofu Salad Bowl", Category = "Meals", Description = "Crispy roasted tofu with mixed greens and a lemon-tahini dressing.", Price = 175, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/m7.jpg", Status = "Active", VendorId = vendor.VendorId },

                    // === SNACKS (10+ Items) ===
                    new BulkItem { Name = "Hara Bhara Kabab Platter", Category = "Snacks", Description = "Nutritious spinach and pea kababs served with mint chutney.", Price = 450, IsVeg = true, MOQ = 1, ImagePath = "/images/bulk/harabharakabab.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Whole Wheat Sandwich Box", Category = "Snacks", Description = "Mix of brown bread veg club and corn-spinach sandwiches.", Price = 600, IsVeg = true, MOQ = 1, ImagePath = "/images/bulk/Brownbreadmayosandwich.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Grilled Chicken Strips", Category = "Snacks", Description = "Tender grilled chicken strips with healthy herb dip.", Price = 850, IsVeg = false, MOQ = 1, ImagePath = "/images/bulk/bulk-meal2.webp", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Roasted Makhana Tub", Category = "Snacks", Description = "Crispy fox nuts seasoned with pink salt and black pepper.", Price = 120, IsVeg = true, MOQ = 5, ImagePath = "/images/bulk/s1.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Sprouted Moong Salad", Category = "Snacks", Description = "Fresh sprouted moong beans with onion, tomato, and lemon.", Price = 90, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/s2.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Baked Beetroot Crisps", Category = "Snacks", Description = "Thinly sliced beetroots baked to perfection without oil.", Price = 150, IsVeg = true, MOQ = 5, ImagePath = "/images/bulk/s3.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Fruit Skewers Platter", Category = "Snacks", Description = "Colorful assortment of seasonal fruits on skewers.", Price = 300, IsVeg = true, MOQ = 2, ImagePath = "/images/bulk/s4.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Oats & Honey Granola", Category = "Snacks", Description = "Homemade granola with oats, nuts, and a touch of honey.", Price = 250, IsVeg = true, MOQ = 3, ImagePath = "/images/bulk/s5.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Steamed Corn Cup", Category = "Snacks", Description = "Sweet corn kernels steamed and lightly seasoned with herbs.", Price = 80, IsVeg = true, MOQ = 20, ImagePath = "/images/bulk/s6.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Hummus & Carrot Sticks", Category = "Snacks", Description = "Creamy chickpea hummus served with fresh carrot batons.", Price = 180, IsVeg = true, MOQ = 5, ImagePath = "/images/bulk/s7.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Baked Multigrain Sticks", Category = "Snacks", Description = "Crispy sticks made from 5 grains, served with salsa.", Price = 160, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/default.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Masala Roasted Peanuts", Category = "Snacks", Description = "High protein peanuts roasted with home-made spices.", Price = 110, IsVeg = true, MOQ = 15, ImagePath = "/images/bulk/default.jpg", Status = "Active", VendorId = vendor.VendorId },

                    // === FOODBOX PREDEFINED ===
                    new BulkItem { Name = "Nutri-Celebration Box", Category = "FoodBox_Predefined", Description = "Includes whole wheat paneer wrap, fruit bowl, fresh juice, and roasted makhana.", Price = 350, IsVeg = true, MOQ = 15, ImagePath = "/images/bulk/fb1.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Executive Seminar Box", Category = "FoodBox_Predefined", Description = "Healthy quinoa wrap, fruit bowl, and roasted nuts.", Price = 280, IsVeg = true, MOQ = 20, ImagePath = "/images/bulk/fb2.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Fitness Enthusiast Box", Category = "FoodBox_Predefined", Description = "Boiled eggs/tofu, sweet potato mash, and green tea.", Price = 310, IsVeg = true, MOQ = 10, ImagePath = "/images/bulk/fb1.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Healthy Refreshment Box", Category = "FoodBox_Predefined", Description = "Includes fruit bowl, roasted makhana, sprout salad, and fresh coconut water.", Price = 240, IsVeg = true, MOQ = 15, ImagePath = "/images/bulk/fb1.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Corporate Wellness Box", Category = "FoodBox_Predefined", Description = "Multi-grain sandwich, Greek yogurt, mixed seeds, and cold-pressed juice.", Price = 290, IsVeg = true, MOQ = 20, ImagePath = "/images/bulk/fb2.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Student Energy Box", Category = "FoodBox_Predefined", Description = "Peanut butter banana wrap, trail mix, and electrolyte water.", Price = 190, IsVeg = true, MOQ = 25, ImagePath = "/images/bulk/fb1.jpg", Status = "Active", VendorId = vendor.VendorId },

                    // === CUSTOM ITEMS ===
                    new BulkItem { Name = "Fresh Cold-Pressed Orange Juice", Category = "FoodBox_Custom", SubCategory = "Beverages", Price = 80, IsVeg = true, ImagePath = "/images/menu items/fresh smoothie.png", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Tender Coconut Water", Category = "FoodBox_Custom", SubCategory = "Beverages", Price = 80, IsVeg = true, ImagePath = "/images/menu items/fresh smoothie.png", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Spiced Buttermilk", Category = "FoodBox_Custom", SubCategory = "Beverages", Price = 40, IsVeg = true, ImagePath = "/images/menu items/fresh smoothie.png", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Green Detox Smoothie", Category = "FoodBox_Custom", SubCategory = "Beverages", Price = 110, IsVeg = true, ImagePath = "/images/menu items/fresh smoothie.png", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Roasted Makhana Pack", Category = "FoodBox_Custom", SubCategory = "Snacks", Price = 70, IsVeg = true, ImagePath = "/images/bulk/s1.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "High-Protein Mixed Nuts", Category = "FoodBox_Custom", SubCategory = "Snacks", Price = 130, IsVeg = true, ImagePath = "/images/bulk/s1.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Date & Seed Energy Bar", Category = "FoodBox_Custom", SubCategory = "Snacks", Price = 95, IsVeg = true, ImagePath = "/images/bulk/s5.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Oats & Chia Pudding", Category = "FoodBox_Custom", SubCategory = "Desserts", Price = 120, IsVeg = true, ImagePath = "/images/menu items/moong and oats chilla.png", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Fresh Seasonal Fruit Bowl", Category = "FoodBox_Custom", SubCategory = "Desserts", Price = 110, IsVeg = true, ImagePath = "/images/bulk/s4.jfif", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Avocado & Hummus Sandwich", Category = "FoodBox_Custom", SubCategory = "Mains", Price = 110, IsVeg = true, ImagePath = "/images/bulk/Brownbreadmayosandwich.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Quinoa Veggie Mix Bowl", Category = "FoodBox_Custom", SubCategory = "Mains", Price = 180, IsVeg = true, ImagePath = "/images/bulk/m5.jpg", Status = "Active", VendorId = vendor.VendorId },
                    new BulkItem { Name = "Whole-Wheat Paneer Tikka Wrap", Category = "FoodBox_Custom", SubCategory = "Mains", Price = 160, IsVeg = true, ImagePath = "/images/menu items/pesto paneer wrap.png", Status = "Active", VendorId = vendor.VendorId }
                };

                foreach (var item in items)
                {
                    if (!_context.BulkItems.Any(b => b.Name == item.Name))
                    {
                        _context.BulkItems.Add(item);
                    }
                }
                _context.SaveChanges();
            }
        }

        // GET: /Bulk
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Bulk/Meals
        [HttpGet]
        public IActionResult Meals()
        {
            var list = _context.BulkItems
                .Where(b => b.Status == "Active" && b.Category == "Meals")
                .OrderBy(b => b.Price).ThenBy(b => b.Name)
                .Select(b => new {
                    b.Id,
                    b.Name,
                    Description =b.Description,       
                    b.Price,
                    b.IsVeg,
                    b.ImagePath,
                    b.MOQ 
                })
                .ToList();

            ViewBag.Items = list;
            ViewBag.ActiveCategory = "Meals";
            return View("Meals");
        }

        // GET: /Bulk/Snacks
        [HttpGet]
        public IActionResult Snacks()
        {
            var list = _context.BulkItems
                .Where(b => b.Status == "Active" && b.Category == "Snacks")
                .OrderBy(b => b.Price).ThenBy(b => b.Name)
                .Select(b => new {
                    b.Id,
                    b.Name,
                    Description = b.Description,
                    b.Price,
                    b.IsVeg,
                    b.ImagePath,
                    b.MOQ
                })
                .ToList();

            ViewBag.Items = list;
            ViewBag.ActiveCategory = "Snacks";
            return View("Snacks");
        }

        // GET: /Bulk/FoodBox
        [HttpGet]
        public IActionResult FoodBox()
        {
            // Force update predefined food box images
            var predefinedItems = _context.BulkItems.Where(x => x.Category == "FoodBox_Predefined").ToList();
            bool changed = false;
            foreach (var item in predefinedItems) {
                if (item.Name == "Nutri-Celebration Box" && item.ImagePath != "/images/bulk/foodbox_nutricelebration.png") { item.ImagePath = "/images/bulk/foodbox_nutricelebration.png"; changed = true; }
                else if (item.Name == "Executive Seminar Box" && item.ImagePath != "/images/bulk/foodbox_executive.png") { item.ImagePath = "/images/bulk/foodbox_executive.png"; changed = true; }
                else if (item.Name == "Fitness Enthusiast Box" && item.ImagePath != "/images/bulk/foodbox_fitness.png") { item.ImagePath = "/images/bulk/foodbox_fitness.png"; changed = true; }
                else if (item.Name == "Healthy Refreshment Box" && item.ImagePath != "/images/bulk/foodbox_refreshment.png") { item.ImagePath = "/images/bulk/foodbox_refreshment.png"; changed = true; }
                else if (item.Name == "Corporate Wellness Box" && item.ImagePath != "/images/bulk/foodbox_corporate.png") { item.ImagePath = "/images/bulk/foodbox_corporate.png"; changed = true; }
                else if (item.Name == "Student Energy Box" && item.ImagePath != "/images/bulk/foodbox_student.png") { item.ImagePath = "/images/bulk/foodbox_student.png"; changed = true; }
            }
            if (changed) _context.SaveChanges();

            // Force update Custom Food Box images
            var customUpdates = new Dictionary<string, string> {
                { "Fresh Cold-Pressed Orange Juice", "/images/bulk/beverage.png" },
                { "Tender Coconut Water", "/images/bulk/beverage.png" },
                { "Spiced Buttermilk", "/images/bulk/beverage.png" },
                { "Green Detox Smoothie", "/images/bulk/beverage.png" },
                { "Roasted Makhana Pack", "/images/bulk/snack_nuts.jpg" },
                { "High-Protein Mixed Nuts", "/images/bulk/snack_nuts.jpg" },
                { "Date & Seed Energy Bar", "/images/bulk/snack_nuts.jpg" },
                { "Oats & Chia Pudding", "/images/bulk/dessert_oats.png" },
                { "Fresh Seasonal Fruit Bowl", "/images/bulk/dessert_fruit.jpg" },
                { "Avocado & Hummus Sandwich", "/images/bulk/main_sandwich.jpg" },
                { "Quinoa Veggie Mix Bowl", "/images/bulk/main_bowl.jpg" },
                { "Whole-Wheat Paneer Tikka Wrap", "/images/bulk/main_wrap.png" }
            };

            bool customChanged = false;
            foreach (var kvp in customUpdates) {
                var item = _context.BulkItems.FirstOrDefault(x => x.Name == kvp.Key && x.Category == "FoodBox_Custom");
                if (item != null && item.ImagePath != kvp.Value) {
                    item.ImagePath = kvp.Value;
                    customChanged = true;
                }
            }
            if (customChanged) _context.SaveChanges();

            // Predefined Food Boxes
            var predefined = _context.BulkItems
                .Where(x => x.Status == "Active" && x.Category == "FoodBox_Predefined")
                .OrderBy(x => x.Price).ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    Description = x.Description,
                    x.Price,
                    x.IsVeg,
                    ImagePath = x.ImagePath ?? "/images/bulk/default.jpg",
                    MOQ = x.MOQ ?? 0
                })
                .ToList();


            // Custom FoodBox items
            var custom = _context.BulkItems
                .Where(x => x.Status == "Active" && x.Category == "FoodBox_Custom")
                .OrderBy(x => x.SubCategory).ThenBy(x => x.Price).ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.SubCategory,
                    x.Price,
                    x.IsVeg,
                    ImagePath = x.ImagePath ?? "/images/bulk/default.jpg"
                })
                .ToList();


            // Group custom items by category (Beverages, Cakes etc)
            var grouped = custom
                .GroupBy(x => x.SubCategory)
                .ToList();


            ViewBag.Predefined = predefined;
            ViewBag.CustomGroups = grouped;

            return View("FoodBox");
        }
    }
}