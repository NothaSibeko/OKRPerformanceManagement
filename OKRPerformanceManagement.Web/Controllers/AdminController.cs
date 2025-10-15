using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Web.Services;
using OKRPerformanceManagement.Web.ViewModels;
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
                .Where(e => e.IsActive) // Only show active employees
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

        [HttpPost]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            // Soft delete - set IsActive to false instead of removing the record
            employee.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Employee {employee.FirstName} {employee.LastName} has been deactivated successfully.";
            return RedirectToAction("Index");
        }

        // Role Management Actions
            
        [HttpGet]
        public IActionResult CreateRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(CreateRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if role name already exists
                var existingRole = await _context.EmployeeRoles
                    .FirstOrDefaultAsync(r => r.Name.ToLower() == model.Name.ToLower());

                if (existingRole != null)
                {
                    ModelState.AddModelError("Name", "A role with this name already exists.");
                    return View(model);
                }

                var role = new EmployeeRole
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.Now
                };

                _context.EmployeeRoles.Add(role);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Role '{role.Name}' has been created successfully.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _context.EmployeeRoles.FindAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var model = new EditRoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(EditRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var role = await _context.EmployeeRoles.FindAsync(model.Id);
                if (role == null)
                {
                    return NotFound();
                }

                // Check if role name already exists (excluding current role)
                var existingRole = await _context.EmployeeRoles
                    .FirstOrDefaultAsync(r => r.Name.ToLower() == model.Name.ToLower() && r.Id != model.Id);

                if (existingRole != null)
                {
                    ModelState.AddModelError("Name", "A role with this name already exists.");
                    return View(model);
                }

                role.Name = model.Name;
                role.Description = model.Description;
                role.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Role '{role.Name}' has been updated successfully.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.EmployeeRoles.FindAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            // Check if role is being used by any employees
            var employeesUsingRole = await _context.Employees
                .AnyAsync(e => e.RoleId == id);

            if (employeesUsingRole)
            {
                TempData["ErrorMessage"] = $"Cannot delete role '{role.Name}' because it is currently assigned to employees.";
                return RedirectToAction("Index");
            }

            _context.EmployeeRoles.Remove(role);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Role '{role.Name}' has been deleted successfully.";
            return RedirectToAction("Index");
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




    }
}
