using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SeedDataService _seedDataService;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SeedDataService seedDataService)
        {
            _context = context;
            _userManager = userManager;
            _seedDataService = seedDataService;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .Include(e => e.Subordinates)
                .ToListAsync();

            var roles = await _context.EmployeeRoles.ToListAsync();
            var okrTemplates = await _context.OKRTemplates
                .Include(t => t.RoleEntity)
                .ToListAsync();

            ViewBag.Employees = employees;
            ViewBag.Roles = roles;
            ViewBag.OKRTemplates = okrTemplates;
            ViewBag.UserRole = "Admin";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CreateEmployee()
        {
            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.Managers = managers;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Create the user account
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Create employee record
                    var employee = new Employee
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email,
                        Role = model.Role,
                        Position = model.Position,
                        UserId = user.Id,
                        ManagerId = model.ManagerId == 0 ? null : model.ManagerId,
                        RoleId = model.RoleId,
                        LineOfBusiness = "Digital Industries - CSI3",
                        FinancialYear = "FY 2025",
                        IsActive = true
                    };

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    // Assign role to user
                    try
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                        TempData["SuccessMessage"] = $"Employee '{model.FirstName} {model.LastName}' has been created successfully!";
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't fail the employee creation
                        // The role can be assigned later by an admin
                        TempData["WarningMessage"] = $"Employee '{model.FirstName} {model.LastName}' created successfully, but role assignment failed: {ex.Message}";
                    }

                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we get here, there was an error - repopulate dropdowns
            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.Managers = managers;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager" && e.Id != id)
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.Managers = managers;

            var model = new EditEmployeeViewModel
            {
                Id = employee.Id,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = employee.Role,
                Position = employee.Position,
                ManagerId = employee.ManagerId,
                RoleId = employee.RoleId,
                IsActive = employee.IsActive
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditEmployee(EditEmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                var employee = await _context.Employees.FindAsync(model.Id);
                if (employee == null)
                {
                    return NotFound();
                }

                employee.FirstName = model.FirstName;
                employee.LastName = model.LastName;
                employee.Email = model.Email;
                employee.Role = model.Role;
                employee.Position = model.Position;
                employee.ManagerId = model.ManagerId == 0 ? null : model.ManagerId;
                employee.RoleId = model.RoleId;
                employee.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                return RedirectToAction("Index");
            }

            // If we get here, there was an error - repopulate dropdowns
            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager" && e.Id != model.Id)
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.Managers = managers;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ManageRoles()
        {
            var employeeRoles = await _context.EmployeeRoles.ToListAsync();
            var identityRoles = await _context.Roles.ToListAsync();
            
            ViewBag.EmployeeRoles = employeeRoles;
            ViewBag.IdentityRoles = identityRoles;
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(string name, string description, bool isActive = true)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var role = new EmployeeRole
                {
                    Name = name,
                    Description = description,
                    IsActive = isActive,
                    CreatedDate = DateTime.Now
                };

                _context.EmployeeRoles.Add(role);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Role '{name}' created successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Role name is required.";
            }

            return RedirectToAction("ManageRoles");
        }

        [HttpGet]
        public async Task<IActionResult> ManageOKRTemplates()
        {
            var templates = await _context.OKRTemplates
                .Include(t => t.RoleEntity)
                .Include(t => t.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .ToListAsync();

            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();

            ViewBag.Roles = roles;
            return View(templates);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOKRTemplate(CreateOKRTemplateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var template = new OKRTemplate
                {
                    Name = model.Name,
                    Role = model.Role,
                    Description = model.Description,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    RoleId = model.RoleId
                };

                _context.OKRTemplates.Add(template);
                await _context.SaveChangesAsync();

                return RedirectToAction("ManageOKRTemplates");
            }

            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            ViewBag.Roles = roles;
            return View("ManageOKRTemplates", await _context.OKRTemplates.Include(t => t.RoleEntity).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> SystemReports()
        {
            var totalEmployees = await _context.Employees.CountAsync();
            var activeReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status != "Completed")
                .CountAsync();
            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Completed")
                .CountAsync();

            var reviewsByStatus = await _context.PerformanceReviews
                .GroupBy(pr => pr.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var reviewsByRole = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .GroupBy(pr => pr.Employee.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.ActiveReviews = activeReviews;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.ReviewsByStatus = reviewsByStatus;
            ViewBag.ReviewsByRole = reviewsByRole;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ReSeedOKRTemplates()
        {
            try
            {
                await _seedDataService.ReSeedOKRTemplatesAsync();
                TempData["SuccessMessage"] = "OKR templates have been successfully updated with the latest structure!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating OKR templates: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SeedDetailedTemplates()
        {
            try
            {
                // For now, just show a message that this feature is coming soon
                TempData["SuccessMessage"] = "Detailed template seeding feature is being implemented. The basic templates are already available for use.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("ManageOKRTemplates");
        }

        [HttpGet]
        public async Task<IActionResult> MyPerformanceHistory()
        {
            // Get all employees in the organization
            var allEmployees = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .Include(e => e.Subordinates)
                .ToListAsync();

            // Get all completed reviews in the organization
            var allCompletedReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Completed")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .OrderByDescending(pr => pr.FinalizedDate)
                .ToListAsync();

            // Calculate performance statistics
            var performanceStats = new Dictionary<string, object>
            {
                ["TotalEmployees"] = allEmployees.Count,
                ["TotalReviews"] = allCompletedReviews.Count,
                ["AverageRating"] = allCompletedReviews.Any() ? allCompletedReviews.Average(r => r.OverallRating ?? 0) : 0,
                ["TopPerformers"] = allCompletedReviews
                    .Where(r => r.OverallRating.HasValue)
                    .OrderByDescending(r => r.OverallRating)
                    .Take(5)
                    .ToList()
            };

            ViewBag.AllEmployees = allEmployees;
            ViewBag.AllCompletedReviews = allCompletedReviews;
            ViewBag.PerformanceStats = performanceStats;
            ViewBag.UserRole = "Admin";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DebugEmployeeOKRs(string email = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                // Show all employees with their OKR counts
                var employeesWithOKRs = await _context.Employees
                    .Select(e => new
                    {
                        e.Id,
                        e.FirstName,
                        e.LastName,
                        e.Email,
                        e.Role,
                        ReviewCount = _context.PerformanceReviews.Count(pr => pr.EmployeeId == e.Id),
                        ObjectiveCount = _context.Objectives.Count(o => o.PerformanceReview.EmployeeId == e.Id),
                        KeyResultCount = _context.KeyResults.Count(kr => kr.Objective.PerformanceReview.EmployeeId == e.Id)
                    })
                    .OrderBy(e => e.Email)
                    .ToListAsync();

                ViewBag.EmployeesWithOKRs = employeesWithOKRs;
                return View();
            }
            else
            {
                // Show detailed OKR data for specific employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == email);

                if (employee == null)
                {
                    TempData["ErrorMessage"] = $"Employee with email '{email}' not found.";
                    return RedirectToAction("DebugEmployeeOKRs");
                }

                var reviews = await _context.PerformanceReviews
                    .Where(pr => pr.EmployeeId == employee.Id)
                    .Include(pr => pr.Objectives)
                        .ThenInclude(o => o.KeyResults)
                    .Include(pr => pr.OKRTemplate)
                    .Include(pr => pr.Manager)
                    .ToListAsync();

                ViewBag.Employee = employee;
                ViewBag.Reviews = reviews;
                return View("DebugEmployeeOKRsDetail");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CleanupEmployeeOKRs(int employeeId)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(employeeId);
                if (employee == null)
                {
                    TempData["ErrorMessage"] = "Employee not found.";
                    return RedirectToAction("DebugEmployeeOKRs");
                }

                // Get all reviews for this employee
                var reviews = await _context.PerformanceReviews
                    .Where(pr => pr.EmployeeId == employeeId)
                    .Include(pr => pr.Objectives)
                        .ThenInclude(o => o.KeyResults)
                    .ToListAsync();

                // Delete key results first
                foreach (var review in reviews)
                {
                    foreach (var objective in review.Objectives)
                    {
                        _context.KeyResults.RemoveRange(objective.KeyResults);
                    }
                }

                // Delete objectives
                foreach (var review in reviews)
                {
                    _context.Objectives.RemoveRange(review.Objectives);
                }

                // Delete performance reviews
                _context.PerformanceReviews.RemoveRange(reviews);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully cleaned up all OKR data for {employee.FirstName} {employee.LastName} ({employee.Email}).";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error cleaning up OKR data: {ex.Message}";
            }

            return RedirectToAction("DebugEmployeeOKRs");
        }
    }

    public class CreateEmployeeViewModel
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

        [Required]
        public string Position { get; set; }

        public int? ManagerId { get; set; }

        public int? RoleId { get; set; }
    }

    public class EditEmployeeViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Role { get; set; }

        [Required]
        public string Position { get; set; }

        public int? ManagerId { get; set; }

        public int? RoleId { get; set; }

        public bool IsActive { get; set; }
    }

    public class CreateOKRTemplateViewModel
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string Role { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        public int? RoleId { get; set; }
    }
}
