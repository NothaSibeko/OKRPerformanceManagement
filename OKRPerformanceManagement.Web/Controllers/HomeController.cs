using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

        public IActionResult Index()
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
