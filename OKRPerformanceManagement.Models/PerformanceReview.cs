using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class PerformanceReview
    {
        public int Id { get; set; }
        
        [Required]
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }
        
        [Required]
        public int ManagerId { get; set; }
        public Employee Manager { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Employee_Review, Manager_Review, Discussion, Signed, Completed
        
        [Required]
        public DateTime ReviewPeriodStart { get; set; }
        
        [Required]
        public DateTime ReviewPeriodEnd { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ManagerReviewedDate { get; set; }
        public DateTime? DiscussionDate { get; set; }
        public DateTime? FinalizedDate { get; set; }
        
        [StringLength(2000)]
        public string EmployeeSelfAssessment { get; set; }
        
        [StringLength(2000)]
        public string ManagerAssessment { get; set; }
        
        [StringLength(2000)]
        public string FinalAssessment { get; set; }
        
        [StringLength(2000)]
        public string DiscussionNotes { get; set; }
        
        public decimal? OverallRating { get; set; }
        
        // Digital Signatures
        [StringLength(500)]
        public string EmployeeSignature { get; set; }
        
        public DateTime? EmployeeSignedDate { get; set; }
        
        [StringLength(500)]
        public string ManagerSignature { get; set; }
        
        public DateTime? ManagerSignedDate { get; set; }
        
        // OKR Template Reference
        public int? OKRTemplateId { get; set; }
        public OKRTemplate? OKRTemplate { get; set; }
        
        // Navigation Properties
        public ICollection<Objective> Objectives { get; set; } = new List<Objective>();
        public ICollection<ReviewComment> Comments { get; set; } = new List<ReviewComment>();
    }
}