using System;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Data
{
    public class ControlPlaneDbContext : DbContext
    {
        public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Release> Releases => Set<Release>();
        public DbSet<ReleaseArtifact> ReleaseArtifacts => Set<ReleaseArtifact>();
        public DbSet<UpdatePolicy> UpdatePolicies => Set<UpdatePolicy>();
        public DbSet<ExtensionRelease> ExtensionReleases => Set<ExtensionRelease>();
        public DbSet<FeatureEntitlement> FeatureEntitlements => Set<FeatureEntitlement>();
        public DbSet<Ban> Bans => Set<Ban>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<AdminAction> AdminActions => Set<AdminAction>();
        public DbSet<AdminRecoveryCode> RecoveryCodes => Set<AdminRecoveryCode>();
        public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<License> Licenses => Set<License>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<DownloadRecord> DownloadRecords => Set<DownloadRecord>();
        public DbSet<WebsiteEvent> WebsiteEvents => Set<WebsiteEvent>();
        public DbSet<WebsiteContent> WebsiteContents => Set<WebsiteContent>();
        public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
        public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
        public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
        public DbSet<SystemHealthSnapshot> SystemHealthSnapshots => Set<SystemHealthSnapshot>();
        public DbSet<SystemMetric> SystemMetrics => Set<SystemMetric>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.RecoveryEmail);
                entity.HasIndex(e => e.GoogleSubjectId);
                entity.Property(e => e.Role).HasConversion<string>();
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // Device configuration
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.InstallationId).IsUnique();
                entity.Property(e => e.ClientType).HasConversion<string>();
                entity.Property(e => e.OsVersion).HasMaxLength(100);
                entity.Property(e => e.AppVersion).HasMaxLength(50);
                entity.Property(e => e.CoarseCountryCode).HasMaxLength(10);
            });

            // Session configuration
            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AccessTokenHash);
                entity.HasIndex(e => e.FamilyId);
                entity.HasIndex(e => new { e.UserId, e.IsRevoked });
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Sessions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.Sessions)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TelemetryEvent configuration
            modelBuilder.Entity<TelemetryEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TimestampUtc);
                entity.HasIndex(e => new { e.DeviceId, e.EventName });
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.TelemetryEvents)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TimestampUtc);
                entity.HasIndex(e => e.CorrelationId);
                entity.HasIndex(e => new { e.ActorId, e.Action });
            });

            // Release configuration
            modelBuilder.Entity<Release>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Platform, e.Version }).IsUnique();
                entity.HasIndex(e => new { e.Platform, e.Channel, e.IsPublished });
                entity.Property(e => e.Platform).HasConversion<string>();
                entity.Property(e => e.Severity).HasConversion<string>();
                entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ReleaseArtifact configuration
            modelBuilder.Entity<ReleaseArtifact>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Sha256Hash);
                entity.HasIndex(e => new { e.ReleaseId, e.Architecture });
                entity.HasOne(e => e.Release)
                    .WithMany(r => r.Artifacts)
                    .HasForeignKey(e => e.ReleaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UpdatePolicy configuration
            modelBuilder.Entity<UpdatePolicy>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Platform, e.Channel, e.IsActive });
                entity.Property(e => e.Platform).HasConversion<string>();
            });

            // ExtensionRelease configuration
            modelBuilder.Entity<ExtensionRelease>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Browser, e.ExtensionVersion }).IsUnique();
                entity.Property(e => e.Browser).HasConversion<string>();
            });

            // FeatureEntitlement configuration
            modelBuilder.Entity<FeatureEntitlement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.FeatureCode }).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.FeatureEntitlements)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Ban configuration
            modelBuilder.Entity<Ban>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TargetType, e.TargetValue, e.IsActive });
                entity.Property(e => e.TargetType).HasConversion<string>();
            });

            // RefreshToken configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasIndex(e => e.FamilyId);
                entity.HasIndex(e => new { e.SessionId, e.IsRevoked, e.IsUsed });
                entity.HasIndex(e => new { e.UserId, e.IsRevoked });
                entity.HasOne(e => e.Session)
                    .WithMany(s => s.RefreshTokens)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AdminAction configuration
            modelBuilder.Entity<AdminAction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TimestampUtc);
                entity.HasOne(e => e.AdminUser)
                    .WithMany(u => u.AdminActions)
                    .HasForeignKey(e => e.AdminUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AdminRecoveryCode configuration
            modelBuilder.Entity<AdminRecoveryCode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CodeHash);
                entity.HasIndex(e => new { e.UserId, e.IsUsed });
                entity.HasOne(e => e.User)
                    .WithMany(u => u.RecoveryCodes)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserPasskey configuration
            modelBuilder.Entity<UserPasskey>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CredentialId).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Passkeys)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Plan configuration
            modelBuilder.Entity<Plan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Tier).HasConversion<string>();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PriceMonthlyUsd).HasPrecision(18, 2);
                entity.Property(e => e.PriceYearlyUsd).HasPrecision(18, 2);
            });

            // License configuration
            modelBuilder.Entity<License>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LicenseKeyHash).IsUnique();
                entity.HasIndex(e => e.KeyPrefix);
                entity.HasIndex(e => new { e.UserId, e.Status });
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Licenses)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Plan)
                    .WithMany(p => p.Licenses)
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Subscription configuration
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ExternalSubscriptionId);
                entity.HasIndex(e => new { e.UserId, e.Status });
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Subscriptions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Plan)
                    .WithMany(p => p.Subscriptions)
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DownloadRecord configuration
            modelBuilder.Entity<DownloadRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DownloadedAtUtc);
                entity.HasIndex(e => new { e.ReleaseArtifactId, e.Status });
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.ReleaseArtifact)
                    .WithMany(a => a.DownloadRecords)
                    .HasForeignKey(e => e.ReleaseArtifactId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.License)
                    .WithMany(l => l.DownloadRecords)
                    .HasForeignKey(e => e.LicenseId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // WebsiteEvent configuration
            modelBuilder.Entity<WebsiteEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TimestampUtc);
                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => new { e.EventType, e.TimestampUtc });
                entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PagePath).HasMaxLength(255).IsRequired();
            });

            // WebsiteContent configuration
            modelBuilder.Entity<WebsiteContent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SectionKey, e.Locale }).IsUnique();
                entity.Property(e => e.SectionKey).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Locale).HasMaxLength(20).IsRequired();
                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PricingTier configuration
            modelBuilder.Entity<PricingTier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PlanId, e.SortOrder });
                entity.Property(e => e.MonthlyPrice).HasPrecision(18, 2);
                entity.Property(e => e.YearlyPrice).HasPrecision(18, 2);
                entity.HasOne(e => e.Plan)
                    .WithMany(p => p.PricingTiers)
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Announcement configuration
            modelBuilder.Entity<Announcement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.IsActive, e.StartsAtUtc, e.EndsAtUtc });
                entity.Property(e => e.Severity).HasConversion<string>();
                entity.Property(e => e.Audience).HasConversion<string>();
                entity.Property(e => e.TargetPlatform).HasConversion<string>();
            });

            // AdminNotification configuration
            modelBuilder.Entity<AdminNotification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.IsRead });
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.Property(e => e.Type).HasConversion<string>();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SupportTicket configuration
            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TicketNumber).IsUnique();
                entity.HasIndex(e => new { e.Status, e.Priority });
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.TicketNumber).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Category).HasConversion<string>();
                entity.Property(e => e.Priority).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.SupportTickets)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.AssignedAdmin)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedAdminId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SupportMessage configuration
            modelBuilder.Entity<SupportMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TicketId, e.CreatedAtUtc });
                entity.Property(e => e.SenderType).HasConversion<string>();
                entity.HasOne(e => e.Ticket)
                    .WithMany(t => t.Messages)
                    .HasForeignKey(e => e.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SystemHealthSnapshot configuration
            modelBuilder.Entity<SystemHealthSnapshot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ComponentName, e.CheckedAtUtc });
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.ComponentName).HasMaxLength(100).IsRequired();
            });

            // SystemMetric configuration
            modelBuilder.Entity<SystemMetric>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.MetricName, e.TimestampUtc });
                entity.Property(e => e.MetricName).HasMaxLength(100).IsRequired();
            });

            // RolePermission configuration
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Role, e.PermissionCode }).IsUnique();
                entity.Property(e => e.Role).HasConversion<string>();
                entity.Property(e => e.PermissionCode).HasMaxLength(100).IsRequired();
            });

            // UserPermissionOverride configuration
            modelBuilder.Entity<UserPermissionOverride>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.PermissionCode }).IsUnique();
                entity.Property(e => e.PermissionCode).HasMaxLength(100).IsRequired();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.PermissionOverrides)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
