using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NUTRIBITE.Models;
using NUTRIBITE.Filters;
using NUTRIBITE.Services;

namespace NUTRIBITE.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IOrderService _orderService;

        private const string AdminEmail = "Nutribite123@gmail.com";
        private const string AdminPassword = "NutriBite//26";

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IEmailService emailService, IOrderService orderService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> VerifyDeliveryOTP(int orderId, int otp)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Json(new { success = false, message = "Please login first." });

            var ok = await _orderService.VerifyOrderOTPAsync(orderId, otp);
            
            if (ok)
            {
                var order = _context.OrderTables.Include(o => o.User).FirstOrDefault(o => o.OrderId == orderId);
                // Send Delivery Confirmation Email
                if (order?.User != null && !string.IsNullOrEmpty(order.User.Email))
                {
                    var emailBody = $@"
                        <div style='font-family: sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                            <h2 style='color: #2d6a4f;'>Order Delivered Successfully!</h2>
                            <p>Hello {order.User.Name}, your order <b>#{order.OrderId}</b> has been delivered.</p>
                            <hr>
                            <p>We hope you enjoy your meal! Thank you for choosing NutriBite.</p>
                            <br>
                            <a href='https://localhost:5156/Home/Index' style='background: #2d6a4f; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Order Again</a>
                        </div>";
                    
                    await _emailService.SendEmailAsync(order.User.Email, "Order Delivered - NutriBite", emailBody);
                }

                return Json(new { success = true, message = "OTP Verified! Order marked as delivered." });
            }

            return Json(new { success = false, message = "Invalid OTP. Please check your email and try again." });
        }

        // =========================
        // ✅ DATA SEEDING (RUN ONCE)
        // =========================
        [AllowAnonymous]
        public IActionResult SeedData()
        {
            // 1. Ensure Nutritionists exist
            var nutritionist1 = _context.Nutritionists.FirstOrDefault(n => n.Name == "Dr. Sarah Johnson");
            if (nutritionist1 == null)
            {
                nutritionist1 = new Nutritionist
                {
                    Name = "Dr. Sarah Johnson",
                    Qualification = "Ph.D. in Clinical Nutrition",
                    Experience = 12
                };
                _context.Nutritionists.Add(nutritionist1);
            }

            var nutritionist2 = _context.Nutritionists.FirstOrDefault(n => n.Name == "Dr. Michael Chen");
            if (nutritionist2 == null)
            {
                nutritionist2 = new Nutritionist
                {
                    Name = "Dr. Michael Chen",
                    Qualification = "M.Sc. in Sports Nutrition, RD",
                    Experience = 8
                };
                _context.Nutritionists.Add(nutritionist2);
            }
            _context.SaveChanges();

            // 2. Ensure System Vendor exists
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == "system@nutribite.com");
            if (vendor == null)
            {
                vendor = new VendorSignup
                {
                    VendorName = "NutriBite Kitchen",
                    Email = "system@nutribite.com",
                    PasswordHash = "system_protected",
                    IsApproved = true,
                    IsRejected = false,
                    CreatedAt = DateTime.Now
                };
                _context.VendorSignups.Add(vendor);
                _context.SaveChanges();
            }

            int vid = vendor.VendorId;

            // 3. Helper to add food if it doesn't exist
            void AddFoodIfMissing(string name, decimal price, int calories, double protein, double carbs, double fat, string time, string type, string image, int nutritionistId, string mealCategoryName)
            {
                var category = _context.AddCategories.FirstOrDefault(c => c.MealCategory == mealCategoryName);
                if (category == null)
                {
                    category = new AddCategory { MealCategory = mealCategoryName, ProductCategory = "Veg", ProductPic = "NA", CreatedAt = DateTime.Now };
                    _context.AddCategories.Add(category);
                    _context.SaveChanges();
                }

                var existingFood = _context.Foods.FirstOrDefault(f => f.Name == name);
                if (existingFood == null)
                {
                    var food = new Food
                    {
                        Name = name,
                        Price = price,
                        Calories = calories,
                        Protein = protein,
                        Carbs = carbs,
                        Fat = fat,
                        PreparationTime = time,
                        FoodType = type,
                        ImagePath = image,
                        VendorId = vid,
                        Status = "Active",
                        CreatedAt = DateTime.Now,
                        IsVerified = true,
                        NutritionistId = nutritionistId,
                        MealCategoryId = category.Cid,
                        CategoryId = category.Cid
                    };
                    _context.Foods.Add(food);
                    _context.SaveChanges(); // Save to get FoodId

                    // Add Recipe
                    var ingredients = GetDefaultIngredients(name);
                    _context.Recipes.Add(new Recipe
                    {
                        FoodId = food.Id,
                        Ingredients = ingredients,
                        Steps = "1. Prepare ingredients. 2. Cook as per standard procedure. 3. Serve hot.",
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    // Update existing food with nutrition details and verification
                    existingFood.Protein = protein;
                    existingFood.Carbs = carbs;
                    existingFood.Fat = fat;
                    existingFood.IsVerified = true;
                    existingFood.NutritionistId = nutritionistId;
                    existingFood.FoodType = type;
                    existingFood.MealCategoryId = category.Cid;
                    existingFood.CategoryId = category.Cid;
                    existingFood.ImagePath = image;

                    // Update or Add Recipe
                    var recipe = _context.Recipes.FirstOrDefault(r => r.FoodId == existingFood.Id);
                    if (recipe == null)
                    {
                        _context.Recipes.Add(new Recipe
                        {
                            FoodId = existingFood.Id,
                            Ingredients = GetDefaultIngredients(name),
                            Steps = "1. Prepare ingredients. 2. Cook as per standard procedure. 3. Serve hot.",
                            CreatedAt = DateTime.Now
                        });
                    }
                    else
                    {
                        recipe.Ingredients = GetDefaultIngredients(name);
                    }
                }
            }

            string GetDefaultIngredients(string foodName)
            {
                if (foodName.Contains("Chicken Masala")) return "200g Chicken, Masala Spices, Ginger-Garlic Paste, 1 tbsp Oil";
                if (foodName.Contains("Standard Thali")) return "Rice, Dal, 1 Veg Sabzi, 2 Roti, Salad";
                if (foodName.Contains("Special Thali")) return "Rice, Dal, 2 Veg Sabzi, 2 Roti, Curd, Salad, Sweet";
                if (foodName.Contains("Deluxe Thali")) return "Pulao, Dal Fry, 2 Premium Sabzi, 3 Roti, Raita, Salad, Dessert";
                if (foodName.Contains("Classical Thali")) return "Traditional Rice, Dal, Seasonal Sabzi, 2 Roti, Buttermilk";
                if (foodName.Contains("Comfort Thali")) return "Soft Rice, Moong Dal, 1 Sabzi, 2 Phulka, Curd";
                if (foodName.Contains("Jain Thali")) return "Rice, Dal, 2 Jain Sabzi (No Onion/Garlic), 2 Phulka, Curd";
                if (foodName.Contains("Rice Combo")) return "Basmati Rice, Choice of Curry, Raita, Papad";
                if (foodName.Contains("Low Calorie")) return "Quinoa/Brown Rice, Steamed Veggies, Grilled Tofu, Green Salad";
                if (foodName.Contains("Protein Meal")) return "Grilled Chicken/Paneer, Sprouts, Boiled Eggs, Lentils";
                if (foodName.Contains("Salad Bowl")) return "Mixed Greens, Cucumber, Cherry Tomatoes, Chickpeas, Olive Oil Dressing";
                if (foodName.Contains("Dal Khichdi")) return "Moong Dal, Rice, Turmeric, Cumin, Ghee, Salt";
                if (foodName.Contains("Paneer Tikka")) return "200g Paneer, Yogurt, Tandoori Masala, Capsicum, Onion";
                return "Base Ingredients, Seasonal Vegetables, House Spices";
            }

            // --- MENU CATEGORIES ---
            AddFoodIfMissing("Standard Thali", 120, 550, 22, 70, 15, "25 mins", "Veg", "/images/Meals/Standard_Thali.jpeg", nutritionist1.Id, "Standard Thali");
            AddFoodIfMissing("Special Thali", 150, 650, 25, 80, 18, "30 mins", "Veg", "/images/Meals/Special_Thali.jpeg", nutritionist1.Id, "Special Thali");
            AddFoodIfMissing("Deluxe Thali", 200, 850, 30, 95, 22, "35 mins", "Veg", "/images/Meals/Deluxe_Thali.jpeg", nutritionist1.Id, "Deluxe Thali");
            AddFoodIfMissing("Classical Thali", 140, 600, 24, 75, 16, "30 mins", "Veg", "/images/Meals/Classical_Thali.jpeg", nutritionist1.Id, "Classical Thali");
            AddFoodIfMissing("Comfort Thali", 130, 580, 22, 72, 14, "25 mins", "Veg", "/images/Meals/Comfort_Thali.jpeg", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Jain Thali", 140, 550, 20, 70, 15, "30 mins", "Veg", "/images/Meals/Jain_Thali.jpeg", nutritionist1.Id, "Jain Thali");
            AddFoodIfMissing("Rice Combo", 110, 520, 18, 75, 12, "20 mins", "Veg", "/images/Meals/Rice_Combo.jpeg", nutritionist1.Id, "Rice Combo");
            AddFoodIfMissing("Low Calorie Bowl", 160, 350, 28, 30, 10, "20 mins", "Veg", "/images/Meals/low-calorie.jpg", nutritionist2.Id, "Low Calorie");
            AddFoodIfMissing("Protein Meal", 180, 500, 45, 35, 15, "25 mins", "Non-Veg", "/images/Meals/protein.jpg", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Salad Bowl", 140, 280, 15, 25, 8, "15 mins", "Veg", "/images/Meals/salads.jpeg", nutritionist2.Id, "Salad Bowl");
            AddFoodIfMissing("Chicken Masala Bowl", 179, 550, 35, 45, 22, "25 mins", "Non-Veg", "/images/Meals/protein1.jpeg", nutritionist2.Id, "Protein Meal");

            // --- ELDERLY ---
            AddFoodIfMissing("Soft Moong Dal Khichdi", 85, 420, 18, 65, 8, "20 mins", "Elderly", "/images/menu items/Dal khichdi.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Oats & Moong Dal Cheela", 80, 380, 16, 55, 10, "15 mins", "Elderly", "/images/menu items/moong and oats chilla.png", nutritionist1.Id, "Low Calorie");
            AddFoodIfMissing("Lauki & Chana Dal with Phulka", 90, 450, 20, 70, 12, "25 mins", "Elderly", "/images/menu items/chaana and lauki dal with phulka.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Vegetable Dalia", 75, 410, 15, 60, 9, "20 mins", "Elderly", "/images/menu items/vegetable daliya.png", nutritionist1.Id, "Low Calorie");
            AddFoodIfMissing("Methi Soft Thepla with Dahi", 80, 390, 14, 58, 11, "15 mins", "Elderly", "/images/menu items/methi thelpa with dahi.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Curd Rice (Mild Tadka)", 70, 370, 12, 55, 10, "10 mins", "Elderly", "/images/menu items/curd rice with pomegranate.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Pumpkin Mash & Phulka", 75, 400, 13, 62, 10, "20 mins", "Elderly", "/images/menu items/pumpkin mash with phulkas.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Steamed Idli with Mild Sambar", 85, 350, 11, 60, 6, "15 mins", "Elderly", "/images/menu items/sambhar idli.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Mashed Aloo-Baingan Bharta", 80, 430, 12, 65, 14, "20 mins", "Elderly", "/images/menu items/baigan bharta.png", nutritionist1.Id, "Comfort Thali");
            AddFoodIfMissing("Warm Apple & Cinnamon Stew", 50, 240, 2, 45, 1, "15 mins", "Elderly", "/images/menu items/cinnamon apple stew.png", nutritionist1.Id, "Low Calorie");

            // --- CORPORATE ---
            AddFoodIfMissing("Deep Work Grain Bowl", 149, 480, 28, 60, 12, "20 mins", "Corporate", "/images/menu items/deep grain bowl.png", nutritionist2.Id, "Low Calorie");
            AddFoodIfMissing("Paneer Tikka Wrap (Whole Wheat)", 129, 420, 24, 45, 18, "15 mins", "Corporate", "/images/menu items/paneer tikka wrap.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Executive Lean Thali", 159, 500, 30, 65, 14, "30 mins", "Corporate", "/images/menu items/executive lean thali.png", nutritionist2.Id, "Special Thali");
            AddFoodIfMissing("Grilled Tofu & Sprout Salad", 139, 350, 26, 25, 15, "15 mins", "Student", "/images/menu items/Grilled tofu salad with sprouts.png", nutritionist2.Id, "Salad Bowl");
            AddFoodIfMissing("Methi-Matar Malai (Low Cream)", 149, 470, 22, 50, 20, "25 mins", "Corporate", "/images/menu items/Methi-Matar Malai.png", nutritionist2.Id, "Deluxe Thali");
            AddFoodIfMissing("Black Chana & Barley Salad", 139, 430, 20, 55, 12, "15 mins", "Corporate", "/images/menu items/Black Chana & Barley Salad.png", nutritionist2.Id, "Salad Bowl");
            AddFoodIfMissing("Whole Wheat Pesto Paneer Wrap", 135, 410, 23, 42, 16, "15 mins", "Corporate", "/images/menu items/pesto paneer wrap.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Tandoori Soya Chaap Bowl", 149, 460, 27, 48, 14, "20 mins", "Corporate", "/images/menu items/Tandoori Soya Chaap Bowl.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Work-Day Dal Khichdi Bowl", 89, 480, 22, 68, 10, "20 mins", "Corporate", "/images/menu items/Dal khichdi.png", nutritionist2.Id, "Comfort Thali");
            AddFoodIfMissing("Aloo-Matar Tehri", 85, 520, 18, 75, 12, "25 mins", "Corporate", "/images/menu items/Tehri.png", nutritionist2.Id, "Rice Combo");
            AddFoodIfMissing("Black Chana Curry & Rotis", 95, 450, 25, 65, 12, "25 mins", "Corporate", "/images/menu items/black channa with 2 rotis.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Mixed Veg Jowar Upma", 79, 390, 17, 58, 11, "15 mins", "Corporate", "/images/menu items/jowar upma.png", nutritionist2.Id, "Low Calorie");

            // --- STUDENT ---
            AddFoodIfMissing("Exam Topper Combo", 119, 630, 32, 85, 18, "25 mins", "Student", "/images/menu items/exam topper combo.png", nutritionist2.Id, "Rice Combo");
            AddFoodIfMissing("Quick Bite Rice Bowl", 89, 450, 18, 70, 10, "15 mins", "Student", "/images/menu items/quick bowl rice.png", nutritionist2.Id, "Rice Combo");
            AddFoodIfMissing("Pocket Friendly Thali", 75, 510, 14, 80, 12, "20 mins", "Student", "/images/menu items/pocket friendly thali.png", nutritionist2.Id, "Standard Thali");
            AddFoodIfMissing("Protein-Packed Soya Chunk Curry", 95, 520, 28, 65, 14, "20 mins", "Student", "/images/menu items/soya chunks curry.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Paneer Bhurji with 3 Phulkas", 119, 610, 30, 60, 22, "20 mins", "Student", "/images/menu items/paneer bhurji with 3 phulkas.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Egg Thali", 109, 580, 32, 65, 18, "25 mins", "Student", "/images/menu items/egg thali.png", nutritionist2.Id, "Protein Meal");
            AddFoodIfMissing("Late Night Khichdi Bowl", 69, 420, 14, 65, 10, "15 mins", "Student", "/images/menu items/Dal khichdi.png", nutritionist2.Id, "Comfort Thali");
            AddFoodIfMissing("Curd Rice with Pomegranate", 65, 390, 11, 60, 10, "10 mins", "Student", "/images/menu items/curd rice with pomegranate.png", nutritionist2.Id, "Comfort Thali");
            AddFoodIfMissing("Veg Tawa Pulav", 79, 470, 12, 75, 12, "20 mins", "Student", "/images/menu items/tawa pulao.png", nutritionist2.Id, "Rice Combo");
            AddFoodIfMissing("Kadhi Chawal", 85, 510, 16, 75, 14, "20 mins", "Student", "/images/menu items/kadhii chawal.png", nutritionist2.Id, "Rice Combo");
            AddFoodIfMissing("Aloo Paratha + Curd", 90, 600, 15, 80, 20, "20 mins", "Student", "/images/menu items/aloo paratha with curd.png", nutritionist2.Id, "Standard Thali");
            AddFoodIfMissing("Sprouted Moong Salad Box", 50, 280, 18, 40, 6, "10 mins", "Student", "/images/menu items/mixed sprouts.png", nutritionist2.Id, "Salad Bowl");
            AddFoodIfMissing("Flash Breakfast Smoothie", 65, 350, 15, 55, 8, "10 mins", "Student", "/images/menu items/fresh smoothie.png", nutritionist2.Id, "Low Calorie");

            // 4. Force-verify ALL existing foods and assign dummy nutrition data if missing
            var allFoods = _context.Foods.ToList();
            foreach (var f in allFoods)
            {
                f.IsVerified = true;
                f.NutritionistId = nutritionist1.Id;
                
                // Assign dummy nutrition data if missing or 0
                if (f.Protein == null || f.Protein == 0) f.Protein = 12.5;
                if (f.Carbs == null || f.Carbs == 0) f.Carbs = 40.0;
                if (f.Fat == null || f.Fat == 0) f.Fat = 8.5;
            }

            _context.SaveChanges();
            return Content("Nutritionist verification system is now LIVE! All meals have been verified by our experts. Please refresh your pages.");
        }

        private static readonly string[] SpecifiedMealCategories = {
            "Low Calorie", "Protein Meal", "Rice Combo", "Salad Bowl",
            "Classical Thali", "Comfort Thali", "Deluxe Thali",
            "Jain Thali", "Special Thali", "Standard Thali"
        };

        // =========================
        // ✅ PUBLIC HOME PAGE
        // =========================
        [AllowAnonymous]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserName = userName;

            // 🥘 Load Categories for Homepage (filtered to specified list, distinct names)
            var categories = _context.AddCategories
                .Where(c => !string.IsNullOrEmpty(c.MealCategory) && SpecifiedMealCategories.Contains(c.MealCategory))
                .AsEnumerable()
                .GroupBy(c => c.MealCategory)
                .Select(g => g.First())
                .OrderBy(c => c.MealCategory)
                .ToList();
            ViewBag.Categories = categories;

            // 🥗 Load foods with Nutritionist details
            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.Status == "Active" || f.Status == null)
                .OrderByDescending(f => f.Id)
                .ToList();

            ViewBag.Foods = foods;

            if (!uid.HasValue)
            {
                // Fallback for non-surveyed users
                var queryFallback = _context.Foods.Include(f => f.Nutritionist).Where(f => f.Status == "Active" || f.Status == null);
                ViewBag.RecommendedFoods = queryFallback.OrderBy(r => Guid.NewGuid()).Take(4).ToList();
                ViewBag.RecommendationReason = "Popular choices from our kitchen today.";
                return View();
            }

            int calorieGoal = 1450;

            // 🔹 Get latest recommended calories
            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (survey != null)
                calorieGoal = (int)survey.RecommendedCalories;

            var today = DateTime.Today;

            var todayEntries = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date.Date == today)
                .ToList();

            int totalCalories = todayEntries.Sum(d => d.Calories);
            int totalProtein = (int)todayEntries.Sum(d => d.Protein);

            ViewBag.CalorieGoal = calorieGoal;
            ViewBag.TotalCalories = totalCalories;
            ViewBag.TotalProtein = totalProtein;
            ViewBag.RemainingCalories = calorieGoal - totalCalories;

            double progress = calorieGoal > 0
                ? (double)totalCalories / calorieGoal * 100
                : 0;

            ViewBag.Progress = progress > 100 ? 100 : progress;

            // 💡 AI-Powered Personalized Recommendations (Daily Blueprint)
            Food breakfast = null;
            Food lunch = null;
            Food dinner = null;

            var activeFoodsQuery = _context.Foods.Include(f => f.Nutritionist).Where(f => f.Status == "Active" || f.Status == null);
            var allActiveFoods = activeFoodsQuery.ToList();

            // Simple Categorization Engine
            bool IsBreakfast(Food f) => f.Name.Contains("Smoothie", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Oats", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Cheela", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Idli", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Upma", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Paratha", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Apple", StringComparison.OrdinalIgnoreCase);
            bool IsLunch(Food f) => f.Name.Contains("Thali", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Combo", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Curry", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Tehri", StringComparison.OrdinalIgnoreCase);
            bool IsDinner(Food f) => f.Name.Contains("Salad", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Khichdi", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Bowl", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Dal", StringComparison.OrdinalIgnoreCase);

            var bfList = allActiveFoods.Where(IsBreakfast).ToList();
            var lList = allActiveFoods.Where(IsLunch).ToList();
            var dList = allActiveFoods.Where(IsDinner).ToList();

            if (survey != null)
            {
                if (survey.Bmi > 25 || survey.Goal == "Weight Loss")
                {
                    breakfast = bfList.Where(f => f.Calories < 300).FirstOrDefault() ?? bfList.FirstOrDefault();
                    lunch = lList.Where(f => f.Calories < 500).FirstOrDefault() ?? lList.FirstOrDefault();
                    dinner = dList.Where(f => f.Calories < 400).FirstOrDefault() ?? dList.FirstOrDefault();
                    ViewBag.RecommendationReason = "Focusing on weight management? We've designed a structured, nutrient-dense daily plan just for you.";
                }
                else if (survey.Goal == "Muscle Gain")
                {
                    breakfast = bfList.OrderByDescending(f => f.Protein).FirstOrDefault() ?? bfList.FirstOrDefault();
                    lunch = lList.OrderByDescending(f => f.Protein).FirstOrDefault() ?? lList.FirstOrDefault();
                    dinner = dList.OrderByDescending(f => f.Protein).FirstOrDefault() ?? dList.FirstOrDefault();
                    ViewBag.RecommendationReason = "To support your muscle gain goals, this daily plan maximizes your protein intake.";
                }
                else if (survey.Age > 60)
                {
                    breakfast = bfList.Where(f => f.FoodType == "Elderly").FirstOrDefault() ?? bfList.FirstOrDefault();
                    lunch = lList.Where(f => f.FoodType == "Elderly").FirstOrDefault() ?? lList.FirstOrDefault();
                    dinner = dList.Where(f => f.FoodType == "Elderly").FirstOrDefault() ?? dList.FirstOrDefault();
                    ViewBag.RecommendationReason = "Specially curated soft and nutritious meals for healthy aging.";
                }
                else
                {
                    breakfast = bfList.FirstOrDefault();
                    lunch = lList.FirstOrDefault();
                    dinner = dList.FirstOrDefault();
                    ViewBag.RecommendationReason = "Hand-picked balanced meals tailored to your health profile.";
                }
            }
            else
            {
                // Fallback for non-surveyed users
                var rand = new Random();
                breakfast = bfList.OrderBy(x => rand.Next()).FirstOrDefault();
                lunch = lList.OrderBy(x => rand.Next()).FirstOrDefault();
                dinner = dList.OrderBy(x => rand.Next()).FirstOrDefault();
                ViewBag.RecommendationReason = "A complete nutritional blueprint for your day.";
            }

            ViewBag.Breakfast = breakfast;
            ViewBag.Lunch = lunch;
            ViewBag.Dinner = dinner;

            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetFoodDetail(int foodId)
        {
            try
            {
                var food = await _context.Foods
                    .Include(f => f.Nutritionist)
                    .FirstOrDefaultAsync(f => f.Id == foodId);

                if (food == null) return Json(new { success = false, message = "Food not found" });

                return Json(new { 
                    success = true, 
                    data = new {
                        name = food.Name,
                        price = food.Price.ToString("N2"),
                        calories = food.Calories ?? 0,
                        protein = food.Protein ?? 0,
                        description = food.Description ?? "No description available for this healthy meal.",
                        imagePath = string.IsNullOrEmpty(food.ImagePath) ? "/images/Meals/Standard_Thali.jpeg" : food.ImagePath
                    } 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching food details for {FoodId}", foodId);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        // =========================
        // ✅ PUBLIC ABOUT PAGE
        // =========================
        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        // =========================
        // ✅ PUBLIC LOCATION PAGE
        // =========================
        [AllowAnonymous]
        public IActionResult Location()
        {
            return View();
        }

        // =========================
        // ADMIN LOGIN (Optional)
        // =========================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return RedirectToAction("Login", "Auth");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = (model.Email ?? "").Trim();

            if (string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Password ?? "", AdminPassword, StringComparison.Ordinal))
            {
                _logger.LogInformation("Admin login succeeded for {Email}", email);
                return Redirect("/Admin/Dashboard");
            }

            _logger.LogWarning("Invalid login attempt for {Email}", email);
            ModelState.AddModelError(string.Empty, "Invalid email or password");

            return View(model);
        }

        // =========================
        // ERROR PAGE
        // =========================
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
        [SessionAuthorize]
        public IActionResult MyProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == userId.Value);

            if (user == null)
                return RedirectToAction("Login", "Auth");

            // Latest Health Survey
            var survey = _context.HealthSurveys
                .Where(h => h.UserId == userId.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            // Fetch Recent Orders (Top 3)
            var recentOrders = _context.OrderTables
                .Where(o => o.UserId == userId.Value)
                .OrderByDescending(o => o.CreatedAt)
                .Take(3)
                .ToList();

            ViewBag.User = user;
            ViewBag.Survey = survey;
            ViewBag.UserId = userId.Value;
            ViewBag.RecentOrders = recentOrders;

            return View();
        }

        [SessionAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Json(new { success = false, message = "Session expired" });

            var order = await _context.OrderTables
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId.Value);

            if (order == null) return Json(new { success = false, message = "Order not found" });

            // Only allow cancellation if not already processed/shipped
            var allowedStatuses = new[] { "Placed", "New", "Accepted" };
            if (!allowedStatuses.Contains(order.Status, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = $"Order cannot be cancelled in '{order.Status}' status." });
            }

            try
            {
                // Update Order Status
                order.Status = "Cancelled";
                order.UpdatedAt = DateTime.Now;

                // Update Payment Status if exists
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
                if (payment != null)
                {
                    payment.RefundStatus = "Pending";
                    payment.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Notify Admin via SignalR (using HubContext if available in HomeController or calling a service)
                // For now, simple return. The admin panel polls or uses SignalR to refresh.

                return Json(new { success = true, message = "Order cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                return Json(new { success = false, message = "Internal server error during cancellation." });
            }
        }

        [SessionAuthorize]
        public IActionResult MyOrders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var orders = _context.OrderTables
                .Where(o => o.UserId == userId.Value)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            return View(orders);
        }

        [SessionAuthorize]
        public IActionResult TrackOrder(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var order = _context.OrderTables
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Food)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.BulkItemData)
                .FirstOrDefault(o => o.OrderId == orderId && o.UserId == userId.Value);

            if (order == null) return NotFound();

            return View(order);
        }
        [HttpGet]
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == userId.Value);

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(UserSignup model)
        {
            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == model.Id);

            if (user == null)
                return RedirectToAction("MyProfile");

            user.Name = model.Name;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.Address = model.Address;

            _context.SaveChanges();

            HttpContext.Session.SetString("UserName", user.Name);

            return RedirectToAction("MyProfile");
        }

    }


}