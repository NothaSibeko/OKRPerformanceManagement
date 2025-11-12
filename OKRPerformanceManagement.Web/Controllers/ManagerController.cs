using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Web.ViewModels;
using OKRPerformanceManagement.Web.Services;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public ManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
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

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            // Check if user has Manager role
            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!userRoles.Contains("Manager"))
                {
                    TempData["ErrorMessage"] = "Access denied. You do not have Manager privileges.";
                    return RedirectToAction("Index", "Home");
                }
            }

            // Get all employees managed by this manager
            var teamMembers = await _context.Employees
                .Where(e => e.ManagerId == currentEmployee.Id && e.IsActive) // Only show active employees
                .Include(e => e.RoleEntity)
                .Include(e => e.PerformanceReviews)
                .ToListAsync();

            // Get pending reviews (reviews that need manager attention)
            var pendingReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Manager_Review")
                .Include(pr => pr.Employee)
                .ToListAsync();

            // Get completed reviews
            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Signed")
                .Include(pr => pr.Employee)
                .ToListAsync();

            // Get upcoming scheduled discussions (future dates only)
            var upcomingDiscussions = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id 
                    && pr.ScheduledDiscussionDate.HasValue 
                    && pr.ScheduledDiscussionDate.Value >= DateTime.Now
                    && pr.Status == "Discussion")
                .Include(pr => pr.Employee)
                .OrderBy(pr => pr.ScheduledDiscussionDate)
                .ToListAsync();

            ViewBag.TeamMembers = teamMembers;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.UpcomingDiscussions = upcomingDiscussions;
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
                        Rating1Description = templateKeyResult.Rating1Description ?? "Needs Improvement",
                        Rating2Description = templateKeyResult.Rating2Description ?? "Below Expectations",
                        Rating3Description = templateKeyResult.Rating3Description ?? "Meets Expectations",
                        Rating4Description = templateKeyResult.Rating4Description ?? "Exceeds Expectations",
                        Rating5Description = templateKeyResult.Rating5Description ?? "Outstanding",
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

        [HttpGet]
        public async Task<IActionResult> ScheduleDiscussion(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.ManagerId != currentManager?.Id)
            {
                return NotFound("Review not found or you don't have permission to schedule this discussion.");
            }

            // Only allow scheduling if status is Manager_Review
            if (review.Status != "Manager_Review")
            {
                TempData["ErrorMessage"] = "Discussion can only be scheduled after manager review is completed.";
                return RedirectToAction("ReviewDetails", new { id });
            }

            ViewBag.Review = review;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleDiscussion(int id, DateTime scheduledDate, string? scheduledTime = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.ManagerId != currentManager?.Id)
            {
                return NotFound("Review not found or you don't have permission to schedule this discussion.");
            }

            // Validate date is in the future
            if (scheduledDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("scheduledDate", "Discussion date must be in the future.");
                ViewBag.Review = review;
                return View();
            }

            // Combine date and time if time is provided
            DateTime scheduledDateTime = scheduledDate;
            if (!string.IsNullOrEmpty(scheduledTime) && TimeSpan.TryParse(scheduledTime, out TimeSpan time))
            {
                scheduledDateTime = scheduledDate.Date.Add(time);
            }
            else
            {
                // Default to 2 PM if no time specified
                scheduledDateTime = scheduledDate.Date.AddHours(14);
            }

            // Update review with scheduled discussion date
            review.ScheduledDiscussionDate = scheduledDateTime;
            review.Status = "Discussion";
            review.DiscussionDate = DateTime.Now; // Mark that discussion phase has started

            await _context.SaveChangesAsync();

            // Send notification to employee
            if (review.Employee?.UserId != null)
            {
                await _notificationService.CreateNotificationAsync(
                    userId: review.Employee.UserId,
                    senderId: currentUserId,
                    title: "Discussion Session Scheduled",
                    message: $"Your manager has scheduled a discussion session for your performance review on {scheduledDateTime:MMMM dd, yyyy} at {scheduledDateTime:hh:mm tt}. Please prepare for the discussion.",
                    type: "Discussion_Scheduled",
                    actionUrl: $"/Employee/ReviewDetails/{id}",
                    relatedEntityId: id,
                    relatedEntityType: "PerformanceReview"
                );
            }

            TempData["SuccessMessage"] = $"Discussion session scheduled successfully for {scheduledDateTime:MMMM dd, yyyy} at {scheduledDateTime:hh:mm tt}. The employee has been notified.";
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

            // Get pending reviews (reviews that need manager attention)
            var pendingReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id && pr.Status == "Manager_Review")
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
        public async Task<IActionResult> ManagerReview(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentManager == null)
            {
                return NotFound("Manager record not found.");
            }

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.ManagerId != currentManager.Id)
            {
                return NotFound("Review not found or you don't have permission to review this.");
            }

            // Only allow reviewing if status is Manager_Review or Discussion
            if (review.Status != "Manager_Review" && review.Status != "Discussion")
            {
                TempData["ErrorMessage"] = "This review is not ready for manager review.";
                return RedirectToAction("PendingReviews");
            }

            ViewBag.CurrentManager = currentManager;
            ViewBag.Review = review;

            return View(review);
        }

        [HttpPost]
        public async Task<IActionResult> ManagerReview(int id, IFormCollection form)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.ManagerId != currentManager?.Id)
            {
                return NotFound();
            }

            // Get form values
            var action = form["action"].ToString();
            var managerAssessment = form["managerAssessment"].ToString();
            var overallRating = form["overallRating"].ToString();

            // Update manager assessment
            if (!string.IsNullOrEmpty(managerAssessment))
            {
                review.ManagerAssessment = managerAssessment;
            }

            // Update overall rating
            if (!string.IsNullOrEmpty(overallRating) && decimal.TryParse(overallRating, out decimal overallRatingValue))
            {
                review.OverallRating = overallRatingValue;
            }

            // Update key results with manager ratings and comments
            foreach (var keyResult in review.Objectives.SelectMany(o => o.KeyResults))
            {
                // Get manager rating for this key result
                var ratingKey = $"managerRatings[{keyResult.Id}]";
                if (form.ContainsKey(ratingKey))
                {
                    var ratingValue = form[ratingKey].ToString();
                    if (!string.IsNullOrEmpty(ratingValue) && int.TryParse(ratingValue, out int managerRating))
                    {
                        keyResult.ManagerRating = managerRating;
                        keyResult.ManagerRatedDate = DateTime.Now;
                    }
                }

                // Get manager comments for this key result
                var commentKey = $"managerComments[{keyResult.Id}]";
                if (form.ContainsKey(commentKey))
                {
                    var commentValue = form[commentKey].ToString();
                    if (!string.IsNullOrEmpty(commentValue))
                    {
                        keyResult.ManagerComments = commentValue;
                    }
                }
            }

            // Handle different actions
            if (action == "schedule_discussion")
            {
                // Redirect to schedule discussion page instead of scheduling directly
                return RedirectToAction("ScheduleDiscussion", new { id = id });
            }
            else if (action == "finalize")
            {
                // Check if manager has rated all key results
                var allKeyResults = review.Objectives.SelectMany(o => o.KeyResults).ToList();
                var unratedKeyResults = allKeyResults.Where(kr => !kr.ManagerRating.HasValue).ToList();

                if (unratedKeyResults.Any())
                {
                    TempData["ErrorMessage"] = $"Cannot finalize review. Please rate all {unratedKeyResults.Count} key result(s) before finalizing.";
                    return RedirectToAction("ManagerReview", new { id = id });
                }

                review.Status = "Completed";
                review.FinalizedDate = DateTime.Now;
                TempData["SuccessMessage"] = "Review finalized successfully. You can view it in your completed reviews.";
            }
            else
            {
                review.ManagerReviewedDate = DateTime.Now;
                TempData["SuccessMessage"] = "Manager review saved successfully. You can continue editing it from Pending Reviews.";
            }

            await _context.SaveChangesAsync();

            // Send notification to employee
            if (review.Employee?.UserId != null)
            {
                // Note: Notification service would be injected if needed
                // await _notificationService.CreateNotificationAsync(
                //     userId: review.Employee.UserId,
                //     senderId: currentUserId,
                //     title: "Manager Review Completed",
                //     message: $"Your manager has completed reviewing your performance review.",
                //     type: "Manager_Review_Completed",
                //     actionUrl: $"/Employee/ReviewDetails/{id}",
                //     relatedEntityId: id,
                //     relatedEntityType: "PerformanceReview"
                // );
            }

            // Redirect based on action taken
            if (action == "schedule_discussion" || action == "finalize")
            {
                // If scheduled for discussion or finalized, redirect to review details
                return RedirectToAction("ReviewDetails", new { id = id });
            }
            else
            {
                // If just saved, redirect back to pending reviews
                return RedirectToAction("PendingReviews");
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyReviews()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Manager record not found.");
            }

            // Get all reviews for this manager (all statuses)
            var allReviews = await _context.PerformanceReviews
                .Where(pr => pr.ManagerId == currentEmployee.Id)
                .Include(pr => pr.Employee)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .OrderByDescending(pr => pr.CreatedDate)
                .ToListAsync();

            ViewBag.AllReviews = allReviews;
            ViewBag.CurrentManager = currentEmployee;

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
                        
                        TempData["SuccessMessage"] = $"Employee '{model.FirstName} {model.LastName}' has been created successfully and can now log in! You can now create an OKR for them when ready.";
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
            var skippedEmployees = new List<string>();

            // Create a review for each selected employee
            foreach (var employee in selectedEmployees)
            {
                // Check if employee already has an active review
                var existingReview = await _context.PerformanceReviews
                    .FirstOrDefaultAsync(pr => pr.EmployeeId == employee.Id && 
                        (pr.Status == "Draft" || pr.Status == "Employee_Review" || pr.Status == "Manager_Review"));

                if (existingReview != null)
                {
                    var employeeName = string.IsNullOrWhiteSpace(employee.FirstName) && string.IsNullOrWhiteSpace(employee.LastName) 
                        ? employee.Email 
                        : $"{employee.FirstName} {employee.LastName}".Trim();
                    skippedEmployees.Add(employeeName);
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
                            Rating1Description = templateKeyResult.Rating1Description ?? "Needs Improvement",
                            Rating2Description = templateKeyResult.Rating2Description ?? "Below Expectations",
                            Rating3Description = templateKeyResult.Rating3Description ?? "Meets Expectations",
                            Rating4Description = templateKeyResult.Rating4Description ?? "Exceeds Expectations",
                            Rating5Description = templateKeyResult.Rating5Description ?? "Outstanding",
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

                // Send notification to employee about new OKR assignment
                if (employee.UserId != null)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId: employee.UserId,
                        senderId: currentUserId,
                        title: "New OKR Assigned",
                        message: $"Your manager has assigned you a new OKR for the period {model.ReviewPeriodStart:MMM dd, yyyy} - {model.ReviewPeriodEnd:MMM dd, yyyy}. Please review and complete it.",
                        type: "OKR_Assigned",
                        actionUrl: $"/Employee/MyActiveReviews",
                        relatedEntityId: performanceReview.Id,
                        relatedEntityType: "PerformanceReview"
                    );
                }
            }

            await _context.SaveChangesAsync();

            // Prepare appropriate messages based on results
            if (createdReviews.Any() && skippedEmployees.Any())
            {
                // Some created, some skipped
                TempData["SuccessMessage"] = $"Successfully created {createdReviews.Count} performance review(s) based on the {template.Name} template. Notifications sent to employees.";
                TempData["WarningMessage"] = $"Skipped {skippedEmployees.Count} employee(s) who already have active reviews: {string.Join(", ", skippedEmployees)}.";
                
                // Send notification to manager about partial success
                await _notificationService.CreateNotificationAsync(
                    userId: currentUserId,
                    senderId: currentUserId,
                    title: "Partial Review Creation Success",
                    message: $"Successfully created {createdReviews.Count} review(s), but skipped {skippedEmployees.Count} employee(s) who already have active reviews: {string.Join(", ", skippedEmployees)}.",
                    type: "Review_Creation_Partial",
                    actionUrl: "/Manager/Index",
                    relatedEntityId: null,
                    relatedEntityType: "ReviewCreation"
                );
            }
            else if (createdReviews.Any())
            {
                // All created successfully
                TempData["SuccessMessage"] = $"Successfully created {createdReviews.Count} performance review(s) based on the {template.Name} template. Notifications sent to employees.";
            }
            else if (skippedEmployees.Any())
            {
                // All were skipped
                TempData["ErrorMessage"] = $"No reviews were created. All selected employees already have active reviews: {string.Join(", ", skippedEmployees)}.";
                
                // Send notification to manager about failed review creation
                await _notificationService.CreateNotificationAsync(
                    userId: currentUserId,
                    senderId: currentUserId,
                    title: "Review Creation Failed",
                    message: $"No reviews were created. All selected employees already have active reviews: {string.Join(", ", skippedEmployees)}.",
                    type: "Review_Creation_Failed",
                    actionUrl: "/Manager/CreateTemplateReview",
                    relatedEntityId: null,
                    relatedEntityType: "ReviewCreation"
                );
            }
            else
            {
                // No employees selected (shouldn't happen due to earlier validation)
                TempData["ErrorMessage"] = "No reviews were created. Please select at least one team member.";
            }

            return RedirectToAction("Index");
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

        [HttpGet]
        public async Task<IActionResult> EditEmployee(int id)
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

            // Find the employee to edit (must be managed by current manager)
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.Id == id && e.ManagerId == currentManager.Id);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found or you don't have permission to edit this employee.";
                return RedirectToAction("Index");
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
            // Get current manager
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentManager == null)
            {
                TempData["ErrorMessage"] = "Manager record not found.";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                // Find the employee to edit (must be managed by current manager)
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == model.Id && e.ManagerId == currentManager.Id);

                if (employee == null)
                {
                    TempData["ErrorMessage"] = "Employee not found or you don't have permission to edit this employee.";
                    return RedirectToAction("Index");
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

                var employeeName = string.IsNullOrWhiteSpace(employee.FirstName) && string.IsNullOrWhiteSpace(employee.LastName) 
                    ? employee.Email 
                    : $"{employee.FirstName} {employee.LastName}".Trim();
                TempData["SuccessMessage"] = $"Employee {employeeName} has been updated successfully.";
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
            // Get current manager
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentManager == null)
            {
                TempData["ErrorMessage"] = "Manager record not found.";
                return RedirectToAction("Index");
            }

            // Find the employee to delete
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.ManagerId == currentManager.Id);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found or you don't have permission to delete this employee.";
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

        [HttpGet]
        public async Task<IActionResult> DownloadPDF(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentManager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.ManagerId != currentManager?.Id)
            {
                return NotFound();
            }

            // TODO: Implement PDF generation
            // For now, return a view that can be printed as PDF
            return View("PerformanceReviewPDF", review);
        }

    }
}
