using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class ViewProfileViewModel
    {
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Position")]
        public string Position { get; set; } = string.Empty;

        [Display(Name = "Manager")]
        public string ManagerName { get; set; } = string.Empty;

        [Display(Name = "Member Since")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Status")]
        public bool IsActive { get; set; }
    }
}
