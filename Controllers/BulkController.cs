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
            var list = _context.BulkItems
                .Where(b => b.Status == "Active" && b.Category == "FoodBox")
                .OrderBy(b => b.Name)
                .Select(b => new {
                    b.Id,
                    b.Name,
                    ShortDesc = b.Description,
                    LongDesc = b.Description,
                    b.Price,
                    b.IsVeg,
                    ImagePath = b.ImagePath ?? "/images/bulk/default.jpg",
                    MOQ = b.MOQ ?? 0
                })
                .ToList();

            ViewBag.Items = list;
            ViewBag.ActiveCategory = "FoodBox";
            return View("FoodBox");
        }
    }
}