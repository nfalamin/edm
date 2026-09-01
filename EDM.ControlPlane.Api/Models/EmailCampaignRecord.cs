using System;
using System.ComponentModel.DataAnnotations;

namespace EDM.ControlPlane.Api.Models
{
    public class EmailCampaignRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TargetAudience { get; set; } = "All Users";

        [Required]
        public string Body { get; set; } = string.Empty;

        public int RecipientsCount { get; set; } = 0;

        public double OpenRatePct { get; set; } = 0.0;

        [MaxLength(50)]
        public string Status { get; set; } = "Sent";

        public DateTime? SentAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
