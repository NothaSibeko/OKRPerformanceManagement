using System.ComponentModel.DataAnnotations;
using OKRPerformanceManagement.Models;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class CreateTemplateReviewViewModel
    {
        [Required]
        [Display(Name = "OKR Template")]
        public int OKRTemplateId { get; set; }
        
        [Required]
        [Display(Name = "Review Period Start")]
        [DataType(DataType.Date)]
        public DateTime ReviewPeriodStart { get; set; } = DateTime.Now;
        
        [Required]
        [Display(Name = "Review Period End")]
        [DataType(DataType.Date)]
        public DateTime ReviewPeriodEnd { get; set; } = DateTime.Now.AddMonths(3);
        
        [Display(Name = "Select Team Members")]
        public List<int> SelectedEmployeeIds { get; set; } = new List<int>();
        
        [Display(Name = "Review Description")]
        [StringLength(500)]
        public string? Description { get; set; }
        
        // For display purposes
        public List<OKRTemplate> AvailableTemplates { get; set; } = new List<OKRTemplate>();
        public List<Employee> AvailableEmployees { get; set; } = new List<Employee>();
        public string? SelectedTemplateName { get; set; }
    }
}
