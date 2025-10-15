using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class NotificationsViewModel
    {
        [Display(Name = "Email Notifications")]
        public bool EmailNotifications { get; set; }

        [Display(Name = "Push Notifications")]
        public bool PushNotifications { get; set; }

        [Display(Name = "Review Reminders")]
        public bool ReviewReminders { get; set; }

        [Display(Name = "Goal Deadlines")]
        public bool GoalDeadlines { get; set; }

        [Display(Name = "Weekly Reports")]
        public bool WeeklyReports { get; set; }
    }
}
