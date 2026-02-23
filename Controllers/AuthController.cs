using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace NUTRIBITE.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;

<<<<<<< HEAD
        // Hardcoded admin for dev/testing (replace with proper auth in production)
        private const string DevAdminEmail = "Nutribite123@gmail.com";
        private const string DevAdminPassword = "NutriBite//26";

        // GET /Auth/Login  (renders login page)
=======
        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: /Auth/Login
>>>>>>> a3e94a9be3432f1bdc5b9216859999546b8d6383
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Auth/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

<<<<<<< HEAD
        // Lightweight probe used by client UI
        [HttpGet]
        public IActionResult IsAuthenticated()
        {
            return Json(new
            {
                authenticated = HttpContext.Session.GetInt32("UserId").HasValue,
                userName = HttpContext.Session.GetString("UserName") ?? ""
            });
        }

        // POST /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "Email and password are required." });

            // Trim for consistent comparison
            var trimmedEmail = email.Trim();

            // 1) Quick dev/admin bypass: if matches hardcoded admin credentials, skip DB
            if (string.Equals(trimmedEmail, DevAdminEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(password ?? string.Empty, DevAdminPassword, StringComparison.Ordinal))
            {
                // minimal session for admin user (use a negative id to indicate special/dev account)
                HttpContext.Session.SetInt32("UserId", -1);
                HttpContext.Session.SetString("UserName", "Administrator");
                return Json(new { success = true, userName = "Administrator" });
            }

            // 2) Normal DB-backed authentication (unchanged)
            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
            using var con = new SqlConnection(cs);
            con.Open();

            // try to find user by email
            using var cmd = new SqlCommand("SELECT TOP(1) * FROM [Users] WHERE Email = @e", con);
            cmd.Parameters.AddWithValue("@e", trimmedEmail);
            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return Json(new { success = false, message = "Invalid credentials." });

            // read common fields if present
            var id = r["Id"] as object;
            var userId = id != null ? Convert.ToInt32(id) : 0;
            var name = r["Name"] as string ?? r["FullName"] as string ?? r["UserName"] as string ?? (r["Email"] as string ?? "");
            string dbPassword = r["Password"] as string ?? r["PasswordHash"] as string;
            string dbSalt = null;
            try
            {
                var schema = r.GetSchemaTable();
                if (schema != null && schema.Columns.Contains("PasswordSalt"))
                    dbSalt = r["PasswordSalt"] as string;
                else if (schema != null && schema.Columns.Contains("Salt"))
                    dbSalt = r["Salt"] as string;
            }
            catch
            {
                // ignore schema read issues and fall back to plain columns
            }

            // Prefer PBKDF2 if salt is present
            if (!string.IsNullOrEmpty(dbSalt) && !string.IsNullOrEmpty(dbPassword))
            {
                try
                {
                    var saltBytes = Convert.FromBase64String(dbSalt);
                    var hashBytes = Convert.FromBase64String(dbPassword);
                    using var derive = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
                    var testHash = derive.GetBytes(hashBytes.Length);
                    if (!testHash.SequenceEqual(hashBytes))
                        return Json(new { success = false, message = "Invalid credentials." });
                }
                catch
                {
                    // fall through to plain compare below
                    if (!string.Equals(dbPassword, password))
                        return Json(new { success = false, message = "Invalid credentials." });
                }
            }
            else if (!string.IsNullOrEmpty(dbPassword))
            {
                // fallback - plain compare (best-effort)
                if (!string.Equals(dbPassword, password))
                    return Json(new { success = false, message = "Invalid credentials." });
            }
            else
            {
                return Json(new { success = false, message = "User has no valid password stored." });
            }

            // success — store minimal session
            HttpContext.Session.SetInt32("UserId", userId);
            HttpContext.Session.SetString("UserName", name ?? email);
            return Json(new { success = true, userName = name ?? email });
        }

        // POST /Auth/Register
=======
        // POST: /Auth/Register
>>>>>>> a3e94a9be3432f1bdc5b9216859999546b8d6383
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string name, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "All fields are required." });
            }

            try
            {
                string cs = _configuration.GetConnectionString("DBCS");

                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Check if email already exists
                SqlCommand checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM UserSignup WHERE Email = @e", con);

                checkCmd.Parameters.AddWithValue("@e", email);

                int exists = (int)checkCmd.ExecuteScalar();

                if (exists > 0)
                {
                    return Json(new { success = false, message = "Email already registered." });
                }

                // Insert new user
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO UserSignup (Name, Email, Password) VALUES (@n, @e, @p)", con);

                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@p", password);

                cmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            string cs = _configuration.GetConnectionString("DBCS");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            SqlCommand cmd = new SqlCommand(
                "SELECT Id, Name FROM UserSignup WHERE Email = @e AND Password = @p", con);

            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@p", password);

            SqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                HttpContext.Session.SetInt32("UserId", (int)reader["Id"]);
                HttpContext.Session.SetString("UserName", reader["Name"].ToString());

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Invalid email or password." });
        }

        // Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}
<<<<<<< HEAD

=======
>>>>>>> a3e94a9be3432f1bdc5b9216859999546b8d6383
