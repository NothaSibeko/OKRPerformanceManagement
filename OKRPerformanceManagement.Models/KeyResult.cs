using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class KeyResult
    {
        public int Id { get; set; }
        
        [Required]
        public int ObjectiveId { get; set; }
        public Objective Objective { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Name { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Target { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Measure { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Objectives { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string MeasurementSource { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Weight { get; set; }
        
        public int SortOrder { get; set; }
        
        // Rating Descriptions
        [Required]
        [StringLength(2000)]
        public string Rating1Description { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Rating2Description { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Rating3Description { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Rating4Description { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Rating5Description { get; set; }
        
        // Ratings
        public int? EmployeeRating { get; set; }
        public int? ManagerRating { get; set; }
        public int? FinalRating { get; set; }
        
        // Comments
        [StringLength(2000)]
        public string EmployeeComments { get; set; }
        
        [StringLength(2000)]
        public string ManagerComments { get; set; }
        
        [StringLength(2000)]
        public string FinalComments { get; set; }
        
        [StringLength(2000)]
        public string DiscussionNotes { get; set; }
        
        // Rating Dates
        public DateTime? EmployeeRatedDate { get; set; }
        public DateTime? ManagerRatedDate { get; set; }
        public DateTime? FinalRatedDate { get; set; }
    }
}