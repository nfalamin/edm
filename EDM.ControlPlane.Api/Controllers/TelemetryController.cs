using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Controllers
{
    public record TelemetryEventDto(
        Guid? InstallationId,
        string EventName,
        JsonElement Payload);

    [ApiController]
    [Route("api/v1/telemetry")]
    public class TelemetryController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;

        // Privacy-safe allowlist of accepted event types
        private static readonly HashSet<string> AllowedEventNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "download_started",
            "download_completed",
            "download_failed",
            "video_detected",
            "app_started",
            "update_checked",
            "update_applied",
            "segment_error_recovered"
        };

        private const int MaxPayloadLength = 8192; // 8 KB payload ceiling

        public TelemetryController(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpPost("event")]
        public async Task<IActionResult> RecordEventAsync([FromBody] TelemetryEventDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EventName))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "EventName is required." });
            }

            if (!AllowedEventNames.Contains(request.EventName))
            {
                return BadRequest(new { error = "EVENT_TYPE_REJECTED", message = $"EventName '{request.EventName}' is not in the telemetry allowlist." });
            }

            string rawPayload = request.Payload.ValueKind != JsonValueKind.Undefined ? request.Payload.GetRawText() : "{}";
            if (rawPayload.Length > MaxPayloadLength)
            {
                return BadRequest(new { error = "PAYLOAD_TOO_LARGE", message = $"Payload size exceeds maximum allowed limit of {MaxPayloadLength} bytes." });
            }

            Guid installId = request.InstallationId ?? Guid.NewGuid();

            // Find or create device record
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.InstallationId == installId);
            if (device == null)
            {
                device = new Device
                {
                    Id = Guid.NewGuid(),
                    InstallationId = installId,
                    ClientType = ClientType.DesktopWindows,
                    OsVersion = "Windows Desktop",
                    AppVersion = "2.0.0",
                    LastSeenAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Devices.Add(device);
            }
            else
            {
                device.LastSeenAtUtc = DateTime.UtcNow;
            }

            var telemetryEvent = new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                EventName = request.EventName.ToLowerInvariant(),
                EventPayloadJson = rawPayload,
                TimestampUtc = DateTime.UtcNow
            };

            _dbContext.TelemetryEvents.Add(telemetryEvent);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, eventId = telemetryEvent.Id, timestampUtc = telemetryEvent.TimestampUtc });
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEventsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? eventName = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.TelemetryEvents.Include(t => t.Device).AsQueryable();

            if (!string.IsNullOrWhiteSpace(eventName))
            {
                var ev = eventName.Trim().ToLowerInvariant();
                query = query.Where(t => t.EventName == ev);
            }

            var totalCount = await query.CountAsync();
            var events = await query
                .OrderByDescending(t => t.TimestampUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.DeviceId,
                    InstallationId = t.Device != null ? t.Device.InstallationId : Guid.Empty,
                    ClientType = t.Device != null ? t.Device.ClientType.ToString() : "Unknown",
                    t.EventName,
                    t.EventPayloadJson,
                    t.TimestampUtc
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, events });
        }
    }
}
