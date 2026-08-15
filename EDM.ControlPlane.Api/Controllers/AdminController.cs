using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record BanRequestDto(BanTargetType TargetType, string TargetValue, string Reason, int? DurationDays);
    public record UnbanRequestDto(BanTargetType TargetType, string TargetValue);
    
    public record CreateReleaseArtifactDto(string ArtifactName, string DownloadUrl, string Sha256Hash, long FileSizeBytes, string? SignatureBase64);
    public record CreateReleaseDto(
        ClientType Platform,
        string Version,
        string MinimumSupportedVersion,
        string Title,
        string ReleaseNotes,
        bool IsMandatory,
        ReleaseSeverity Severity,
        List<CreateReleaseArtifactDto> Artifacts);

    [ApiController]
    [Route("api/v1/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IBanEnforcementService _banService;
        private readonly IAuthService _authService;
        private readonly IAuditLoggingService _auditLogger;

        public AdminController(
            ControlPlaneDbContext dbContext,
            IBanEnforcementService banService,
            IAuthService authService,
            IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _banService = banService ?? throw new ArgumentNullException(nameof(banService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        // ==========================================
        // 1. DASHBOARD SUMMARY
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST,SUPPORT,RELEASE_MANAGER")]
        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var totalUsers = await _dbContext.Users.CountAsync();
            var activeUsers = await _dbContext.Users.CountAsync(u => u.IsActive);
            var registeredDevices = await _dbContext.Devices.CountAsync();
            var activeSessions = await _dbContext.Sessions.CountAsync(s => !s.IsRevoked && s.ExpiresAtUtc > now);
            
            var totalDownloads = await _dbContext.TelemetryEvents.CountAsync(t => t.EventName == "download_completed");
            var downloadsToday = await _dbContext.TelemetryEvents.CountAsync(t => t.EventName == "download_completed" && t.TimestampUtc >= todayStart);
            
            var latestRelease = await _dbContext.Releases
                .Where(r => !r.IsWithdrawn)
                .OrderByDescending(r => r.PublishedAtUtc)
                .Select(r => r.Version)
                .FirstOrDefaultAsync() ?? "None";

            var pendingUpdates = await _dbContext.Releases.CountAsync(r => r.IsWithdrawn);
            var securityEvents = await _dbContext.AuditLogs.CountAsync(a => a.ResultStatus == "DENIED" || a.Action.Contains("BAN") || a.Action.Contains("REUSE"));
            var bannedAccounts = await _dbContext.Bans.CountAsync(b => b.IsActive && (b.ExpiresAtUtc == null || b.ExpiresAtUtc > now));

            return Ok(new
            {
                totalUsers,
                activeUsers,
                registeredDevices,
                activeSessions,
                totalDownloads,
                downloadsToday,
                currentRelease = latestRelease,
                pendingUpdates,
                securityEvents,
                bannedAccounts,
                serverTimeUtc = DateTime.UtcNow
            });
        }

        // ==========================================
        // 2. ANALYTICS METRICS & RANGES
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/downloads")]
        public async Task<IActionResult> GetDownloadAnalyticsAsync([FromQuery] string range = "7d")
        {
            var (startDate, bucketDays) = ParseRange(range);
            var events = await _dbContext.TelemetryEvents
                .Where(t => (t.EventName == "download_completed" || t.EventName == "download_failed") && t.TimestampUtc >= startDate)
                .Select(t => new { t.EventName, t.TimestampUtc })
                .ToListAsync();

            var groups = events
                .GroupBy(e => e.TimestampUtc.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key,
                    completed = g.Count(x => x.EventName == "download_completed"),
                    failed = g.Count(x => x.EventName == "download_failed")
                })
                .ToList();

            return Ok(new { range, data = groups });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/users")]
        public async Task<IActionResult> GetUserGrowthAnalyticsAsync([FromQuery] string range = "30d")
        {
            var (startDate, _) = ParseRange(range);
            var users = await _dbContext.Users
                .Where(u => u.CreatedAtUtc >= startDate)
                .Select(u => u.CreatedAtUtc)
                .ToListAsync();

            var groups = users
                .GroupBy(u => u.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new { date = g.Key, count = g.Count() })
                .ToList();

            return Ok(new { range, data = groups });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/versions")]
        public async Task<IActionResult> GetVersionDistributionAsync()
        {
            var devices = await _dbContext.Devices
                .GroupBy(d => d.AppVersion)
                .Select(g => new { version = string.IsNullOrEmpty(g.Key) ? "Unknown" : g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(devices);
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/platforms")]
        public async Task<IActionResult> GetPlatformDistributionAsync()
        {
            var platforms = await _dbContext.Devices
                .GroupBy(d => d.ClientType)
                .Select(g => new { platform = g.Key.ToString(), count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(platforms);
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/activity")]
        public async Task<IActionResult> GetHourlyActivityAsync()
        {
            var now = DateTime.UtcNow;
            var past24h = now.AddHours(-24);

            var events = await _dbContext.TelemetryEvents
                .Where(t => t.TimestampUtc >= past24h)
                .Select(t => t.TimestampUtc.Hour)
                .ToListAsync();

            var hourly = Enumerable.Range(0, 24)
                .Select(hour => new { hour, count = events.Count(h => h == hour) })
                .ToList();

            return Ok(hourly);
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("analytics/security")]
        public async Task<IActionResult> GetSecurityAnalyticsAsync()
        {
            var past30d = DateTime.UtcNow.AddDays(-30);
            var logs = await _dbContext.AuditLogs
                .Where(a => a.TimestampUtc >= past30d)
                .GroupBy(a => a.Action)
                .Select(g => new { action = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(logs);
        }

        // ==========================================
        // 3. USER MANAGEMENT & BAN WORKFLOW
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsersAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(u => u.Username.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    Role = u.Role.ToString(),
                    u.IsActive,
                    u.CreatedAtUtc,
                    deviceCount = _dbContext.Sessions.Where(s => s.UserId == u.Id).Select(s => s.DeviceId).Distinct().Count(),
                    sessionCount = _dbContext.Sessions.Count(s => s.UserId == u.Id && !s.IsRevoked)
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, users });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserByIdAsync(Guid id)
        {
            var user = await _dbContext.Users
                .Include(u => u.FeatureEntitlements)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound(new { error = "USER_NOT_FOUND", message = "User not found." });

            var sessions = await _dbContext.Sessions
                .Where(s => s.UserId == id)
                .OrderByDescending(s => s.LastActivityAtUtc)
                .Take(10)
                .ToListAsync();

            var ban = await _dbContext.Bans
                .Where(b => b.TargetType == BanTargetType.UserId && b.TargetValue == id.ToString() && b.IsActive)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                Role = user.Role.ToString(),
                user.IsActive,
                user.CreatedAtUtc,
                isBanned = ban != null,
                banReason = ban?.Reason,
                entitlements = user.FeatureEntitlements.Select(f => f.FeatureCode),
                recentSessions = sessions.Select(s => new
                {
                    s.Id,
                    s.DeviceId,
                    s.UserAgent,
                    s.CoarseIpAddress,
                    s.LastActivityAtUtc,
                    s.IsRevoked
                })
            });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        [HttpPost("ban")]
        public async Task<IActionResult> BanTargetAsync([FromBody] BanRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetValue))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Target value is required." });
            }

            var adminName = User.Identity?.Name ?? "ADMIN";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            DateTime? expires = request.DurationDays.HasValue ? DateTime.UtcNow.AddDays(request.DurationDays.Value) : null;

            await _banService.BanTargetAsync(request.TargetType, request.TargetValue, request.Reason, adminName, expires);

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "ADMIN_BAN_ISSUED",
                targetEntity: request.TargetType.ToString(),
                targetId: request.TargetValue,
                detailsJson: $"{{\"reason\":\"{request.Reason}\",\"expires\":\"{expires}\"}}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = $"Ban applied successfully to {request.TargetType}: {request.TargetValue}" });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        [HttpPost("unban")]
        public async Task<IActionResult> UnbanTargetAsync([FromBody] UnbanRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetValue))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Target value is required." });
            }

            var adminName = User.Identity?.Name ?? "ADMIN";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            await _banService.UnbanTargetAsync(request.TargetType, request.TargetValue);

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "ADMIN_UNBAN_ISSUED",
                targetEntity: request.TargetType.ToString(),
                targetId: request.TargetValue,
                detailsJson: "{}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = $"Ban lifted for {request.TargetType}: {request.TargetValue}" });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpPost("revoke-user-sessions/{userId}")]
        public async Task<IActionResult> RevokeAllUserSessionsAsync(Guid userId)
        {
            var adminName = User.Identity?.Name ?? "ADMIN";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            await _authService.LogoutAllAsync(userId, $"REVOKED_BY_ADMIN_{adminName}");

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "ADMIN_REVOKE_USER_SESSIONS",
                targetEntity: "User",
                targetId: userId.ToString(),
                detailsJson: "{}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = $"All active sessions for user {userId} have been revoked." });
        }

        // ==========================================
        // 4. DEVICE & SESSION INSPECTION
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpGet("devices")]
        public async Task<IActionResult> GetDevicesAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.Devices.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(d => d.InstallationId.ToString().Contains(s) || d.OsVersion.ToLower().Contains(s) || d.AppVersion.ToLower().Contains(s));
            }

            var totalCount = await query.CountAsync();
            var devices = await query
                .OrderByDescending(d => d.LastSeenAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id,
                    d.InstallationId,
                    ClientType = d.ClientType.ToString(),
                    d.OsVersion,
                    d.AppVersion,
                    d.CoarseCountryCode,
                    d.IsBanned,
                    d.LastSeenAtUtc,
                    d.CreatedAtUtc,
                    sessionCount = d.Sessions.Count(s => !s.IsRevoked)
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, devices });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessionsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var now = DateTime.UtcNow;
            var query = _dbContext.Sessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .AsQueryable();

            var totalCount = await query.CountAsync();
            var sessions = await query
                .OrderByDescending(s => s.LastActivityAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    Username = s.User != null ? s.User.Username : "UNKNOWN",
                    InstallationId = s.Device != null ? s.Device.InstallationId : Guid.Empty,
                    s.UserAgent,
                    s.CoarseIpAddress,
                    s.IsRevoked,
                    s.RevocationReason,
                    s.CreatedAtUtc,
                    s.LastActivityAtUtc,
                    s.ExpiresAtUtc,
                    IsActive = !s.IsRevoked && s.ExpiresAtUtc > now
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, sessions });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SUPPORT")]
        [HttpPost("revoke-session/{sessionId}")]
        public async Task<IActionResult> RevokeSingleSessionAsync(Guid sessionId)
        {
            var adminName = User.Identity?.Name ?? "ADMIN";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            await _authService.LogoutAsync(sessionId, $"ADMIN_REVOKED_{adminName}");

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "ADMIN_REVOKE_SESSION",
                targetEntity: "Session",
                targetId: sessionId.ToString(),
                detailsJson: "{}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = $"Session {sessionId} has been revoked." });
        }

        // ==========================================
        // 5. RELEASE & ARTIFACT MANAGEMENT
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,RELEASE_MANAGER")]
        [HttpGet("releases")]
        public async Task<IActionResult> GetReleasesAsync([FromQuery] ClientType? platform = null)
        {
            var query = _dbContext.Releases.Include(r => r.Artifacts).AsQueryable();
            if (platform.HasValue)
            {
                query = query.Where(r => r.Platform == platform.Value);
            }

            var releases = await query
                .OrderByDescending(r => r.PublishedAtUtc)
                .Select(r => new
                {
                    r.Id,
                    Platform = r.Platform.ToString(),
                    r.Version,
                    r.MinimumSupportedVersion,
                    r.Title,
                    r.ReleaseNotes,
                    r.IsMandatory,
                    r.IsWithdrawn,
                    Severity = r.Severity.ToString(),
                    r.PublishedAtUtc,
                    r.CreatedAtUtc,
                    artifacts = r.Artifacts.Select(a => new
                    {
                        a.Id,
                        a.ArtifactName,
                        a.DownloadUrl,
                        a.Sha256Hash,
                        a.FileSizeBytes,
                        a.SignatureBase64
                    })
                })
                .ToListAsync();

            return Ok(releases);
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,RELEASE_MANAGER")]
        [HttpPost("releases")]
        public async Task<IActionResult> CreateReleaseAsync([FromBody] CreateReleaseDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Version))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Version and platform are required." });
            }

            bool exists = await _dbContext.Releases.AnyAsync(r => r.Platform == request.Platform && r.Version == request.Version);
            if (exists)
            {
                return Conflict(new { error = "VERSION_EXISTS", message = $"Release version {request.Version} already exists for {request.Platform}." });
            }

            var release = new Release
            {
                Id = Guid.NewGuid(),
                Platform = request.Platform,
                Version = request.Version.Trim(),
                MinimumSupportedVersion = request.MinimumSupportedVersion?.Trim() ?? "1.0.0",
                Title = request.Title?.Trim() ?? $"Release {request.Version}",
                ReleaseNotes = request.ReleaseNotes ?? string.Empty,
                IsMandatory = request.IsMandatory,
                IsWithdrawn = false,
                Severity = request.Severity,
                PublishedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (request.Artifacts != null)
            {
                foreach (var art in request.Artifacts)
                {
                    release.Artifacts.Add(new ReleaseArtifact
                    {
                        Id = Guid.NewGuid(),
                        ReleaseId = release.Id,
                        ArtifactName = art.ArtifactName,
                        DownloadUrl = art.DownloadUrl,
                        Sha256Hash = art.Sha256Hash,
                        FileSizeBytes = art.FileSizeBytes,
                        SignatureBase64 = art.SignatureBase64,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            _dbContext.Releases.Add(release);
            await _dbContext.SaveChangesAsync();

            var adminName = User.Identity?.Name ?? "RELEASE_MANAGER";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "RELEASE_CREATED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\",\"platform\":\"{release.Platform}\"}}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, releaseId = release.Id, version = release.Version });
        }

        [Authorize(Roles = "SUPER_ADMIN,ADMIN,RELEASE_MANAGER")]
        [HttpPut("releases/{id}/archive")]
        public async Task<IActionResult> ArchiveReleaseAsync(Guid id)
        {
            var release = await _dbContext.Releases.FindAsync(id);
            if (release == null) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            release.IsWithdrawn = true;
            await _dbContext.SaveChangesAsync();

            var adminName = User.Identity?.Name ?? "RELEASE_MANAGER";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            await _auditLogger.LogActionAsync(
                actorId: adminId,
                actorUsername: adminName,
                action: "RELEASE_ARCHIVED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: "{}",
                correlationId: HttpContext.TraceIdentifier,
                rawIpAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = $"Release {release.Version} has been archived/withdrawn." });
        }

        // ==========================================
        // 6. AUDIT LOGS
        // ==========================================
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,ANALYST")]
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? action = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.AuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(action))
            {
                var act = action.Trim();
                query = query.Where(a => a.Action.Contains(act));
            }

            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(l => l.TimestampUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, logs });
        }

        private static (DateTime startDate, int bucketDays) ParseRange(string range)
        {
            var now = DateTime.UtcNow;
            return range.ToLowerInvariant() switch
            {
                "today" => (new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc), 1),
                "7d" => (now.AddDays(-7), 1),
                "30d" => (now.AddDays(-30), 1),
                "90d" => (now.AddDays(-90), 3),
                "1y" => (now.AddYears(-1), 14),
                _ => (now.AddDays(-7), 1)
            };
        }
    }
}
