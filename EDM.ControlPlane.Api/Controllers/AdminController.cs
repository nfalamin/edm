using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record BanRequestDto(BanTargetType TargetType, string TargetValue, string Reason, int? DurationDays);
    public record UnbanRequestDto(BanTargetType TargetType, string TargetValue);
    
    public record CreateReleaseArtifactDto(
        string ArtifactName,
        string? Architecture = "x64",
        string DownloadUrl = "",
        string Sha256Hash = "",
        long FileSizeBytes = 0,
        string? SignatureBase64 = null);

    public record CreateReleaseDto(
        ClientType Platform,
        string Version,
        string? Channel = "stable",
        string? MinimumSupportedVersion = "1.0.0",
        string? Title = null,
        string? ReleaseNotes = null,
        bool IsMandatory = false,
        ReleaseSeverity Severity = ReleaseSeverity.Standard,
        List<CreateReleaseArtifactDto>? Artifacts = null);

    public record RollbackReleaseDto(string TargetVersion, string Reason);
    public record UpdateReleaseDto(
        string? Version = null,
        string? Channel = null,
        string? MinimumSupportedVersion = null,
        string? Title = null,
        string? ReleaseNotes = null,
        bool? IsMandatory = null,
        ReleaseSeverity? Severity = null);
    public record PermissionChangeDto(string PermissionCode);
    public record CreateAnnouncementDto(
        string Title,
        string Message,
        AnnouncementSeverity Severity = AnnouncementSeverity.Info,
        TargetAudience Audience = TargetAudience.All,
        DateTime? StartsAtUtc = null,
        DateTime? ExpiresAtUtc = null);

    [ApiController]
    [Authorize]
    [Route("api/v1/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IBanEnforcementService _banService;
        private readonly IAuthService _authService;
        private readonly IAuditLoggingService _auditLogger;
        private readonly IPermissionService _permissionService;
        private readonly IReleaseService _releaseService;

        public AdminController(
            ControlPlaneDbContext dbContext,
            IBanEnforcementService banService,
            IAuthService authService,
            IAuditLoggingService auditLogger,
            IPermissionService permissionService,
            IReleaseService releaseService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _banService = banService ?? throw new ArgumentNullException(nameof(banService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _releaseService = releaseService ?? throw new ArgumentNullException(nameof(releaseService));
        }

        // ==========================================
        // 1. DASHBOARD SUMMARY
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var totalUsers = await _dbContext.Users.CountAsync();
            var activeUsers = await _dbContext.Users.CountAsync(u => u.IsActive);
            var registeredDevices = await _dbContext.Devices.CountAsync();
            var activeSessions = await _dbContext.Sessions.CountAsync(s => !s.IsRevoked && s.ExpiresAtUtc > now);
            
            var totalDownloads = await _dbContext.DownloadRecords.CountAsync() + await _dbContext.TelemetryEvents.CountAsync(t => t.EventName == "download_completed");
            var downloadsToday = await _dbContext.DownloadRecords.CountAsync(d => d.DownloadedAtUtc >= todayStart) + await _dbContext.TelemetryEvents.CountAsync(t => t.EventName == "download_completed" && t.TimestampUtc >= todayStart);
            
            var latestRelease = await _dbContext.Releases
                .Where(r => !r.IsWithdrawn)
                .OrderByDescending(r => r.PublishedAtUtc)
                .Select(r => r.Version)
                .FirstOrDefaultAsync() ?? "None";

            var pendingUpdates = await _dbContext.Releases.CountAsync(r => r.IsWithdrawn);
            var securityEvents = await _dbContext.AuditLogs.CountAsync(a => a.ResultStatus == "DENIED" || a.Action.Contains("BAN") || a.Action.Contains("REUSE"));
            var bannedAccounts = await _dbContext.Bans.CountAsync(b => b.IsActive && (b.ExpiresAtUtc == null || b.ExpiresAtUtc > now));
            var activeLicenses = await _dbContext.Licenses.CountAsync(l => l.Status == LicenseStatus.Active);
            var openSupportTickets = await _dbContext.SupportTickets.CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

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
                activeLicenses,
                openSupportTickets,
                serverTimeUtc = DateTime.UtcNow
            });
        }

        // ==========================================
        // 2. ANALYTICS METRICS & RANGES
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
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
        // 3. USER MANAGEMENT & RBAC PERMISSIONS
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.UsersRead)]
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
                    sessionCount = _dbContext.Sessions.Count(s => s.UserId == u.Id && !s.IsRevoked),
                    licenseCount = _dbContext.Licenses.Count(l => l.UserId == u.Id)
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, users });
        }

        [Authorize]
        [RequirePermission(Permissions.UsersRead)]
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserByIdAsync(Guid id)
        {
            var user = await _dbContext.Users
                .Include(u => u.FeatureEntitlements)
                .Include(u => u.PermissionOverrides)
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

            var permissions = await _permissionService.GetEffectivePermissionsAsync(id);

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
                effectivePermissions = permissions,
                permissionOverrides = user.PermissionOverrides.Select(o => new { o.PermissionCode, o.IsGranted }),
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

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
        [HttpPost("users/{id}/permissions/grant")]
        public async Task<IActionResult> GrantPermissionAsync(Guid id, [FromBody] PermissionChangeDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PermissionCode))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Permission code is required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _permissionService.GrantUserPermissionAsync(id, request.PermissionCode.Trim(), adminId);
            if (!success) return NotFound(new { error = "USER_NOT_FOUND", message = "User not found." });

            return Ok(new { success = true, message = $"Permission '{request.PermissionCode}' granted to user {id}." });
        }

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
        [HttpPost("users/{id}/permissions/revoke")]
        public async Task<IActionResult> RevokePermissionAsync(Guid id, [FromBody] PermissionChangeDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PermissionCode))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Permission code is required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _permissionService.RevokeUserPermissionAsync(id, request.PermissionCode.Trim(), adminId);
            if (!success) return NotFound(new { error = "USER_NOT_FOUND", message = "User not found." });

            return Ok(new { success = true, message = $"Permission '{request.PermissionCode}' revoked from user {id}." });
        }

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
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

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
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

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
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
        [Authorize]
        [RequirePermission(Permissions.UsersRead)]
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

        [Authorize]
        [RequirePermission(Permissions.UsersRead)]
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

        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
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
        [Authorize]
        [RequirePermission(Permissions.ReleasesRead)]
        [HttpGet("releases")]
        public async Task<IActionResult> GetReleasesAsync([FromQuery] ClientType? platform = null, [FromQuery] string? channel = null, [FromQuery] bool includeWithdrawn = true)
        {
            var releases = await _releaseService.GetReleasesAsync(platform, channel, includeWithdrawn);
            return Ok(releases.Select(r => new
            {
                r.Id,
                Platform = r.Platform.ToString(),
                r.Version,
                r.Channel,
                r.MinimumSupportedVersion,
                r.Title,
                r.ReleaseNotes,
                r.IsMandatory,
                r.IsPublished,
                r.IsWithdrawn,
                r.RollbackTargetVersion,
                r.RollbackReason,
                Severity = r.Severity.ToString(),
                r.PublishedAtUtc,
                r.CreatedAtUtc,
                artifacts = r.Artifacts.Select(a => new
                {
                    a.Id,
                    a.ArtifactName,
                    a.Architecture,
                    a.DownloadUrl,
                    a.Sha256Hash,
                    a.FileSizeBytes,
                    a.SignatureBase64,
                    a.DownloadCount
                })
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesCreate)]
        [HttpPost("releases")]
        public async Task<IActionResult> CreateReleaseAsync([FromBody] CreateReleaseDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Version))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Version and platform are required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var model = new CreateReleaseModel(
                Platform: request.Platform,
                Version: request.Version,
                Channel: request.Channel ?? "stable",
                MinimumSupportedVersion: request.MinimumSupportedVersion ?? "1.0.0",
                Title: request.Title ?? string.Empty,
                ReleaseNotes: request.ReleaseNotes ?? string.Empty,
                IsMandatory: request.IsMandatory,
                Severity: request.Severity,
                Artifacts: request.Artifacts?.ConvertAll(a => new CreateArtifactModel(
                    ArtifactName: a.ArtifactName,
                    Architecture: a.Architecture ?? "x64",
                    DownloadUrl: a.DownloadUrl,
                    Sha256Hash: a.Sha256Hash,
                    FileSizeBytes: a.FileSizeBytes,
                    SignatureBase64: a.SignatureBase64)) ?? new List<CreateArtifactModel>());

            var release = await _releaseService.CreateReleaseAsync(model, adminId);
            return Ok(new { success = true, releaseId = release.Id, version = release.Version });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesPublish)]
        [HttpPut("releases/{id}")]
        public async Task<IActionResult> UpdateReleaseAsync(Guid id, [FromBody] UpdateReleaseDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_PAYLOAD", message = "Update model is required." });

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var model = new UpdateReleaseModel(
                Version: request.Version,
                Channel: request.Channel,
                MinimumSupportedVersion: request.MinimumSupportedVersion,
                Title: request.Title,
                ReleaseNotes: request.ReleaseNotes,
                IsMandatory: request.IsMandatory,
                Severity: request.Severity);

            var updated = await _releaseService.UpdateReleaseAsync(id, model, adminId);
            if (updated == null) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            return Ok(new { success = true, release = updated });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesCreate)]
        [HttpPost("releases/{id}/artifacts/upload")]
        [RequestSizeLimit(524_288_000)]
        public async Task<IActionResult> UploadArtifactAsync(
            Guid id,
            [FromForm] Microsoft.AspNetCore.Http.IFormFile file,
            [FromForm] string? architecture = "x64",
            [FromForm] string? expectedSha256 = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "EMPTY_FILE", message = "A valid installer binary file must be uploaded." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            try
            {
                using var stream = file.OpenReadStream();
                var artifact = await _releaseService.UploadArtifactAsync(
                    releaseId: id,
                    fileStream: stream,
                    originalFileName: file.FileName,
                    architecture: architecture ?? "x64",
                    expectedSha256: expectedSha256,
                    adminActorId: adminId);

                return Ok(new
                {
                    success = true,
                    artifactId = artifact.Id,
                    artifactName = artifact.ArtifactName,
                    architecture = artifact.Architecture,
                    sha256Hash = artifact.Sha256Hash,
                    fileSizeBytes = artifact.FileSizeBytes,
                    downloadUrl = artifact.DownloadUrl
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = "VALIDATION_FAILED", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "UPLOAD_FAILED", message = ex.Message });
            }
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesPublish)]
        [HttpPost("releases/{id}/publish")]
        public async Task<IActionResult> PublishReleaseAsync(Guid id)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _releaseService.PublishReleaseAsync(id, adminId);
            if (!success) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            return Ok(new { success = true, message = "Release published successfully to production." });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesPublish)]
        [HttpPost("releases/{id}/unpublish")]
        public async Task<IActionResult> UnpublishReleaseAsync(Guid id)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _releaseService.UnpublishReleaseAsync(id, adminId);
            if (!success) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            return Ok(new { success = true, message = "Release unpublished (moved to draft)." });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesCreate)]
        [HttpDelete("releases/{id}/artifacts/{artifactId}")]
        public async Task<IActionResult> DeleteArtifactAsync(Guid id, Guid artifactId)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _releaseService.DeleteArtifactAsync(id, artifactId, adminId);
            if (!success) return NotFound(new { error = "ARTIFACT_NOT_FOUND", message = "Artifact not found." });

            return Ok(new { success = true, message = "Artifact deleted successfully." });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesRollback)]
        [HttpPost("releases/{id}/rollback")]
        public async Task<IActionResult> RollbackReleaseAsync(Guid id, [FromBody] RollbackReleaseDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetVersion))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "TargetVersion is required for rollback." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _releaseService.RollbackReleaseAsync(id, request.TargetVersion.Trim(), request.Reason ?? "Administrative rollback", adminId);
            if (!success) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            return Ok(new { success = true, message = $"Release has been rolled back to version {request.TargetVersion}." });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesPublish)]
        [HttpPut("releases/{id}/archive")]
        public async Task<IActionResult> ArchiveReleaseAsync(Guid id)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _releaseService.WithdrawReleaseAsync(id, "Archived by administrator", adminId);
            if (!success) return NotFound(new { error = "RELEASE_NOT_FOUND", message = "Release not found." });

            return Ok(new { success = true, message = "Release has been archived/withdrawn." });
        }

        // ==========================================
        // 6. AUDIT LOGS
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.SecurityManage)]
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

        // ==========================================
        // 7. NOTIFICATIONS & ANNOUNCEMENTS
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.SettingsManage)]
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotificationsAsync([FromQuery] bool unreadOnly = false)
        {
            var query = _dbContext.AdminNotifications.AsQueryable();
            if (unreadOnly) query = query.Where(n => !n.IsRead);

            var list = await query
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(50)
                .ToListAsync();

            return Ok(list.ConvertAll(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                Type = n.Type.ToString(),
                n.IsRead,
                ActionUrl = n.LinkUrl,
                n.CreatedAtUtc
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.SettingsManage)]
        [HttpPost("notifications/mark-read")]
        public async Task<IActionResult> MarkAllNotificationsReadAsync()
        {
            var unread = await _dbContext.AdminNotifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true, markedCount = unread.Count });
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpGet("announcements")]
        public async Task<IActionResult> GetAnnouncementsAsync()
        {
            var list = await _dbContext.Announcements
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(50)
                .ToListAsync();

            return Ok(list.ConvertAll(a => new
            {
                a.Id,
                a.Title,
                a.Message,
                Severity = a.Severity.ToString(),
                Audience = a.Audience.ToString(),
                a.StartsAtUtc,
                ExpiresAtUtc = a.EndsAtUtc,
                a.IsActive,
                a.CreatedAtUtc
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("announcements")]
        public async Task<IActionResult> CreateAnnouncementAsync([FromBody] CreateAnnouncementDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Title and message are required." });
            }

            var announcement = new Announcement
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                Severity = request.Severity,
                Audience = request.Audience,
                StartsAtUtc = request.StartsAtUtc ?? DateTime.UtcNow,
                EndsAtUtc = request.ExpiresAtUtc,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Announcements.Add(announcement);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, announcementId = announcement.Id });
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
