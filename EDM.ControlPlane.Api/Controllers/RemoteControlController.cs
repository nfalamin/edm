using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Controllers
{
    public record DeviceHeartbeatRequest(
        Guid InstallationId,
        string? OsVersion,
        string? AppVersion,
        string? ClientType,
        List<LiveDownloadItemDto>? Downloads);

    public record LiveDownloadItemDto(
        string DownloadId,
        string FileName,
        string Url,
        string? Category,
        long TotalBytes,
        long DownloadedBytes,
        double ProgressPercentage,
        double SpeedBytesPerSecond,
        long? EtaSeconds,
        string Status,
        string? ErrorMessage);

    public record CreateRemoteCommandRequest(
        Guid DeviceId,
        string CommandType,
        string? TargetDownloadId,
        JsonElement? Payload);

    public record AcknowledgeCommandRequest(
        string Status,
        string? ErrorMessage);

    [ApiController]
    [Route("api/v1/remote")]
    [Authorize]
    public class RemoteControlController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;

        public RemoteControlController(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var uid) ? uid : null;
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetUserDevicesAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var user = await _dbContext.Users.FindAsync(userId.Value);

            // Find all devices associated with the user via Sessions, LiveDownloads, SyncedFiles, or RemoteCommands
            var sessionDeviceIds = await _dbContext.Sessions
                .Where(s => s.UserId == userId.Value)
                .Select(s => s.DeviceId)
                .Distinct()
                .ToListAsync();

            var liveDeviceIds = await _dbContext.LiveDownloads
                .Where(l => l.UserId == userId.Value)
                .Select(l => l.DeviceId)
                .Distinct()
                .ToListAsync();

            var syncedDeviceIds = await _dbContext.SyncedFiles
                .Where(f => f.OwnerId == userId.Value && f.DeviceId != null)
                .Select(f => f.DeviceId!.Value)
                .Distinct()
                .ToListAsync();

            var commandDeviceIds = await _dbContext.RemoteCommands
                .Where(c => c.UserId == userId.Value)
                .Select(c => c.DeviceId)
                .Distinct()
                .ToListAsync();

            var allDeviceIds = sessionDeviceIds
                .Union(liveDeviceIds)
                .Union(syncedDeviceIds)
                .Union(commandDeviceIds)
                .Distinct()
                .ToList();

            var deviceQuery = _dbContext.Devices.AsQueryable();
            if (user?.Role != UserRole.SUPER_ADMIN)
            {
                deviceQuery = deviceQuery.Where(d => allDeviceIds.Contains(d.Id));
            }
            else
            {
                deviceQuery = deviceQuery.Where(d => allDeviceIds.Contains(d.Id) || d.LastSeenAtUtc >= DateTime.UtcNow.AddDays(-7));
            }

            var devices = await deviceQuery.ToListAsync();

            var now = DateTime.UtcNow;
            var deviceDtos = new List<object>();

            foreach (var d in devices)
            {
                bool isOnline = (now - d.LastSeenAtUtc).TotalMinutes <= 2;
                int activeCount = await _dbContext.LiveDownloads
                    .CountAsync(l => l.DeviceId == d.Id && l.UserId == userId.Value && (l.Status == "Downloading" || l.Status == "Queued"));

                deviceDtos.Add(new
                {
                    id = d.Id,
                    installationId = d.InstallationId,
                    clientType = d.ClientType.ToString(),
                    osVersion = d.OsVersion,
                    appVersion = d.AppVersion,
                    coarseCountryCode = d.CoarseCountryCode ?? "Local",
                    isOnline,
                    status = isOnline ? "Online" : "Offline",
                    lastSeenAtUtc = d.LastSeenAtUtc,
                    activeDownloadCount = activeCount
                });
            }

            return Ok(new { devices = deviceDtos });
        }

        [HttpPost("devices/heartbeat")]
        public async Task<IActionResult> DeviceHeartbeatAsync([FromBody] DeviceHeartbeatRequest request)
        {
            if (request == null || request.InstallationId == Guid.Empty)
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "InstallationId is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            // Parse ClientType
            ClientType clientType = ClientType.DesktopWindows;
            if (!string.IsNullOrEmpty(request.ClientType) && Enum.TryParse<ClientType>(request.ClientType, true, out var parsedCt))
            {
                clientType = parsedCt;
            }

            // Find or create device
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.InstallationId == request.InstallationId);
            if (device == null)
            {
                device = new Device
                {
                    Id = Guid.NewGuid(),
                    InstallationId = request.InstallationId,
                    ClientType = clientType,
                    OsVersion = request.OsVersion ?? "Windows Desktop",
                    AppVersion = request.AppVersion ?? "2.0.0",
                    LastSeenAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Devices.Add(device);
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                device.LastSeenAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(request.OsVersion)) device.OsVersion = request.OsVersion;
                if (!string.IsNullOrEmpty(request.AppVersion)) device.AppVersion = request.AppVersion;
                if (!string.IsNullOrEmpty(request.ClientType)) device.ClientType = clientType;
                device.UpdatedAtUtc = DateTime.UtcNow;
            }

            // Link user's active session to this device if unassigned or updated
            var activeSession = await _dbContext.Sessions.FirstOrDefaultAsync(s => s.UserId == userId.Value && !s.IsRevoked);
            if (activeSession != null && activeSession.DeviceId != device.Id)
            {
                activeSession.DeviceId = device.Id;
            }

            // Sync live download telemetry
            if (request.Downloads != null)
            {
                var existingDownloads = await _dbContext.LiveDownloads
                    .Where(l => l.DeviceId == device.Id && l.UserId == userId.Value)
                    .ToListAsync();

                var incomingIds = new HashSet<string>(request.Downloads.Select(d => d.DownloadId), StringComparer.OrdinalIgnoreCase);

                // Remove downloads that are no longer reported
                var toRemove = existingDownloads.Where(e => !incomingIds.Contains(e.DownloadId)).ToList();
                if (toRemove.Count > 0)
                {
                    _dbContext.LiveDownloads.RemoveRange(toRemove);
                }

                // Upsert incoming downloads
                foreach (var incoming in request.Downloads)
                {
                    var existing = existingDownloads.FirstOrDefault(e => string.Equals(e.DownloadId, incoming.DownloadId, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.FileName = incoming.FileName;
                        existing.Url = incoming.Url;
                        existing.Category = incoming.Category ?? "General";
                        existing.TotalBytes = incoming.TotalBytes;
                        existing.DownloadedBytes = incoming.DownloadedBytes;
                        existing.ProgressPercentage = incoming.ProgressPercentage;
                        existing.SpeedBytesPerSecond = incoming.SpeedBytesPerSecond;
                        existing.EtaSeconds = incoming.EtaSeconds;
                        existing.Status = incoming.Status;
                        existing.ErrorMessage = incoming.ErrorMessage;
                        existing.LastUpdatedUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        _dbContext.LiveDownloads.Add(new LiveDownloadStatus
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId.Value,
                            DeviceId = device.Id,
                            DownloadId = incoming.DownloadId,
                            FileName = incoming.FileName,
                            Url = incoming.Url,
                            Category = incoming.Category ?? "General",
                            TotalBytes = incoming.TotalBytes,
                            DownloadedBytes = incoming.DownloadedBytes,
                            ProgressPercentage = incoming.ProgressPercentage,
                            SpeedBytesPerSecond = incoming.SpeedBytesPerSecond,
                            EtaSeconds = incoming.EtaSeconds,
                            Status = incoming.Status,
                            ErrorMessage = incoming.ErrorMessage,
                            LastUpdatedUtc = DateTime.UtcNow
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            // Count pending commands for this device
            int pendingCount = await _dbContext.RemoteCommands
                .CountAsync(c => c.DeviceId == device.Id && c.Status == RemoteCommandStatus.Pending && c.ExpiresAtUtc > DateTime.UtcNow);

            return Ok(new
            {
                success = true,
                deviceId = device.Id,
                serverTimeUtc = DateTime.UtcNow,
                pendingCommandCount = pendingCount
            });
        }

        [HttpGet("downloads")]
        public async Task<IActionResult> GetLiveDownloadsAsync([FromQuery] Guid? deviceId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var query = _dbContext.LiveDownloads
                .Include(l => l.Device)
                .Where(l => l.UserId == userId.Value);

            if (deviceId.HasValue)
            {
                query = query.Where(l => l.DeviceId == deviceId.Value);
            }

            var list = await query
                .OrderByDescending(l => l.LastUpdatedUtc)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var dtos = list.Select(l => new
            {
                id = l.Id,
                downloadId = l.DownloadId,
                deviceId = l.DeviceId,
                deviceName = l.Device != null ? $"{l.Device.ClientType} ({l.Device.OsVersion})" : "Desktop Client",
                isDeviceOnline = l.Device != null && (now - l.Device.LastSeenAtUtc).TotalMinutes <= 2,
                fileName = l.FileName,
                url = l.Url,
                category = l.Category,
                totalBytes = l.TotalBytes,
                downloadedBytes = l.DownloadedBytes,
                progressPercentage = l.ProgressPercentage,
                speedBytesPerSecond = l.SpeedBytesPerSecond,
                etaSeconds = l.EtaSeconds,
                status = l.Status,
                errorMessage = l.ErrorMessage,
                lastUpdatedUtc = l.LastUpdatedUtc
            }).ToList();

            return Ok(new { downloads = dtos });
        }

        [HttpPost("commands")]
        public async Task<IActionResult> SendRemoteCommandAsync([FromBody] CreateRemoteCommandRequest request)
        {
            if (request == null || request.DeviceId == Guid.Empty)
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "DeviceId is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            // Validate device existence
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId);
            if (device == null)
            {
                return NotFound(new { error = "DEVICE_NOT_FOUND", message = "Target device not found." });
            }

            // Verify device belongs to user (or user is SuperAdmin)
            bool isAuthorizedDevice = await _dbContext.Sessions.AnyAsync(s => s.UserId == userId.Value && s.DeviceId == device.Id)
                || await _dbContext.LiveDownloads.AnyAsync(l => l.UserId == userId.Value && l.DeviceId == device.Id)
                || await _dbContext.SyncedFiles.AnyAsync(f => f.OwnerId == userId.Value && f.DeviceId == device.Id);

            if (!isAuthorizedDevice)
            {
                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user?.Role != UserRole.SUPER_ADMIN)
                {
                    return Forbid();
                }
            }

            if (string.IsNullOrWhiteSpace(request.CommandType) || !Enum.TryParse<RemoteCommandType>(request.CommandType, true, out var commandType))
            {
                return BadRequest(new { error = "INVALID_COMMAND_TYPE", message = $"Unsupported command type '{request.CommandType}'." });
            }

            string? payloadStr = request.Payload.HasValue && request.Payload.Value.ValueKind != JsonValueKind.Undefined
                ? request.Payload.Value.GetRawText()
                : null;

            var cmd = new RemoteCommand
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                DeviceId = device.Id,
                CommandType = commandType,
                TargetDownloadId = request.TargetDownloadId,
                PayloadJson = payloadStr,
                Status = RemoteCommandStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
            };

            _dbContext.RemoteCommands.Add(cmd);

            // Audit log
            _dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorId = userId.Value,
                ActorUsername = User.Identity?.Name ?? "User",
                Action = $"REMOTE_COMMAND_{commandType}",
                TargetEntity = "Device",
                TargetId = device.Id.ToString(),
                ResultStatus = "PENDING",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    commandId = cmd.Id,
                    commandType = commandType.ToString(),
                    targetDownloadId = request.TargetDownloadId
                }),
                TimestampUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Remote command queued.",
                command = new
                {
                    id = cmd.Id,
                    deviceId = cmd.DeviceId,
                    commandType = cmd.CommandType.ToString(),
                    targetDownloadId = cmd.TargetDownloadId,
                    status = cmd.Status.ToString(),
                    createdAtUtc = cmd.CreatedAtUtc
                }
            });
        }

        [HttpGet("commands/pending")]
        public async Task<IActionResult> GetPendingCommandsAsync([FromQuery] Guid? installationId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var query = _dbContext.RemoteCommands
                .Include(c => c.Device)
                .Where(c => c.UserId == userId.Value && c.Status == RemoteCommandStatus.Pending && c.ExpiresAtUtc > DateTime.UtcNow);

            if (installationId.HasValue)
            {
                query = query.Where(c => c.Device != null && c.Device.InstallationId == installationId.Value);
            }

            var pending = await query
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync();

            var dtos = pending.Select(c => new
            {
                id = c.Id,
                deviceId = c.DeviceId,
                commandType = c.CommandType.ToString(),
                targetDownloadId = c.TargetDownloadId,
                payloadJson = c.PayloadJson,
                status = c.Status.ToString(),
                createdAtUtc = c.CreatedAtUtc
            }).ToList();

            return Ok(new { commands = dtos });
        }

        [HttpPost("commands/{id}/ack")]
        public async Task<IActionResult> AcknowledgeCommandAsync([FromRoute] Guid id, [FromBody] AcknowledgeCommandRequest request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_PAYLOAD" });

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var cmd = await _dbContext.RemoteCommands.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);
            if (cmd == null) return NotFound(new { error = "COMMAND_NOT_FOUND" });

            if (string.IsNullOrWhiteSpace(request.Status) || !Enum.TryParse<RemoteCommandStatus>(request.Status, true, out var status))
            {
                return BadRequest(new { error = "INVALID_STATUS", message = $"Unsupported status '{request.Status}'." });
            }

            cmd.Status = status;
            cmd.ErrorMessage = request.ErrorMessage;

            if (status == RemoteCommandStatus.Received)
            {
                cmd.AcknowledgedAtUtc = DateTime.UtcNow;
            }
            else if (status == RemoteCommandStatus.Completed || status == RemoteCommandStatus.Failed)
            {
                cmd.CompletedAtUtc = DateTime.UtcNow;

                _dbContext.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActorId = userId.Value,
                    ActorUsername = User.Identity?.Name ?? "User",
                    Action = $"REMOTE_COMMAND_{cmd.CommandType}_EXECUTED",
                    TargetEntity = "RemoteCommand",
                    TargetId = cmd.Id.ToString(),
                    ResultStatus = status == RemoteCommandStatus.Completed ? "SUCCESS" : "FAILURE",
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        commandId = cmd.Id,
                        status = status.ToString(),
                        errorMessage = request.ErrorMessage
                    }),
                    TimestampUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, status = cmd.Status.ToString(), completedAtUtc = cmd.CompletedAtUtc });
        }

        [HttpGet("commands/{id}")]
        public async Task<IActionResult> GetCommandStatusAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var cmd = await _dbContext.RemoteCommands.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);
            if (cmd == null) return NotFound(new { error = "COMMAND_NOT_FOUND" });

            return Ok(new
            {
                id = cmd.Id,
                deviceId = cmd.DeviceId,
                commandType = cmd.CommandType.ToString(),
                targetDownloadId = cmd.TargetDownloadId,
                status = cmd.Status.ToString(),
                errorMessage = cmd.ErrorMessage,
                createdAtUtc = cmd.CreatedAtUtc,
                acknowledgedAtUtc = cmd.AcknowledgedAtUtc,
                completedAtUtc = cmd.CompletedAtUtc
            });
        }
    }
}
