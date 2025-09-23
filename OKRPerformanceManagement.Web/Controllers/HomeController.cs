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
            // Set user role based on the current user's role
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);
            
            string userRole = "Employee"; // Default fallback
            
            if (currentEmployee != null)
            {
                // Check if user is Admin (has Admin role in Identity)
                if (User.IsInRole("Admin"))
                {
                    userRole = "Admin";
                }
                else if (User.IsInRole("Manager") || currentEmployee.Role == "Manager")
                {
                    userRole = "Manager";
                }
                else
                {
                    userRole = "Employee";
                }
            }

            ViewBag.UserRole = userRole;
            ViewBag.CurrentEmployee = currentEmployee;

            // Get role-specific statistics
            if (userRole == "Admin")
            {
                // Admin sees all system data
                ViewBag.EmployeeCount = await _context.Employees.CountAsync();
                ViewBag.ReviewCount = await _context.PerformanceReviews.CountAsync();
                ViewBag.ObjectiveCount = await _context.Objectives.CountAsync();
                ViewBag.KeyResultCount = await _context.KeyResults.CountAsync();
                ViewBag.ManagerCount = await _context.Employees.CountAsync(e => e.Role == "Manager");
                ViewBag.ActiveReviews = await _context.PerformanceReviews.CountAsync(pr => pr.Status != "Completed");
                ViewBag.CompletedReviews = await _context.PerformanceReviews.CountAsync(pr => pr.Status == "Completed");
            }
            else if (userRole == "Manager" && currentEmployee != null)
            {
                // Manager sees only their team data
                var teamMembers = await _context.Employees
                    .Where(e => e.ManagerId == currentEmployee.Id)
                    .ToListAsync();
                
                var teamMemberIds = teamMembers.Select(e => e.Id).ToList();
                
                ViewBag.EmployeeCount = teamMembers.Count;
                ViewBag.ReviewCount = await _context.PerformanceReviews
                    .CountAsync(pr => teamMemberIds.Contains(pr.EmployeeId));
                ViewBag.ObjectiveCount = await _context.Objectives
                    .CountAsync(o => teamMemberIds.Contains(o.PerformanceReview.EmployeeId));
                ViewBag.KeyResultCount = await _context.KeyResults
                    .CountAsync(kr => teamMemberIds.Contains(kr.Objective.PerformanceReview.EmployeeId));
                ViewBag.PendingReviews = await _context.PerformanceReviews
                    .CountAsync(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Employee_Review");
                ViewBag.CompletedReviews = await _context.PerformanceReviews
                    .CountAsync(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Completed");
                ViewBag.TeamMembers = teamMembers;
            }
            else
            {
                // Employee sees their own data
                if (currentEmployee != null)
                {
                    ViewBag.EmployeeCount = 1; // Just themselves
                    ViewBag.ReviewCount = await _context.PerformanceReviews
                        .CountAsync(pr => pr.EmployeeId == currentEmployee.Id);
                    ViewBag.ObjectiveCount = await _context.Objectives
                        .CountAsync(o => o.PerformanceReview.EmployeeId == currentEmployee.Id);
                    ViewBag.KeyResultCount = await _context.KeyResults
                        .CountAsync(kr => kr.Objective.PerformanceReview.EmployeeId == currentEmployee.Id);
                    ViewBag.MyActiveReviews = await _context.PerformanceReviews
                        .Where(pr => pr.EmployeeId == currentEmployee.Id && pr.Status != "Completed")
                        .ToListAsync();
                }
                else
                {
                    // Fallback for users without employee records
                    ViewBag.EmployeeCount = 0;
                    ViewBag.ReviewCount = 0;
                    ViewBag.ObjectiveCount = 0;
                    ViewBag.KeyResultCount = 0;
                }
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
