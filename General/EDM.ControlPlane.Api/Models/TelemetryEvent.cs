using System;

namespace EDM.ControlPlane.Api.Models
{
    public class TelemetryEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DeviceId { get; set; }
        public string EventName { get; set; } = string.Empty; // e.g. "download_completed", "video_detected"
        public string EventPayloadJson { get; set; } = "{}"; // Anonymized structured payload, zero credentials
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public Device? Device { get; set; }
    }
}
