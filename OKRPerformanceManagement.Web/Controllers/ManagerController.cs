using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            // Get all employees managed by this manager
            var teamMembers = await _context.Employees
                .Where(e => e.ManagerId == currentEmployee.Id)
                .Include(e => e.RoleEntity)
                .Include(e => e.PerformanceReviews)
                .ToListAsync();

            // Get pending reviews
            var pendingReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Employee_Review")
                .Include(pr => pr.Employee)
                .ToListAsync();

            // Get completed reviews
            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Signed")
                .Include(pr => pr.Employee)
                .ToListAsync();

            ViewBag.TeamMembers = teamMembers;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.Manager = currentEmployee;
            ViewBag.UserRole = "Manager";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CreateReview(int? employeeId = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Manager record not found.");
            }

            if (employeeId.HasValue)
            {
                // Show form for specific employee
                var employee = await _context.Employees
                    .Include(e => e.RoleEntity)
                    .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

                if (employee == null)
                {
                    return NotFound();
                }

                // Get the appropriate OKR template for this role
                var okrTemplate = await _context.OKRTemplates
                    .Include(t => t.Objectives)
                        .ThenInclude(o => o.KeyResults)
                    .FirstOrDefaultAsync(t => t.RoleId == employee.RoleId && t.IsActive);

                if (okrTemplate == null)
                {
                    TempData["ErrorMessage"] = $"No OKR template found for role: {employee.Role}";
                    return RedirectToAction("CreateReview");
                }

                ViewBag.Employee = employee;
                ViewBag.OKRTemplate = okrTemplate;
                ViewBag.IsSpecificEmployee = true;
            }
            else
            {
                // Show list of available team members
                var teamMembers = await _context.Employees
                    .Where(e => e.ManagerId == currentEmployee.Id && e.IsActive)
                    .Where(e => !_context.PerformanceReviews.Any(pr => pr.EmployeeId == e.Id && 
                        (pr.Status == "Draft" || pr.Status == "Employee_Review" || pr.Status == "Manager_Review")))
                    .Include(e => e.RoleEntity)
                    .ToListAsync();

                ViewBag.TeamMembers = teamMembers;
                ViewBag.IsSpecificEmployee = false;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview(int employeeId, DateTime reviewPeriodStart, DateTime reviewPeriodEnd)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null || currentEmployee == null)
            {
                return NotFound();
            }

            // Get the appropriate OKR template
            var okrTemplate = await _context.OKRTemplates
                .Include(t => t.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .FirstOrDefaultAsync(t => t.RoleId == employee.RoleId && t.IsActive);

            if (okrTemplate == null)
            {
                return NotFound($"No OKR template found for role: {employee.Role}");
            }

            // Create the performance review
            var performanceReview = new PerformanceReview
            {
                EmployeeId = employeeId,
                ManagerId = currentEmployee.Id,
                ReviewPeriodStart = reviewPeriodStart,
                ReviewPeriodEnd = reviewPeriodEnd,
                Status = "Draft",
                EmployeeSelfAssessment = "",
                ManagerAssessment = "",
                FinalAssessment = "",
                DiscussionNotes = "",
                EmployeeSignature = "",
                ManagerSignature = "",
                OKRTemplateId = okrTemplate.Id
            };

            _context.PerformanceReviews.Add(performanceReview);
            await _context.SaveChangesAsync();

            // Create objectives and key results from template
            foreach (var templateObjective in okrTemplate.Objectives.OrderBy(o => o.SortOrder))
            {
                var objective = new Objective
                {
                    PerformanceReviewId = performanceReview.Id,
                    Name = templateObjective.Name,
                    Weight = templateObjective.Weight,
                    Description = templateObjective.Description,
                    SortOrder = templateObjective.SortOrder
                };

                _context.Objectives.Add(objective);
                await _context.SaveChangesAsync();

                foreach (var templateKeyResult in templateObjective.KeyResults.OrderBy(kr => kr.SortOrder))
                {
                    var keyResult = new KeyResult
                    {
                        ObjectiveId = objective.Id,
                        Name = templateKeyResult.Name,
                        Target = templateKeyResult.Target,
                        Measure = templateKeyResult.Measure,
                        Objectives = templateKeyResult.Objectives,
                        MeasurementSource = templateKeyResult.MeasurementSource,
                        Weight = templateKeyResult.Weight,
                        SortOrder = templateKeyResult.SortOrder,
                        Rating1Description = templateKeyResult.Rating1Description,
                        Rating2Description = templateKeyResult.Rating2Description,
                        Rating3Description = templateKeyResult.Rating3Description,
                        Rating4Description = templateKeyResult.Rating4Description,
                        Rating5Description = templateKeyResult.Rating5Description,
                        EmployeeComments = "",
                        ManagerComments = "",
                        FinalComments = "",
                        DiscussionNotes = ""
                    };

                    _context.KeyResults.Add(keyResult);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("ReviewDetails", new { id = performanceReview.Id });
        }

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

        [HttpPost]
        public async Task<IActionResult> SubmitForEmployeeReview(int id)
        {
            var review = await _context.PerformanceReviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.Status = "Employee_Review";
            review.SubmittedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction("ReviewDetails", new { id });
        }


        public async Task<IActionResult> ReviewEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null) return NotFound();

            // Get employee's current OKR
            var currentOKR = await _context.PerformanceReviews
                .Where(pr => pr.EmployeeId == id && pr.Status != "Completed")
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .FirstOrDefaultAsync();

            ViewBag.Employee = employee;
            ViewBag.CurrentOKR = currentOKR;

            return View();
        }

        public async Task<IActionResult> ViewOKR(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null) return NotFound();

            // Get employee's OKR
            var okr = await _context.PerformanceReviews
                .Where(pr => pr.EmployeeId == id)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.Comments)
                .FirstOrDefaultAsync();

            ViewBag.Employee = employee;
            ViewBag.OKR = okr;

            return View();
        }

        public async Task<IActionResult> PendingReviews()
        {
            // Get current user
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null) return NotFound();

            // Get pending reviews
            var pendingReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Employee_Review")
                .Include(pr => pr.Employee)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .ToListAsync();

            ViewBag.PendingReviews = pendingReviews;
            ViewBag.Manager = currentEmployee;
            ViewBag.UserRole = currentEmployee.Role;

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
                // Get current manager
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentManager = await _context.Employees
                    .FirstOrDefaultAsync(e => e.UserId == currentUserId);

                if (currentManager == null)
                {
                    TempData["ErrorMessage"] = "Manager record not found.";
                    return RedirectToAction("Index");
                }

                // Create the user account
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = $"A user with email '{model.Email}' already exists. Please use a different email address.";
                    // Repopulate dropdowns and return view with model to preserve form data
                    var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
                    var managers = await _context.Employees
                        .Where(e => e.Role == "Manager")
                        .ToListAsync();

                    ViewBag.Roles = roles;
                    ViewBag.Managers = managers;
                    return View(model);
                }

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
                        ManagerId = currentManager.Id, // Set to current manager
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
                        
                        // Automatically create OKR for the new employee
                        await CreateAutomaticOKRForEmployee(employee);
                        
                        TempData["SuccessMessage"] = $"Employee '{model.FirstName} {model.LastName}' has been created successfully and can now log in! An OKR has been automatically assigned based on their role.";
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't fail the employee creation
                        // The role can be assigned later by an admin
                        TempData["WarningMessage"] = $"Employee '{model.FirstName} {model.LastName}' created successfully, but role assignment failed: {ex.Message}";
                    }

                    return RedirectToAction("Index");
                }

                // Handle specific error cases
                foreach (var error in result.Errors)
                {
                    if (error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail")
                    {
                        TempData["ErrorMessage"] = $"A user with email '{model.Email}' already exists. Please use a different email address.";
                        // Repopulate dropdowns and return view with model to preserve form data
                        var roles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
                        var managers = await _context.Employees
                            .Where(e => e.Role == "Manager")
                            .ToListAsync();

                        ViewBag.Roles = roles;
                        ViewBag.Managers = managers;
                        return View(model);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // If we get here, there was an error - repopulate dropdowns
            var availableRoles = await _context.EmployeeRoles.Where(r => r.IsActive).ToListAsync();
            var availableManagers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .ToListAsync();

            ViewBag.Roles = availableRoles;
            ViewBag.Managers = availableManagers;

            return View(model);
        }

        private async Task CreateAutomaticOKRForEmployee(Employee employee)
        {
            try
            {
                // Get the appropriate OKR template for this role
                var okrTemplate = await _context.OKRTemplates
                    .Include(t => t.Objectives)
                        .ThenInclude(o => o.KeyResults)
                    .FirstOrDefaultAsync(t => t.Role == employee.Role && t.IsActive);

                if (okrTemplate == null)
                {
                    // Log warning but don't fail - OKR can be created manually later
                    return;
                }

                // Create the performance review with current year period
                var currentYear = DateTime.Now.Year;
                var performanceReview = new PerformanceReview
                {
                    EmployeeId = employee.Id,
                    ManagerId = employee.ManagerId ?? 0,
                    OKRTemplateId = okrTemplate.Id,
                    ReviewPeriodStart = new DateTime(currentYear, 1, 1),
                    ReviewPeriodEnd = new DateTime(currentYear, 12, 31),
                    Status = "Draft",
                    CreatedDate = DateTime.Now,
                    EmployeeSelfAssessment = "",
                    ManagerAssessment = "",
                    FinalAssessment = "",
                    DiscussionNotes = "",
                    EmployeeSignature = "",
                    ManagerSignature = ""
                };

                _context.PerformanceReviews.Add(performanceReview);
                await _context.SaveChangesAsync();

                // Create objectives and key results from template
                foreach (var templateObjective in okrTemplate.Objectives)
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

                    // Create key results for this objective
                    foreach (var templateKeyResult in templateObjective.KeyResults)
                    {
                        var keyResult = new KeyResult
                        {
                            ObjectiveId = objective.Id,
                            Name = templateKeyResult.Name,
                            Target = templateKeyResult.Target,
                            Measure = templateKeyResult.Measure,
                            Objectives = templateKeyResult.Objectives,
                            MeasurementSource = templateKeyResult.MeasurementSource,
                            Weight = templateKeyResult.Weight,
                            SortOrder = templateKeyResult.SortOrder,
                            Rating1Description = templateKeyResult.Rating1Description,
                            Rating2Description = templateKeyResult.Rating2Description,
                            Rating3Description = templateKeyResult.Rating3Description,
                            Rating4Description = templateKeyResult.Rating4Description,
                            Rating5Description = templateKeyResult.Rating5Description,
                            EmployeeComments = "",
                            ManagerComments = "",
                            FinalComments = "",
                            DiscussionNotes = ""
                        };

                        _context.KeyResults.Add(keyResult);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't fail employee creation
                // OKR can be created manually later
            }
        }

        // Template-based Review Creation Methods
        [HttpGet]
        public async Task<IActionResult> CreateTemplateReview()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Manager record not found.");
            }

            var model = new CreateTemplateReviewViewModel
            {
                ReviewPeriodStart = DateTime.Now,
                ReviewPeriodEnd = DateTime.Now.AddMonths(3),
                AvailableTemplates = await _context.OKRTemplates
                    .Where(t => t.IsActive)
                    .Include(t => t.RoleEntity)
                    .ToListAsync(),
                AvailableEmployees = await _context.Employees
                    .Where(e => e.ManagerId == currentEmployee.Id && e.IsActive)
                    .Include(e => e.RoleEntity)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTemplateReview(CreateTemplateReviewViewModel model)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Manager record not found.");
            }

            if (!ModelState.IsValid)
            {
                // Repopulate the model
                model.AvailableTemplates = await _context.OKRTemplates
                    .Where(t => t.IsActive)
                    .Include(t => t.RoleEntity)
                    .ToListAsync();
                model.AvailableEmployees = await _context.Employees
                    .Where(e => e.ManagerId == currentEmployee.Id && e.IsActive)
                    .Include(e => e.RoleEntity)
                    .ToListAsync();
                return View(model);
            }

            // Get the selected template
            var template = await _context.OKRTemplates
                .Include(t => t.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .FirstOrDefaultAsync(t => t.Id == model.OKRTemplateId);

            if (template == null)
            {
                TempData["ErrorMessage"] = "Selected template not found.";
                return RedirectToAction("CreateTemplateReview");
            }

            // Get selected employees
            var selectedEmployees = await _context.Employees
                .Where(e => model.SelectedEmployeeIds.Contains(e.Id))
                .ToListAsync();

            if (!selectedEmployees.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one team member.";
                return RedirectToAction("CreateTemplateReview");
            }

            var createdReviews = new List<int>();

            // Create a review for each selected employee
            foreach (var employee in selectedEmployees)
            {
                // Check if employee already has an active review
                var existingReview = await _context.PerformanceReviews
                    .FirstOrDefaultAsync(pr => pr.EmployeeId == employee.Id && 
                        (pr.Status == "Draft" || pr.Status == "Employee_Review" || pr.Status == "Manager_Review"));

                if (existingReview != null)
                {
                    TempData["WarningMessage"] = $"Employee {employee.FirstName} {employee.LastName} already has an active review.";
                    continue;
                }

                // Create the performance review
                var performanceReview = new PerformanceReview
                {
                    EmployeeId = employee.Id,
                    ManagerId = currentEmployee.Id,
                    Status = "Draft",
                    ReviewPeriodStart = model.ReviewPeriodStart,
                    ReviewPeriodEnd = model.ReviewPeriodEnd,
                    CreatedDate = DateTime.Now,
                    OKRTemplateId = template.Id,
                    EmployeeSelfAssessment = model.Description
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

                    // Create key results for this objective
                    foreach (var templateKeyResult in templateObjective.KeyResults)
                    {
                        var keyResult = new KeyResult
                        {
                            ObjectiveId = objective.Id,
                            Name = templateKeyResult.Name,
                            Target = templateKeyResult.Target,
                            Measure = templateKeyResult.Measure,
                            Objectives = templateKeyResult.Objectives,
                            MeasurementSource = templateKeyResult.MeasurementSource,
                            Weight = templateKeyResult.Weight,
                            SortOrder = templateKeyResult.SortOrder,
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
            }

            await _context.SaveChangesAsync();

            if (createdReviews.Any())
            {
                TempData["SuccessMessage"] = $"Successfully created {createdReviews.Count} performance review(s) based on the {template.Name} template.";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "No reviews were created. Please check for existing active reviews.";
                return RedirectToAction("CreateTemplateReview");
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyPerformanceHistory()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentManager == null)
            {
                return NotFound("Manager record not found.");
            }

            // Get all employees managed by this manager
            var teamMembers = await _context.Employees
                .Where(e => e.ManagerId == currentManager.Id)
                .Include(e => e.RoleEntity)
                .ToListAsync();

            // Get completed reviews for all team members
            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentManager.Id && pr.Status == "Completed")
                .Include(pr => pr.Employee)
                .Include(pr => pr.OKRTemplate)
                .OrderByDescending(pr => pr.FinalizedDate)
                .ToListAsync();

            ViewBag.CurrentManager = currentManager;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.TeamMembers = teamMembers;
            ViewBag.UserRole = "Manager";

            return View();
        }

    }
}
