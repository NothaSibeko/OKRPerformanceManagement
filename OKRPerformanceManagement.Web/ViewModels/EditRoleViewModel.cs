using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.ViewModels
{
    public class EditRoleViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Role Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
