using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public static class Permissions
    {
        public const string All = "*";

        // User Management
        public const string UsersRead = "users.read";
        public const string UsersManage = "users.manage";

        // Release Management & Rollback
        public const string ReleasesRead = "releases.read";
        public const string ReleasesCreate = "releases.create";
        public const string ReleasesPublish = "releases.publish";
        public const string ReleasesRollback = "releases.rollback";

        // Website & Pricing
        public const string WebsiteManage = "website.manage";
        public const string PricingManage = "pricing.manage";

        // License Management
        public const string LicensesManage = "licenses.manage";

        // Support & Helpdesk
        public const string SupportManage = "support.manage";

        // Analytics & Telemetry
        public const string AnalyticsRead = "analytics.read";

        // Settings & Security
        public const string SettingsManage = "settings.manage";
        public const string SecurityManage = "security.manage";

        // System Health & Diagnostics
        public const string SystemHealthRead = "system.health.read";

        public static readonly IReadOnlyList<string> AllPermissions = new[]
        {
            UsersRead, UsersManage,
            ReleasesRead, ReleasesCreate, ReleasesPublish, ReleasesRollback,
            WebsiteManage, PricingManage,
            LicensesManage,
            SupportManage,
            AnalyticsRead,
            SettingsManage, SecurityManage,
            SystemHealthRead
        };
    }
}
