using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class OKRTemplateKeyResult
    {
        public int Id { get; set; }
        
        [Required]
        public int OKRTemplateObjectiveId { get; set; }
        public OKRTemplateObjective OKRTemplateObjective { get; set; }
        
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
    }
}