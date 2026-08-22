using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly ISystemHealthService _healthService;

        public HealthController(ControlPlaneDbContext dbContext, ISystemHealthService healthService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "Healthy",
                service = "EDM.ControlPlane.Api",
                version = "2.1.0",
                timestampUtc = DateTime.UtcNow
            });
        }

        [HttpGet("health/ready")]
        public async Task<IActionResult> GetReadiness()
        {
            bool dbConnected = await _dbContext.Database.CanConnectAsync();
            if (dbConnected)
            {
                return Ok(new { status = "Ready", database = "Connected", timestampUtc = DateTime.UtcNow });
            }
            return StatusCode(503, new { status = "Unhealthy", database = "Disconnected", timestampUtc = DateTime.UtcNow });
        }

        [HttpGet("health/live")]
        public IActionResult GetLiveness()
        {
            return Ok(new { status = "Alive", timestampUtc = DateTime.UtcNow });
        }

        [Authorize]
        [RequirePermission(Permissions.SystemHealthRead)]
        [HttpGet("api/v1/health/diagnostics")]
        [HttpGet("health/diagnostics")]
        public async Task<IActionResult> GetDiagnosticsAsync()
        {
            var report = await _healthService.CheckSystemHealthAsync();
            var snapshots = await _healthService.GetRecentSnapshotsAsync(10);

            return Ok(new
            {
                report.OverallStatus,
                report.LatencyMs,
                report.Components,
                report.CheckedAtUtc,
                recentSnapshots = snapshots.ConvertAll(s => new
                {
                    s.ComponentName,
                    Status = s.Status.ToString(),
                    s.LatencyMs,
                    s.DetailsJson,
                    s.CheckedAtUtc
                })
            });
        }
    }
}
