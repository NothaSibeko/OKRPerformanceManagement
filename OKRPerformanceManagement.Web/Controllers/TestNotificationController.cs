using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OKRPerformanceManagement.Web.Services;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize]
    public class TestNotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public TestNotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTestNotification()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Create a test notification
            await _notificationService.CreateNotificationAsync(
                userId: currentUserId,
                senderId: currentUserId, // Self notification for testing
                title: "Test OKR Submission",
                message: "You have successfully submitted your OKR for Q4 2024. Your manager will review it shortly.",
                type: "OKR_Submitted",
                actionUrl: "/Employee/MyOKRs"
            );

            TempData["SuccessMessage"] = "Test notification created! Check the bell icon in the top navigation.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> CreateManagerNotification()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Create a test notification for manager
            await _notificationService.CreateNotificationAsync(
                userId: currentUserId,
                senderId: currentUserId, // Self notification for testing
                title: "New OKR Submission",
                message: "Junior 1 Junior Small has submitted their OKR for review. Please review and provide feedback.",
                type: "OKR_Submitted",
                actionUrl: "/Manager/PendingReviews"
            );

            TempData["SuccessMessage"] = "Manager notification created! Check the bell icon in the top navigation.";
            return RedirectToAction("Index", "Home");
        }
    }
}
