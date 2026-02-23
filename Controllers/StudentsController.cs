using Microsoft.AspNetCore.Mvc;

namespace NUTRIBITE.Controllers
{
    public class StudentsController : Controller
    {
        // GET: /Students
        public IActionResult Index()
        {
            return View();
        }
    }
}