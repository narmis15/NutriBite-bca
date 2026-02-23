using Microsoft.AspNetCore.Mvc;

namespace NUTRIBITE.Controllers
{
    public class ElderlyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
