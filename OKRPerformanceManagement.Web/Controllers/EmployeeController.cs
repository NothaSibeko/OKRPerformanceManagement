using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Web.Services;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    public class KeyResultUpdate
    {
        public int Id { get; set; }
        public string? EmployeeComments { get; set; }
        public int? EmployeeRating { get; set; }
    }
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public EmployeeController(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }


        [HttpGet]
        public async Task<IActionResult> ReviewDetails(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            return View(review);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSelfAssessment(int id, string employeeSelfAssessment, Dictionary<int, int> employeeRatings, Dictionary<int, string> employeeComments)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.Manager)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            review.EmployeeSelfAssessment = employeeSelfAssessment;
            review.Status = "Manager_Review";
            review.SubmittedDate = DateTime.Now;

            // Update employee ratings and comments for key results
            foreach (var kvp in employeeRatings)
            {
                var keyResult = review.Objectives
                    .SelectMany(o => o.KeyResults)
                    .FirstOrDefault(kr => kr.Id == kvp.Key);

                if (keyResult != null)
                {
                    keyResult.EmployeeRating = kvp.Value;
                    keyResult.EmployeeRatedDate = DateTime.Now;
                }
            }

            foreach (var kvp in employeeComments)
            {
                var keyResult = review.Objectives
                    .SelectMany(o => o.KeyResults)
                    .FirstOrDefault(kr => kr.Id == kvp.Key);

                if (keyResult != null)
                {
                    keyResult.EmployeeComments = kvp.Value;
                }
            }

            await _context.SaveChangesAsync();

            // Send notification to manager
            if (review.Manager?.UserId != null)
            {
                await _notificationService.CreateNotificationAsync(
                    userId: review.Manager.UserId,
                    senderId: currentUserId,
                    title: "Performance Review Submitted",
                    message: $"{currentEmployee.FirstName} {currentEmployee.LastName} has submitted their performance review for your review.",
                    type: "Review_Submitted",
                    actionUrl: $"/Manager/ReviewDetails/{id}",
                    relatedEntityId: id,
                    relatedEntityType: "PerformanceReview"
                );
            }

            TempData["SuccessMessage"] = "Your performance review has been submitted successfully! Your manager will be notified.";

            return RedirectToAction("MyActiveReviews");
        }

        [HttpPost]
        public async Task<IActionResult> EmployeeSignOff(int id, string employeeSignature)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            review.EmployeeSignature = employeeSignature;
            review.EmployeeSignedDate = DateTime.Now;

            // If both employee and manager have signed, mark as completed
            if (!string.IsNullOrEmpty(review.ManagerSignature))
            {
                review.Status = "Completed";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyActiveReviews");
        }

        [HttpGet]
        public async Task<IActionResult> MyPerformanceHistory()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            var completedReviews = await _context.PerformanceReviews
                .Where(pr => pr.EmployeeId == currentEmployee.Id && pr.Status == "Completed")
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .OrderByDescending(pr => pr.FinalizedDate)
                .ToListAsync();

            ViewBag.CurrentEmployee = currentEmployee;
            ViewBag.CompletedReviews = completedReviews;
            ViewBag.UserRole = "Employee";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MyActiveReviews()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            // Get active reviews (only Draft and Employee_Review - reviews the employee can work on)
            var activeReviews = await _context.PerformanceReviews
                .Where(pr => pr.EmployeeId == currentEmployee.Id && 
                    (pr.Status == "Draft" || pr.Status == "Employee_Review"))
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .OrderByDescending(pr => pr.CreatedDate)
                .ToListAsync();

            ViewBag.CurrentEmployee = currentEmployee;
            ViewBag.ActiveReviews = activeReviews;
            ViewBag.UserRole = "Employee";

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> MyOKRs()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            var myOKRs = await _context.PerformanceReviews
                .Where(pr => pr.EmployeeId == currentEmployee.Id)
                .Include(pr => pr.Manager)
                .Include(pr => pr.OKRTemplate)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .OrderByDescending(pr => pr.CreatedDate)
                .ToListAsync();

            ViewBag.Employee = currentEmployee;
            ViewBag.MyOKRs = myOKRs;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ViewOKR(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .Include(pr => pr.Comments)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            // Debug information
            ViewBag.DebugInfo = $"Current User ID: {currentUserId}, Current Employee ID: {currentEmployee?.Id}, Review ID: {id}, Review Employee ID: {review?.EmployeeId}";

            if (review == null)
            {
                ViewBag.ErrorMessage = "Review not found in database";
                return View();
            }

            if (currentEmployee == null)
            {
                ViewBag.ErrorMessage = "Current employee not found";
                return View();
            }

            if (review.EmployeeId != currentEmployee.Id)
            {
                ViewBag.ErrorMessage = $"Review belongs to employee {review.EmployeeId} but current user is employee {currentEmployee.Id}";
                return View();
            }

            ViewBag.Employee = currentEmployee;
            ViewBag.OKR = review;

            return View(review);
        }

        [HttpGet]
        public async Task<IActionResult> EditOKR(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            // Only allow editing if status is Draft or Employee_Review
            if (review.Status != "Draft" && review.Status != "Employee_Review")
            {
                return BadRequest("This OKR cannot be edited at this time.");
            }

            ViewBag.Employee = currentEmployee;
            ViewBag.OKR = review;

            return View(review);
        }

        [HttpPost]
        public async Task<IActionResult> EditOKR(int id, string action, string EmployeeSelfAssessment, List<KeyResultUpdate> KeyResults)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.Manager)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            // Only allow editing if status is Draft or Employee_Review
            if (review.Status != "Draft" && review.Status != "Employee_Review")
            {
                return BadRequest("This OKR cannot be edited at this time.");
            }

            // Update self assessment
            if (!string.IsNullOrEmpty(EmployeeSelfAssessment))
            {
                review.EmployeeSelfAssessment = EmployeeSelfAssessment;
            }

            // Update key results
            if (KeyResults != null)
            {
                foreach (var keyResultUpdate in KeyResults)
                {
                    var keyResult = review.Objectives
                        .SelectMany(o => o.KeyResults)
                        .FirstOrDefault(kr => kr.Id == keyResultUpdate.Id);

                    if (keyResult != null)
                    {
                        if (!string.IsNullOrEmpty(keyResultUpdate.EmployeeComments))
                        {
                            keyResult.EmployeeComments = keyResultUpdate.EmployeeComments;
                        }
                        
                        if (keyResultUpdate.EmployeeRating.HasValue)
                        {
                            keyResult.EmployeeRating = keyResultUpdate.EmployeeRating;
                            keyResult.EmployeeRatedDate = DateTime.Now;
                        }
                    }
                }
            }

            // Handle different actions
            if (action == "submit")
            {
                review.Status = "Manager_Review";
                review.SubmittedDate = DateTime.Now;
                
                // Send notification to manager
                if (review.Manager?.UserId != null)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId: review.Manager.UserId,
                        senderId: currentUserId,
                        title: "Performance Review Submitted",
                        message: $"{currentEmployee.FirstName} {currentEmployee.LastName} has submitted their performance review for your review.",
                        type: "Review_Submitted",
                        actionUrl: $"/Manager/ReviewDetails/{id}",
                        relatedEntityId: id,
                        relatedEntityType: "PerformanceReview"
                    );
                }
                
                TempData["SuccessMessage"] = "OKR submitted for manager review successfully! Your manager will be notified.";
            }
            else
            {
                TempData["SuccessMessage"] = "OKR saved successfully!";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyActiveReviews");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (currentEmployee == null)
            {
                return NotFound("Employee record not found.");
            }

            ViewBag.Employee = currentEmployee;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPDF(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            var review = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Include(pr => pr.Objectives)
                    .ThenInclude(o => o.KeyResults)
                .Include(pr => pr.OKRTemplate)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (review == null || review.EmployeeId != currentEmployee?.Id)
            {
                return NotFound();
            }

            // TODO: Implement PDF generation
            // For now, return a view that can be printed as PDF
            return View("PerformanceReviewPDF", review);
        }
    }
}
