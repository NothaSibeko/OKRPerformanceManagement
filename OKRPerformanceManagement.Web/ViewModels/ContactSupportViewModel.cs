using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class ContactSupportViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        [Display(Name = "Message")]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Medium";

        [Display(Name = "Category")]
        public string Category { get; set; } = "General";
    }
}
