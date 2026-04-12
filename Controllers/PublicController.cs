using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace NUTRIBITE.Controllers
{
    public class PublicController : Controller
    {
        private readonly NUTRIBITE.Models.ApplicationDbContext _context;
        private readonly NUTRIBITE.Services.IRecipeAnalysisService _recipeService;

        public PublicController(NUTRIBITE.Models.ApplicationDbContext context, NUTRIBITE.Services.IRecipeAnalysisService recipeService)
        {
            _context = context;
            _recipeService = recipeService;
        }

        // GET: /Public/Index
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (uid.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // GET: /Public/FixData
        public async System.Threading.Tasks.Task<IActionResult> FixData()
        {
            var foods = _context.Foods.Include(f => f.Recipe).ToList();

            foreach (var food in foods)
            {
                string n = food.Name.ToLower();
                
                // --- IMAGE MAPPING ---
                if (n.Contains("khichdi") || n.Contains("rice")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?q=80&w=800&auto=format&fit=crop"; // Rice Bowl
                } else if (n.Contains("salad")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?q=80&w=800&auto=format&fit=crop"; // Salad
                } else if (n.Contains("paneer")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1631452180519-c014fe946bc0?q=80&w=800&auto=format&fit=crop"; // Curry / Paneer
                } else if (n.Contains("chicken")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1604908176997-125f25cc6f3d?q=80&w=800&auto=format&fit=crop"; // Chicken
                } else if (n.Contains("shake") || n.Contains("smoothie") || n.Contains("juice")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1623065422900-0339951fd38f?q=80&w=800&auto=format&fit=crop"; // Shake
                } else if (n.Contains("dal") || n.Contains("soup")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1547592166-23ac45744acd?q=80&w=800&auto=format&fit=crop"; // Soup/Dal
                } else if (n.Contains("wrap") || n.Contains("roll")) {
                    food.ImagePath = "https://images.unsplash.com/photo-1628840042765-356cda07504e?q=80&w=800&auto=format&fit=crop"; // Wrap
                } else {
                    var fallbackImages = new string[] {
                        "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800&q=80",
                        "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?w=800&q=80",
                        "https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=800&q=80",
                        "https://images.unsplash.com/photo-1604908176997-125f25cc6f3d?w=800&q=80",
                        "https://images.unsplash.com/photo-1588166524941-3bf61a9c41db?w=800&q=80",
                        "https://images.unsplash.com/photo-1565557623262-b51c2513a641?w=800&q=80",
                        "https://images.unsplash.com/photo-1505253716362-afaea1d3d1af?w=800&q=80"
                    };
                    food.ImagePath = fallbackImages[food.Id % fallbackImages.Length];
                }

                _context.Foods.Update(food);

                // --- RECIPE INGREDIENTS MAPPING ---
                if (food.Recipe != null)
                {
                    if (n.Contains("khichdi")) {
                        food.Recipe.Ingredients = "100g Rice, 50g Moong Dal, 1 tbsp Ghee, 1 tsp Turmeric";
                    } else if (n.Contains("rice")) {
                        food.Recipe.Ingredients = "200g Brown Rice, 1 tbsp Olive Oil, 50g Vegetables";
                    } else if (n.Contains("salad")) {
                        food.Recipe.Ingredients = "100g Lettuce, 50g Tomatoes, 50g Cucumber, 1 tbsp Olive Oil";
                    } else if (n.Contains("paneer")) {
                        food.Recipe.Ingredients = "150g Paneer, 50g Onions, 50g Tomatoes, 1 tbsp Oil, 1 tsp Masala";
                    } else if (n.Contains("chicken")) {
                        food.Recipe.Ingredients = "200g Chicken Breast, 1 tbsp Olive Oil, 1 tsp Pepper, 50g Beans";
                    } else if (n.Contains("shake") || n.Contains("smoothie")) {
                        food.Recipe.Ingredients = "1 cup Milk, 1 Banana, 1 tbsp Honey, 50g Oats";
                    } else {
                        food.Recipe.Ingredients = "100g Mixed Vegetables, 1 tbsp Olive Oil, 1 tsp Salt, 100g Lentils";
                    }
                    _context.Recipes.Update(food.Recipe);
                }
            }

            await _context.SaveChangesAsync();
            await _recipeService.RecalculateAllRecipeNutritionAsync();

            return Content("Successfully updated all food images and recalculated realistic recipe ingredients!");
        }
    }
}