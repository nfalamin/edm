using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }

        // ==========================================
        // 1. PUBLIC TELEMETRY BEACON
        // ==========================================
        [AllowAnonymous]
        [HttpPost("analytics/event")]
        public async Task<IActionResult> RecordEventAsync([FromBody] WebsiteEventDto dto)
        {
            if (dto == null) return BadRequest("Invalid event payload");

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            string? userAgent = Request.Headers.UserAgent.ToString();
            string? countryHeader = Request.Headers["CF-IPCountry"].ToString();
            if (string.IsNullOrWhiteSpace(countryHeader))
            {
                countryHeader = Request.Headers["X-Country-Code"].ToString();
            }

            var evt = await _analyticsService.RecordWebsiteEventAsync(dto, clientIp, userAgent, countryHeader);
            return Ok(new { success = true, eventId = evt.Id });
        }

        // ==========================================
        // 2. ADMIN ANALYTICS REPORTING
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
        [HttpGet("admin/analytics/website")]
        public async Task<IActionResult> GetWebsiteSummaryAsync([FromQuery] string range = "7d")
        {
            var summary = await _analyticsService.GetWebsiteAnalyticsSummaryAsync(range);
            return Ok(summary);
        }

        [Authorize]
        [RequirePermission(Permissions.AnalyticsRead)]
        [HttpGet("admin/analytics/downloads/overview")]
        public async Task<IActionResult> GetDownloadOverviewAsync([FromQuery] string range = "30d")
        {
            var overview = await _analyticsService.GetDownloadAnalyticsOverviewAsync(range);
            return Ok(overview);
        }
    }
}
