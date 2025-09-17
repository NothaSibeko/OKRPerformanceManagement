using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Models
{
    public class EmployeeRole
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } // Administration, Support_Systems Engineer, Snr and Technical Team Leads, Manager, Consultant
        
        [Required]
        [StringLength(200)]
        public string Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Navigation Properties
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<OKRTemplate> OKRTemplates { get; set; } = new List<OKRTemplate>();
    }
}