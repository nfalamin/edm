using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public static class Permissions
    {
        public const string All = "*";

        // User Management
        public const string UsersRead = "users.read";
        public const string UsersManage = "users.manage";

        // Subscription & Regional Policy Management
        public const string SubscriptionsRead = "subscriptions.read";
        public const string SubscriptionsManage = "subscriptions.manage";
        public const string SubscriptionGlobalSwitch = "subscriptions.globalswitch";
        public const string SubscriptionRegionalSwitch = "subscriptions.regionalswitch";
        public const string EntitlementsManage = "entitlements.manage";

        // Payments, Refunds & Reconciliation
        public const string PaymentsRead = "payments.read";
        public const string PaymentsManage = "payments.manage";
        public const string PaymentsRefund = "payments.refund";
        public const string PaymentsReconcile = "payments.reconcile";

        // System Settings & Security
        public const string SystemSettingsRead = "settings.read";
        public const string SystemSettingsWrite = "settings.write";
        public const string SettingsManage = "settings.manage";
        public const string SecurityManage = "security.manage";
        public const string AdminSecurityManage = "admin.security.manage";

        // Audit Logs
        public const string AuditLogsRead = "auditlogs.read";

        // Release Management & Rollback
        public const string ReleasesRead = "releases.read";
        public const string ReleasesCreate = "releases.create";
        public const string ReleasesPublish = "releases.publish";
        public const string ReleasesRollback = "releases.rollback";
        public const string ReleasesManage = "releases.manage";

        // Website & Pricing
        public const string WebsiteManage = "website.manage";
        public const string PricingRead = "pricing.read";
        public const string PricingManage = "pricing.manage";

        // License Management
        public const string LicensesRead = "licenses.read";
        public const string LicensesManage = "licenses.manage";

        // Support & Helpdesk
        public const string SupportManage = "support.manage";
        public const string AnnouncementsManage = "announcements.manage";

        // Analytics & Telemetry
        public const string AnalyticsRead = "analytics.read";

        // System Health & Diagnostics
        public const string SystemHealthRead = "system.health.read";

        public static readonly IReadOnlyList<string> AllPermissions = new[]
        {
            UsersRead, UsersManage,
            SubscriptionsRead, SubscriptionsManage, SubscriptionGlobalSwitch, SubscriptionRegionalSwitch, EntitlementsManage,
            PaymentsRead, PaymentsManage, PaymentsRefund, PaymentsReconcile,
            SystemSettingsRead, SystemSettingsWrite, SettingsManage, SecurityManage, AdminSecurityManage,
            AuditLogsRead,
            ReleasesRead, ReleasesCreate, ReleasesPublish, ReleasesRollback,
            WebsiteManage, PricingRead, PricingManage,
            LicensesRead, LicensesManage,
            SupportManage, AnnouncementsManage,
            AnalyticsRead,
            SystemHealthRead
        };
    }
}
