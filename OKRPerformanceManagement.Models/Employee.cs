using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKRPerformanceManagement.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Role { get; set; } // Junior, Support, Senior, Manager, Consultant
        
        public int? RoleId { get; set; }
        public EmployeeRole? RoleEntity { get; set; }

        [Required]
        public string Position { get; set; }

        public int? ManagerId { get; set; }
        public Employee? Manager { get; set; }

        // Identity User ID for authentication
        public string? UserId { get; set; }

        // Additional fields for OKR system
        public string LineOfBusiness { get; set; } = "Digital Industries - CSI3";
        public string FinancialYear { get; set; } = "FY 2025";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public ICollection<Employee> Subordinates { get; set; } = new List<Employee>();
        public ICollection<PerformanceReview> PerformanceReviews { get; set; } = new List<PerformanceReview>();
        public ICollection<PerformanceReview> ManagedReviews { get; set; } = new List<PerformanceReview>();
    }
}
