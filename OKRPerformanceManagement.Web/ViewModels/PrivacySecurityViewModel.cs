using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class PrivacySecurityViewModel
    {
        [Display(Name = "Two-Factor Authentication")]
        public bool TwoFactorEnabled { get; set; }

        [Display(Name = "Data Sharing")]
        public bool DataSharing { get; set; }

        [Display(Name = "Profile Visibility")]
        public string ProfileVisibility { get; set; } = "Private";

        [Display(Name = "Login Alerts")]
        public bool LoginAlerts { get; set; }
    }
}
