using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OKRPerformanceManagement.Web.Services;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);
            
            return Json(new { 
                notifications = notifications.Select(n => new {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    actionUrl = n.ActionUrl,
                    isRead = n.IsRead,
                    createdDate = n.CreatedDate.ToString("MMM dd, yyyy HH:mm"),
                    senderName = n.Sender?.FirstName + " " + n.Sender?.LastName
                }),
                unreadCount = await _notificationService.GetUnreadNotificationCountAsync(userId)
            });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notificationService.MarkAsReadAsync(id, userId);
            
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notificationService.MarkAllAsReadAsync(userId);
            
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
            
            return Json(new { count });
        }

    }
}
