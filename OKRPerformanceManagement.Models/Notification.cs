using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OKRPerformanceManagement.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // Who receives the notification
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [Required]
        public string SenderId { get; set; } // Who sent the notification
        [ForeignKey("SenderId")]
        public ApplicationUser Sender { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        [StringLength(50)]
        public string Type { get; set; } // "OKR_Submitted", "OKR_Assigned", "Review_Assigned", etc.

        [StringLength(100)]
        public string ActionUrl { get; set; } // URL to navigate when clicked

        public bool IsRead { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ReadDate { get; set; }

        // Optional: Reference to related entity
        public int? RelatedEntityId { get; set; } // Could be OKR ID, Review ID, etc.
        public string RelatedEntityType { get; set; } // "OKR", "Review", etc.
    }
}
