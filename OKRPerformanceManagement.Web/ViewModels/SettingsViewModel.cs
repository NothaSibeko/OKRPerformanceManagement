using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class SettingsViewModel
    {
        [Display(Name = "Notifications Enabled")]
        public bool NotificationsEnabled { get; set; }

        [Display(Name = "Theme")]
        public string Theme { get; set; } = "Light";

        [Display(Name = "Language")]
        public string Language { get; set; } = "English";

        [Display(Name = "Time Zone")]
        public string TimeZone { get; set; } = "UTC";
    }
}
