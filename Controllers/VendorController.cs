using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class VendorController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public VendorController(IConfiguration configuration,
                                IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        // ================= PASSWORD HASH =================
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // ================= REGISTER =================
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string vendorName, string email, string password)
        {
            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // Check email exists
                string checkQuery = "SELECT COUNT(*) FROM VendorSignup WHERE Email=@Email";
                SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@Email", email);

                int exists = (int)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    ViewBag.Error = "Email already exists!";
                    return View();
                }

                // Insert new vendor (default not approved)
                string query = @"INSERT INTO VendorSignup 
                                (VendorName, Email, PasswordHash, IsApproved, IsRejected)
                                VALUES (@VendorName, @Email, @PasswordHash, 0, 0)";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VendorName", vendorName);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@PasswordHash", HashPassword(password));

                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("Login");
        }

        // ================= LOGIN =================
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string query = @"SELECT VendorId, PasswordHash, IsApproved, IsRejected
                                 FROM VendorSignup
                                 WHERE Email = @Email";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Email", email);

                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    string dbPassword = reader["PasswordHash"].ToString();
                    bool isApproved = Convert.ToBoolean(reader["IsApproved"]);
                    bool isRejected = Convert.ToBoolean(reader["IsRejected"]);
                    int vendorId = Convert.ToInt32(reader["VendorId"]);

                    if (isRejected)
                    {
                        ViewBag.Error = "Your account was rejected by admin.";
                        return View();
                    }

                    if (!isApproved)
                    {
                        ViewBag.Error = "Your account is waiting for admin approval.";
                        return View();
                    }

                    if (dbPassword != HashPassword(password))
                    {
                        ViewBag.Error = "Invalid email or password.";
                        return View();
                    }

                    // Save session
                    HttpContext.Session.SetInt32("VendorId", vendorId);
                    HttpContext.Session.SetString("VendorEmail", email);

                    return RedirectToAction("Dashboard");
                }

                ViewBag.Error = "Invalid email or password.";
                return View();
            }
        }

        // ================= AUTH CHECK =================
        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("VendorId") != null;
        }

        // ================= DASHBOARD =================
        public IActionResult Dashboard()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            int vendorId = HttpContext.Session.GetInt32("VendorId").Value;

            string cs = _configuration.GetConnectionString("DBCS");

            int totalFoods = 0;

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT COUNT(*) FROM Foods WHERE VendorId = @VendorId";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VendorId", vendorId);

                con.Open();
                totalFoods = (int)cmd.ExecuteScalar();
            }

            ViewBag.TotalFoods = totalFoods;

            return View();
        }

        // ================= ADD FOOD =================
        public IActionResult AddFood()
        {
            return View();
        }

       
        [HttpPost]
        public IActionResult AddFood(Food model, IFormFile ImageFile)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            string imagePath = "";

            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "Vendorfooduploads");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = Guid.NewGuid().ToString()
                                  + Path.GetExtension(ImageFile.FileName);

                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                imagePath = "/Vendorfooduploads/" + fileName;
            }

            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"INSERT INTO Foods
                        (Name, Description, Price, CategoryId,
                         Calories, PreparationTime, ImagePath, VendorId)
                         VALUES
                        (@Name, @Description, @Price, @CategoryId,
                         @Calories, @PreparationTime, @ImagePath, @VendorId)";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@Name", model.Name);
                cmd.Parameters.AddWithValue("@Description", model.Description ?? "");
                cmd.Parameters.AddWithValue("@Price", model.Price);
                cmd.Parameters.AddWithValue("@CategoryId", model.CategoryId );
                cmd.Parameters.AddWithValue("@Calories", (object?)model.Calories ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PreparationTime", model.PreparationTime ?? "");
                cmd.Parameters.AddWithValue("@ImagePath", imagePath);
                cmd.Parameters.AddWithValue("@VendorId",
                    HttpContext.Session.GetInt32("VendorId"));

                con.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("MyFood");
        }

        // ================= MY FOODS =================
        public IActionResult MyFood()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            List<Food> foods = new List<Food>();

            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Foods WHERE VendorId = @VendorId";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VendorId",
                    HttpContext.Session.GetInt32("VendorId"));

                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    foods.Add(new Food
                    {
                        Id = (int)reader["Id"],
                        Name = reader["Name"].ToString(),
                        Description = reader["Description"].ToString(),
                        Price = (decimal)reader["Price"],
                        CategoryId = (int)reader["CategoryId"],
                        Calories = reader["Calories"] != DBNull.Value
                                    ? (int?)reader["Calories"]
                                    : null,
                        PreparationTime = reader["PreparationTime"].ToString(),
                        ImagePath = reader["ImagePath"].ToString(),
                        VendorId = (int)reader["VendorId"]
                    });
                }
            }

            return View(foods);
        }

        // ================= ORDERS =================
        public IActionResult Order()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        // ================= PROFILE =================
        public IActionResult Profile()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        // ================= LOGOUT =================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}