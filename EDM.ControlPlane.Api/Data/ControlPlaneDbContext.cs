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
        public DbSet<SyncedFileRecord> SyncedFiles => Set<SyncedFileRecord>();
        public DbSet<RemoteCommand> RemoteCommands => Set<RemoteCommand>();
        public DbSet<LiveDownloadStatus> LiveDownloads => Set<LiveDownloadStatus>();
        public DbSet<SubscriptionPolicyRecord> SubscriptionPolicies => Set<SubscriptionPolicyRecord>();
        public DbSet<AdminOverrideRecord> AdminOverrides => Set<AdminOverrideRecord>();
        public DbSet<GeoPricingRuleRecord> GeoPricingRules => Set<GeoPricingRuleRecord>();
        public DbSet<GlobalSubscriptionConfigRecord> GlobalSubscriptionConfigs => Set<GlobalSubscriptionConfigRecord>();
        public DbSet<RegionPolicyRecord> RegionPolicies => Set<RegionPolicyRecord>();
        public DbSet<PromotionRecord> Promotions => Set<PromotionRecord>();
        public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
        public DbSet<WebhookEventRecord> WebhookEvents => Set<WebhookEventRecord>();
        public DbSet<CouponUsageRecord> CouponUsages => Set<CouponUsageRecord>();

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

            // SyncedFileRecord configuration
            modelBuilder.Entity<SyncedFileRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OwnerId);
                entity.HasIndex(e => new { e.OwnerId, e.RelativePath });
                entity.HasIndex(e => e.Sha256Hash);
                entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.RelativePath).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Sha256Hash).HasMaxLength(64).IsRequired();
                entity.Property(e => e.SyncState).HasConversion<string>();
                entity.HasOne(e => e.Owner)
                    .WithMany()
                    .HasForeignKey(e => e.OwnerId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // RemoteCommand configuration
            modelBuilder.Entity<RemoteCommand>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.DeviceId, e.Status });
                entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc });
                entity.Property(e => e.CommandType).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.TargetDownloadId).HasMaxLength(100);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // LiveDownloadStatus configuration
            modelBuilder.Entity<LiveDownloadStatus>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.DeviceId });
                entity.HasIndex(e => new { e.DeviceId, e.DownloadId }).IsUnique();
                entity.Property(e => e.DownloadId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed Default Geo-Pricing Rules
            modelBuilder.Entity<GeoPricingRuleRecord>().HasData(
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                    CountryCode = "BD",
                    Region = "South Asia",
                    Currency = "BDT",
                    CurrencySymbol = "\u09F3",
                    MonthlyPrice = 63.00m,
                    YearlyPrice = 599.00m,
                    Description = "Bangladesh Local Direct Pricing (63 BDT/mo)"
                },
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                    CountryCode = "IN",
                    Region = "South Asia",
                    Currency = "INR",
                    CurrencySymbol = "\u20B9",
                    MonthlyPrice = 63.00m,
                    YearlyPrice = 599.00m,
                    Description = "India Local Regional Pricing (63 INR/mo)"
                },
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                    CountryCode = "PK",
                    Region = "South Asia",
                    Currency = "PKR",
                    CurrencySymbol = "\u20A8",
                    MonthlyPrice = 63.00m,
                    YearlyPrice = 599.00m,
                    Description = "Pakistan Local Regional Pricing (63 PKR/mo)"
                },
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                    CountryCode = "ASIA",
                    Region = "Asia",
                    Currency = "USD",
                    CurrencySymbol = "$",
                    MonthlyPrice = 2.99m,
                    YearlyPrice = 24.99m,
                    Description = "Asian Countries Regional Tier ($2.99/mo)"
                },
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                    CountryCode = "US",
                    Region = "North America",
                    Currency = "USD",
                    CurrencySymbol = "$",
                    MonthlyPrice = 9.99m,
                    YearlyPrice = 79.99m,
                    Description = "North America Tier ($9.99/mo)"
                },
                new GeoPricingRuleRecord
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                    CountryCode = "GLOBAL",
                    Region = "Global",
                    Currency = "USD",
                    CurrencySymbol = "$",
                    MonthlyPrice = 4.99m,
                    YearlyPrice = 49.99m,
                    Description = "Global Fallback Tier ($4.99/mo)"
                }
            );
            // Seed Global Subscription Config
            modelBuilder.Entity<GlobalSubscriptionConfigRecord>().HasData(
                new GlobalSubscriptionConfigRecord
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    IsGlobalSubscriptionEnabled = true,
                    IsAsiaSubscriptionEnabled = true,
                    IsTrialEnabled = true,
                    DefaultTrialDurationDays = 10,
                    IsGracePeriodEnabled = true,
                    DefaultGraceDurationDays = 5,
                    OfflineGraceHours = 72,
                    MaxTurboConnections = 64,
                    MaxGraceConnections = 32,
                    MaxRestrictedConnections = 16,
                    PaymentSystemEnabled = false,
                    PaymentProvider = "None",
                    IsTestMode = true,
                    SupportedCurrencies = "BDT,INR,PKR,USD,EUR,GBP,ZAR,AED,SAR,JPY,CNY",
                    GlobalFeaturesJson = "{\"premium_download\":true,\"dynamic_segmentation\":true,\"max_connections_64\":true,\"hls\":true,\"dash\":true,\"torrent\":true,\"browser_integration\":true,\"remote_control\":true,\"advanced_scheduler\":true,\"media_quality_selector\":true}",
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByUsername = "System"
                }
            );

            // Seed Regional Policies
            modelBuilder.Entity<RegionPolicyRecord>().HasData(
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222201"), RegionName = "South Asia", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 1.99m, DefaultYearlyPrice = 18.99m, Description = "South Asia Region Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222202"), RegionName = "Asia", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 2.99m, DefaultYearlyPrice = 24.99m, Description = "Pan-Asia Region Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222203"), RegionName = "North America", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 9.99m, DefaultYearlyPrice = 79.99m, Description = "North America Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222204"), RegionName = "Europe", IsSubscriptionEnabled = true, DefaultCurrency = "EUR", DefaultCurrencySymbol = "€", DefaultMonthlyPrice = 7.99m, DefaultYearlyPrice = 69.99m, Description = "European Union Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222205"), RegionName = "Middle East", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 5.99m, DefaultYearlyPrice = 49.99m, Description = "Middle East Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222206"), RegionName = "Africa", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 2.99m, DefaultYearlyPrice = 24.99m, Description = "Africa Region Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222207"), RegionName = "South America", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 3.99m, DefaultYearlyPrice = 34.99m, Description = "Latin America Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222208"), RegionName = "Oceania", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 8.99m, DefaultYearlyPrice = 74.99m, Description = "Oceania & Australia Policy" },
                new RegionPolicyRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222209"), RegionName = "Global", IsSubscriptionEnabled = true, DefaultCurrency = "USD", DefaultCurrencySymbol = "$", DefaultMonthlyPrice = 4.99m, DefaultYearlyPrice = 49.99m, Description = "Worldwide Fallback Policy" }
            );

        }
    }
}
