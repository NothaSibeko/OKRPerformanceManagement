using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class ReviewComment
    {
        public int Id { get; set; }
        
        [Required]
        public int PerformanceReviewId { get; set; }
        public PerformanceReview PerformanceReview { get; set; }
        
        [Required]
        public int CommenterId { get; set; }
        public Employee Commenter { get; set; }
        
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        [Required]
        [StringLength(2000)]
        public string Comment { get; set; }
        
        [Required]
        [StringLength(50)]
        public string CommentType { get; set; } // Employee, Manager, Discussion, Final
    }
}