using Microsoft.AspNetCore.Mvc;

namespace NUTRIBITE.Controllers
{
    public class BulkController : Controller
    {
        // GET: /Bulk
        [HttpGet]
        public IActionResult Index()
        {
            // View will render hero cards from ViewBag.HeroCards if provided,
            // otherwise shows empty template cards which are easy to replace.
            return View();
        }

        // GET: /Bulk/Meals
        [HttpGet]
        public IActionResult Meals()
        {
            ViewBag.ActiveCategory = "Meals";
            // Reuse same view for now so routes exist. Developer can create a dedicated view later.
            return View("Index");
        }

        // GET: /Bulk/Snacks
        [HttpGet]
        public IActionResult Snacks()
        {
            ViewBag.ActiveCategory = "Snacks";
            return View("Index");
        }

        // GET: /Bulk/FoodBox
        [HttpGet]
        public IActionResult FoodBox()
        {
            ViewBag.ActiveCategory = "FoodBox";
            return View("Index");
        }
    }
}