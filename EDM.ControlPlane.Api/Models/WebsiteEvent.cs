using System;

namespace EDM.ControlPlane.Api.Models
{
    public class WebsiteEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = "pageview"; // pageview, cta_click, download_started, download_completed
        public string SessionId { get; set; } = string.Empty;
        public string PagePath { get; set; } = "/";
        public string? PageTitle { get; set; }
        public string? Referrer { get; set; }
        public string? OperatingSystem { get; set; }
        public string? Browser { get; set; }
        public string? DeviceCategory { get; set; } // Desktop, Mobile, Tablet
        public string? CountryCode { get; set; }
        public string? ReleaseVersion { get; set; }
        public string? UserAgent { get; set; }
        public string? ClientIpCoarse { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
