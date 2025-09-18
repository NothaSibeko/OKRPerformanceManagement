using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get some basic statistics for the dashboard
            var employeeCount = _context.Employees.Count();
            var reviewCount = _context.PerformanceReviews.Count();
            var objectiveCount = _context.Objectives.Count();
            var keyResultCount = _context.KeyResults.Count();

            ViewBag.EmployeeCount = employeeCount;
            ViewBag.ReviewCount = reviewCount;
            ViewBag.ObjectiveCount = objectiveCount;
            ViewBag.KeyResultCount = keyResultCount;

            // Set user role based on the current user's role
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != null)
            {
                var currentEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.UserId == currentUserId);
                
                if (currentEmployee != null)
                {
                    // Check if user is Admin (has Admin role in Identity)
                    if (User.IsInRole("Admin"))
                    {
                        ViewBag.UserRole = "Admin";
                    }
                    else if (User.IsInRole("Manager") || currentEmployee.Role == "Manager")
                    {
                        ViewBag.UserRole = "Manager";
                    }
                    else
                    {
                        ViewBag.UserRole = "Employee";
                    }
                }
                else
                {
                    ViewBag.UserRole = "Employee"; // Default fallback
                }
            }
            else
            {
                ViewBag.UserRole = "Employee"; // Default fallback
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
