using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class BulkController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BulkController(ApplicationDbContext context)
        {
            _context = context;
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
                .OrderBy(b => b.Name)
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
                .OrderBy(b => b.Name)
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
            // Predefined Food Boxes
            var predefined = _context.BulkItems
                .Where(x => x.Status == "Active" && x.Category == "FoodBox_Predefined")
                .OrderBy(x => x.Name)
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
                .OrderBy(x => x.SubCategory)
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