using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    public class VendorController : Controller
    {
        // GET: Vendor/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: Vendor/Login
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            // TEMP LOGIN (no DB yet)
            if (email == "vendor@test.com" && password == "1234")
            {
                HttpContext.Session.SetString("VendorEmail", email);
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid login";
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
           // if (HttpContext.Session.GetString("VendorEmail") == null)
           // {
               // return RedirectToAction("Login");
           // }

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: Vendor/AddFood
        public IActionResult AddFood()
        {
            return View();
        }

        // POST: Vendor/AddFood
        [HttpPost]
        public IActionResult AddFood(string foodName, string description, int calories, decimal price, string category, string dietType)
        {
            // 🔒 NO DATABASE YET
            // Later you will save data here

            TempData["Success"] = "Food added successfully (UI demo).";
            return RedirectToAction("MyFood");
        }

        public IActionResult MyFood()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult Order()
        {
            return View();
        }
        public IActionResult ForgotPassword()
        {
            return View();
        }


    }
}
