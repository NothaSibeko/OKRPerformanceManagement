using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Models
{
    public class OKRTemplate
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Role { get; set; } // Administration, Support_Systems Engineer, Snr and Technical Team Leads, Manager, Consultant
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public int? RoleId { get; set; }
        public EmployeeRole? RoleEntity { get; set; }
        
        // Navigation Properties
        public ICollection<OKRTemplateObjective> Objectives { get; set; } = new List<OKRTemplateObjective>();
    }
}