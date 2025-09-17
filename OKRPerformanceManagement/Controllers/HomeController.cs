using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;

namespace OKRPerformanceManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string role)
        {
            // Simple login logic - in production, use proper authentication
            var employee = _context.Employees.FirstOrDefault(e => e.Email == email && e.Role == role);

            if (employee != null)
            {
                HttpContext.Session.SetString("EmployeeId", employee.Id.ToString());
                HttpContext.Session.SetString("EmployeeRole", employee.Role);
                HttpContext.Session.SetString("EmployeeName", $"{employee.FirstName} {employee.LastName}");

                return RedirectToAction("Dashboard", "Home");
            }

            ViewBag.Error = "Invalid credentials";
            return View();
        }

        public IActionResult Dashboard()
        {
            var employeeId = HttpContext.Session.GetString("EmployeeId");
            var role = HttpContext.Session.GetString("EmployeeRole");

            if (string.IsNullOrEmpty(employeeId))
            {
                return RedirectToAction("Login");
            }

            ViewBag.EmployeeId = employeeId;
            ViewBag.Role = role;
            ViewBag.EmployeeName = HttpContext.Session.GetString("EmployeeName");

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}