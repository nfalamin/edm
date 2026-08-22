using System;
using System.Collections.Generic;
using EDM.Services;

namespace EDM.Models
{
    public class DownloadProfile
    {
        public string ProfileId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Default Profile";
        public string DefaultCategory { get; set; } = "General";
        public string DefaultSubFolder { get; set; } = "General";
        public string DefaultQueueId { get; set; } = "default";
        public DownloadPriority DefaultPriority { get; set; } = DownloadPriority.Normal;
        public int SpeedLimitKbps { get; set; } = 0;
        public bool AutoStart { get; set; } = true;
    }

    public class DownloadRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New Rule";
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; } = 0;

        // Match Conditions (empty = match all / unconstrained)
        public List<string> Extensions { get; set; } = new();
        public List<string> MimeTypes { get; set; } = new();
        public List<string> Domains { get; set; } = new();
        public List<string> UrlPatterns { get; set; } = new();
        public IngestionSource? MatchingSource { get; set; }

        // Actions & Targets
        public string TargetCategory { get; set; } = "General";
        public string TargetSubFolder { get; set; } = "General";
        public string? TargetQueueId { get; set; }
        public DownloadPriority? TargetPriority { get; set; }
        public string? ProfileId { get; set; }
        public int? SpeedLimitKbps { get; set; }
        public bool? AutoStart { get; set; }
    }

    public class RuleResolutionResult
    {
        public string Category { get; set; } = "General";
        public string DestinationPath { get; set; } = string.Empty;
        public string QueueId { get; set; } = "default";
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        public string? AppliedRuleId { get; set; }
        public string? AppliedProfileId { get; set; }
        public int SpeedLimitKbps { get; set; } = 0;
        public bool AutoStart { get; set; } = true;
    }
}
