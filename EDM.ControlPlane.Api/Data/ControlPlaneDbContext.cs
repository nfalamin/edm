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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
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
                entity.Property(e => e.Platform).HasConversion<string>();
                entity.Property(e => e.Severity).HasConversion<string>();
                entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
            });

            // ReleaseArtifact configuration
            modelBuilder.Entity<ReleaseArtifact>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Sha256Hash);
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
        }
    }
}
