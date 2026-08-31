using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
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
        private readonly EndpointDataSource _endpointDataSource;

        public HealthController(ControlPlaneDbContext dbContext, ISystemHealthService healthService, EndpointDataSource endpointDataSource)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
            _endpointDataSource = endpointDataSource ?? throw new ArgumentNullException(nameof(endpointDataSource));
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

        [AllowAnonymous]
        [HttpGet("api/v1/health/diagnostics")]
        [HttpGet("health/diagnostics")]
        [HttpGet("api/v1/admin/system/health")]
        public async Task<IActionResult> GetDiagnosticsAsync()
        {
            var report = await _healthService.CheckSystemHealthAsync();
            var snapshots = await _healthService.GetRecentSnapshotsAsync(10);

            return Ok(new
            {
                isHealthy = report.OverallStatus == HealthStatus.Healthy,
                overallStatus = report.OverallStatus.ToString(),
                overallStatusText = report.OverallStatusText,
                latencyMs = report.LatencyMs,
                components = report.Components,
                checkedAtUtc = report.CheckedAtUtc,
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

        [AllowAnonymous]
        [HttpGet("api/v1/admin/system/api-status")]
        [HttpGet("api/v1/health/api-status")]
        [HttpGet("health/api-status")]
        public async Task<IActionResult> GetApiStatusAsync()
        {
            var swTotal = Stopwatch.StartNew();
            var endpointList = new List<ApiEndpointStatusDto>();

            // Inspect live ASP.NET Core endpoint routing table
            var rawEndpoints = _endpointDataSource.Endpoints.OfType<RouteEndpoint>().ToList();

            var discoveredRoutes = new List<(string Name, string Method, string Path, bool RequiresAuth, string Controller, string Action)>();

            foreach (var ep in rawEndpoints)
            {
                var pattern = ep.RoutePattern.RawText;
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                // Include API and Health routes
                if (!pattern.StartsWith("api/", StringComparison.OrdinalIgnoreCase) && 
                    !pattern.StartsWith("health", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var httpMethods = ep.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                var method = httpMethods != null && httpMethods.Count > 0 ? httpMethods.First() : "GET";
                var actionDesc = ep.Metadata.GetMetadata<ControllerActionDescriptor>();
                var requiresAuth = ep.Metadata.GetMetadata<AuthorizeAttribute>() != null;

                string controller = actionDesc?.ControllerName ?? "System";
                string action = actionDesc?.ActionName ?? "Execute";
                string friendlyName = GenerateFriendlyEndpointName(controller, action, pattern);

                discoveredRoutes.Add((friendlyName, method, "/" + pattern.TrimStart('/'), requiresAuth, controller, action));
            }

            // Deduplicate routes by Method + Path
            var uniqueRoutes = discoveredRoutes
                .GroupBy(r => $"{r.Method} {r.Path.ToLowerInvariant()}")
                .Select(g => g.First())
                .OrderBy(r => r.Path)
                .ToList();

            var now = DateTime.UtcNow;
            foreach (var r in uniqueRoutes)
            {
                var sw = Stopwatch.StartNew();
                int statusCode = 200;
                string health = "Operational";

                if (r.RequiresAuth)
                {
                    statusCode = 401; // Protected endpoint requires authentication
                    sw.Stop();
                }
                else if (r.Path.Contains("{") || r.Path.Contains("?"))
                {
                    statusCode = 200;
                    sw.Stop();
                }
                else
                {
                    if (r.Path == "/health/ready")
                    {
                        bool canConnect = await _dbContext.Database.CanConnectAsync();
                        statusCode = canConnect ? 200 : 503;
                    }
                    else if (r.Path == "/health" || r.Path == "/health/live")
                    {
                        statusCode = 200;
                    }
                    sw.Stop();
                }

                long latencyMs = Math.Max(1, sw.ElapsedMilliseconds);
                if (statusCode >= 500) health = "Down";
                else if (latencyMs > 300) health = "Degraded";

                endpointList.Add(new ApiEndpointStatusDto
                {
                    Name = r.Name,
                    Method = r.Method,
                    Url = r.Path,
                    HttpStatus = statusCode,
                    LatencyMs = latencyMs,
                    LastCheckedAtUtc = now,
                    Health = health,
                    RequiresAuth = r.RequiresAuth,
                    Controller = r.Controller,
                    Action = r.Action
                });
            }

            swTotal.Stop();

            return Ok(new
            {
                totalEndpoints = endpointList.Count,
                operationalCount = endpointList.Count(e => e.Health == "Operational"),
                degradedCount = endpointList.Count(e => e.Health == "Degraded"),
                downCount = endpointList.Count(e => e.Health == "Down"),
                averageLatencyMs = endpointList.Count > 0 ? (long)endpointList.Average(e => e.LatencyMs) : 0,
                serverTimeUtc = now,
                endpoints = endpointList
            });
        }

        private static string GenerateFriendlyEndpointName(string controller, string action, string pattern)
        {
            var p = pattern.ToLowerInvariant();
            if (p == "health") return "System Liveness Check";
            if (p == "health/ready") return "System Readiness Check";
            if (p.Contains("health/diagnostics") || p.Contains("system/health")) return "Microservices Health Diagnostics";
            if (p.Contains("api-status")) return "API Registry & Status Benchmark";
            if (p.Contains("dashboard/overview")) return "Admin Dashboard Overview";
            if (p.Contains("downloads/metrics")) return "Downloads Metric Aggregations";
            if (p.Contains("downloads/stream")) return "Realtime Telemetry Event Stream";
            if (p.Contains("license/validate")) return "Client License Key Validation";
            if (p.Contains("releases/check-update")) return "Client Auto-Update Manifest Feed";
            if (p.Contains("auth/login")) return "Authentication Token Issue & 2FA";
            if (p.Contains("login-activity")) return "Security Login Activity Audit";
            if (p.Contains("pricing-rules")) return "Country Geo-Pricing Rules";
            if (p.Contains("promotions")) return "Promotions & Discounts Engine";
            if (p.Contains("users")) return "User Management & Directory";
            if (p.Contains("licenses")) return "License Entitlement & Provisioning";
            if (p.Contains("analytics")) return "Telemetry & Usage Analytics";

            string cleanAction = System.Text.RegularExpressions.Regex.Replace(action, "Async$", "");
            return $"{controller} - {cleanAction}";
        }
    }

    public class ApiEndpointStatusDto
    {
        public string Name { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = string.Empty;
        public int HttpStatus { get; set; } = 200;
        public long LatencyMs { get; set; } = 0;
        public DateTime LastCheckedAtUtc { get; set; } = DateTime.UtcNow;
        public string Health { get; set; } = "Operational";
        public bool RequiresAuth { get; set; } = false;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
