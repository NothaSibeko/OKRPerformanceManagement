using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class OKRTemplateObjective
    {
        public int Id { get; set; }
        
        [Required]
        public int OKRTemplateId { get; set; }
        public OKRTemplate OKRTemplate { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Name { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Weight { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Description { get; set; }
        
        public int SortOrder { get; set; }
        
        // Navigation Properties
        public ICollection<OKRTemplateKeyResult> KeyResults { get; set; } = new List<OKRTemplateKeyResult>();
    }
}