using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    public class PublicController : Controller
    {
        // GET: /Public/Index
        public IActionResult Index()
        {
            return View();
        }
    }
}