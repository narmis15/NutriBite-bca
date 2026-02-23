using Microsoft.AspNetCore.Mvc;

namespace NUTRIBITE.Controllers
{
    public class CorporateController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
