using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class Objective
    {
        public int Id { get; set; }
        
        [Required]
        public int PerformanceReviewId { get; set; }
        public PerformanceReview PerformanceReview { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Name { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Weight { get; set; }
        
        [StringLength(1000)]
        public string Description { get; set; }
        
        public int SortOrder { get; set; }
        
        // Navigation Properties
        public ICollection<KeyResult> KeyResults { get; set; } = new List<KeyResult>();
    }
}