using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class UserSettings
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        
        // General Settings
        public bool NotificationsEnabled { get; set; } = true;
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "English";
        public string TimeZone { get; set; } = "UTC";
        
        // Notification Settings
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = false;
        public bool ReviewReminders { get; set; } = true;
        public bool GoalDeadlines { get; set; } = true;
        public bool WeeklyReports { get; set; } = false;
        
        // Privacy & Security Settings
        public bool TwoFactorEnabled { get; set; } = false;
        public bool DataSharing { get; set; } = false;
        public string ProfileVisibility { get; set; } = "Private";
        public bool LoginAlerts { get; set; } = true;
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
