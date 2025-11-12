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
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public HRController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            // HR sees everything - all employees, all reviews, all data
            var allEmployees = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .Where(e => e.IsActive)
                .ToListAsync();

            var allPendingReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Manager_Review" || pr.Status == "Employee_Review")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .ToListAsync();

            var allCompletedReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Completed" || pr.Status == "Signed")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .OrderByDescending(pr => pr.FinalizedDate ?? pr.CreatedDate)
                .Take(10)
                .ToListAsync();

            // Get upcoming scheduled discussions
            var upcomingDiscussions = await _context.PerformanceReviews
                .Where(pr => pr.ScheduledDiscussionDate.HasValue 
                    && pr.ScheduledDiscussionDate.Value >= DateTime.Now
                    && pr.Status == "Discussion")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .OrderBy(pr => pr.ScheduledDiscussionDate)
                .ToListAsync();

            // Statistics
            var totalEmployees = allEmployees.Count;
            var totalReviews = await _context.PerformanceReviews.CountAsync();
            var activeReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status != "Completed" && pr.Status != "Signed")
                .CountAsync();
            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Completed" || pr.Status == "Signed")
                .CountAsync();

            ViewBag.AllEmployees = allEmployees;
            ViewBag.AllPendingReviews = allPendingReviews;
            ViewBag.AllCompletedReviews = allCompletedReviews;
            ViewBag.UpcomingDiscussions = upcomingDiscussions;
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalReviews = totalReviews;
            ViewBag.ActiveReviews = activeReviews;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.UserRole = "HR";
            ViewBag.CurrentEmployee = currentEmployee;

            return View();
        }

        // Employee Management - Same as Admin
        [HttpGet]
        public async Task<IActionResult> CreateEmployee()
        {
            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager" || e.Role == "HR")
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
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = $"A user with email '{model.Email}' already exists.";
                    var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
                    var managers = await _context.Employees
                        .Where(e => e.Role == "Manager" || e.Role == "HR")
                        .ToListAsync();
                    ViewBag.Roles = roles;
                    ViewBag.Managers = managers;
                    return View(model);
                }

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
                        TempData["WarningMessage"] = $"Employee created successfully, but role assignment failed: {ex.Message}";
                    }

                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            var availableRoles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var availableManagers = await _context.Employees
                .Where(e => e.Role == "Manager" || e.Role == "HR")
                .ToListAsync();
            ViewBag.Roles = availableRoles;
            ViewBag.Managers = availableManagers;

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
                .Where(e => (e.Role == "Manager" || e.Role == "HR") && e.Id != id)
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
                TempData["SuccessMessage"] = $"Employee '{model.FirstName} {model.LastName}' has been updated successfully!";
                return RedirectToAction("Index");
            }

            var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var managers = await _context.Employees
                .Where(e => (e.Role == "Manager" || e.Role == "HR") && e.Id != model.Id)
                .ToListAsync();
            ViewBag.Roles = roles;
            ViewBag.Managers = managers;

            return View(model);
        }

        // Delete Employee (Soft Delete)
        [HttpPost]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found.";
                return RedirectToAction("Index");
            }

            // Soft delete - set IsActive to false instead of removing the record
            employee.IsActive = false;
            await _context.SaveChangesAsync();

            var employeeName = string.IsNullOrWhiteSpace(employee.FirstName) && string.IsNullOrWhiteSpace(employee.LastName) 
                ? employee.Email 
                : $"{employee.FirstName} {employee.LastName}".Trim();
            TempData["SuccessMessage"] = $"Employee {employeeName} has been deactivated successfully.";
            return RedirectToAction("Index");
        }

        // Assign OKR to ANY Employee (HR Super Power)
        [HttpGet]
        public async Task<IActionResult> AssignOKR()
        {
            var model = new CreateTemplateReviewViewModel
            {
                ReviewPeriodStart = DateTime.Now,
                ReviewPeriodEnd = DateTime.Now.AddMonths(3),
                AvailableTemplates = await _context.OKRTemplates
                    .Where(t => t.IsActive)
                    .Include(t => t.RoleEntity)
                    .ToListAsync(),
                // HR can assign to ANY employee, not just direct reports
                AvailableEmployees = await _context.Employees
                    .Where(e => e.IsActive)
                    .Include(e => e.RoleEntity)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AssignOKR(CreateTemplateReviewViewModel model)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentHR = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentHR == null)
            {
                return NotFound("HR record not found.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableTemplates = await _context.OKRTemplates
                    .Where(t => t.IsActive)
                    .Include(t => t.RoleEntity)
                    .ToListAsync();
                model.AvailableEmployees = await _context.Employees
                    .Where(e => e.IsActive)
                    .Include(e => e.RoleEntity)
                    .ToListAsync();
                return View(model);
            }

            var template = await _context.OKRTemplates
                .Include(t => t.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .FirstOrDefaultAsync(t => t.Id == model.OKRTemplateId);

            if (template == null)
            {
                TempData["ErrorMessage"] = "Selected template not found.";
                return RedirectToAction("AssignOKR");
            }

            var selectedEmployees = await _context.Employees
                .Where(e => model.SelectedEmployeeIds.Contains(e.Id))
                .ToListAsync();

            if (!selectedEmployees.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one employee.";
                return RedirectToAction("AssignOKR");
            }

            var createdReviews = new List<int>();
            var skippedEmployees = new List<string>();

            foreach (var employee in selectedEmployees)
            {
                // Check if employee already has an active review - NO OVERRIDING ALLOWED
                var existingReview = await _context.PerformanceReviews
                    .FirstOrDefaultAsync(pr => pr.EmployeeId == employee.Id && 
                        (pr.Status == "Draft" || pr.Status == "Employee_Review" || pr.Status == "Manager_Review" || pr.Status == "Discussion"));

                if (existingReview != null)
                {
                    var employeeName = string.IsNullOrWhiteSpace(employee.FirstName) && string.IsNullOrWhiteSpace(employee.LastName) 
                        ? employee.Email 
                        : $"{employee.FirstName} {employee.LastName}".Trim();
                    skippedEmployees.Add(employeeName);
                    continue;
                }

                // Use employee's manager if they have one, otherwise use HR as manager
                var managerId = employee.ManagerId ?? currentHR.Id;
                var manager = await _context.Employees.FindAsync(managerId);

                var performanceReview = new PerformanceReview
                {
                    EmployeeId = employee.Id,
                    ManagerId = managerId,
                    Status = "Draft",
                    ReviewPeriodStart = model.ReviewPeriodStart,
                    ReviewPeriodEnd = model.ReviewPeriodEnd,
                    CreatedDate = DateTime.Now,
                    OKRTemplateId = template.Id,
                    EmployeeSelfAssessment = model.Description ?? "",
                    ManagerAssessment = "",
                    FinalAssessment = "",
                    DiscussionNotes = "",
                    EmployeeSignature = "",
                    ManagerSignature = ""
                };

                _context.PerformanceReviews.Add(performanceReview);
                await _context.SaveChangesAsync();

                // Create objectives and key results from template
                foreach (var templateObjective in template.Objectives)
                {
                    var objective = new Objective
                    {
                        PerformanceReviewId = performanceReview.Id,
                        Name = templateObjective.Name,
                        Description = templateObjective.Description,
                        Weight = templateObjective.Weight,
                        SortOrder = templateObjective.SortOrder
                    };

                    _context.Objectives.Add(objective);
                    await _context.SaveChangesAsync();

                    foreach (var templateKeyResult in templateObjective.KeyResults)
                    {
                        var keyResult = new KeyResult
                        {
                            ObjectiveId = objective.Id,
                            Name = templateKeyResult.Name,
                            Target = templateKeyResult.Target,
                            Measure = templateKeyResult.Measure,
                            Weight = templateKeyResult.Weight,
                            SortOrder = templateKeyResult.SortOrder,
                            Objectives = templateKeyResult.Objectives,
                            MeasurementSource = templateKeyResult.MeasurementSource,
                            Rating1Description = templateKeyResult.Rating1Description,
                            Rating2Description = templateKeyResult.Rating2Description,
                            Rating3Description = templateKeyResult.Rating3Description,
                            Rating4Description = templateKeyResult.Rating4Description,
                            Rating5Description = templateKeyResult.Rating5Description,
                            EmployeeRating = null,
                            ManagerRating = null,
                            FinalRating = null,
                            EmployeeComments = "",
                            ManagerComments = "",
                            FinalComments = "",
                            DiscussionNotes = ""
                        };

                        _context.KeyResults.Add(keyResult);
                    }
                }

                createdReviews.Add(performanceReview.Id);

                // Send notification to employee
                if (employee.UserId != null)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId: employee.UserId,
                        senderId: currentUserId,
                        title: "New OKR Assigned",
                        message: $"HR has assigned you a new OKR for the period {model.ReviewPeriodStart:MMM dd, yyyy} - {model.ReviewPeriodEnd:MMM dd, yyyy}. Please review and complete it.",
                        type: "OKR_Assigned",
                        actionUrl: $"/Employee/MyActiveReviews",
                        relatedEntityId: performanceReview.Id,
                        relatedEntityType: "PerformanceReview"
                    );
                }

                // Send notification to manager (if manager exists and is different from HR)
                if (manager != null && manager.UserId != null && manager.Id != currentHR.Id)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId: manager.UserId,
                        senderId: currentUserId,
                        title: "New OKR Assigned to Your Team Member",
                        message: $"HR has assigned a new OKR to {employee.FirstName} {employee.LastName} for the period {model.ReviewPeriodStart:MMM dd, yyyy} - {model.ReviewPeriodEnd:MMM dd, yyyy}. Please review it once the employee completes their self-assessment.",
                        type: "OKR_Assigned_Manager",
                        actionUrl: $"/Manager/PendingReviews",
                        relatedEntityId: performanceReview.Id,
                        relatedEntityType: "PerformanceReview"
                    );
                }
            }

            await _context.SaveChangesAsync();

            if (createdReviews.Any() && skippedEmployees.Any())
            {
                TempData["SuccessMessage"] = $"OKRs assigned to {createdReviews.Count} employee(s). {skippedEmployees.Count} employee(s) were skipped because they already have active reviews.";
            }
            else if (createdReviews.Any())
            {
                TempData["SuccessMessage"] = $"OKRs successfully assigned to {createdReviews.Count} employee(s)! Notifications have been sent to employees and their managers.";
            }
            else
            {
                TempData["ErrorMessage"] = "No OKRs were assigned. All selected employees already have active reviews. Please wait until their current reviews are completed.";
            }

            return RedirectToAction("Index");
        }

        // View all reviews
        [HttpGet]
        public async Task<IActionResult> ViewAllReviews()
        {
            var allReviews = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .OrderByDescending(pr => pr.CreatedDate)
                .ToListAsync();

            ViewBag.AllReviews = allReviews;
            return View();
        }

        // View specific review details
        [HttpGet]
        public async Task<IActionResult> ReviewDetails(int id)
        {
            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // My Performance History (HR sees all reviews)
        [HttpGet]
        public async Task<IActionResult> MyPerformanceHistory()
        {
            // HR sees all reviews in the organization
            var allCompletedReviews = await _context.PerformanceReviews
                .Where(pr => pr.Status == "Completed" || pr.Status == "Signed")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .OrderByDescending(pr => pr.FinalizedDate ?? pr.CreatedDate)
                .ToListAsync();

            // Get all employees for statistics
            var allEmployees = await _context.Employees
                .Where(e => e.IsActive)
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

            ViewBag.AllCompletedReviews = allCompletedReviews;
            ViewBag.PerformanceStats = performanceStats;
            ViewBag.UserRole = "HR";

            return View();
        }
    }
}

