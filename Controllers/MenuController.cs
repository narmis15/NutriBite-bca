using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;

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

        // ============================

        // Show foods inside category
        // ============================
        public IActionResult Category(string name)
        {
            if (string.IsNullOrEmpty(name))
                return NotFound();

            var category = _context.AddCategories
                .AsEnumerable()   // 👈 IMPORTANT
                .FirstOrDefault(c =>
                    c.MealCategory.Trim().ToLower()
                    == name.Trim().ToLower());

            if (category == null)
                return Content("Category Not Found: " + name);

            var foods = _context.Foods
                .Where(f => f.CategoryId == category.Cid)
                .ToList();

            ViewBag.CategoryName = category.MealCategory;

            return View(foods);
        }
        public IActionResult Test()
        {
            return Content("Menu Controller Working");
        }
    }
}