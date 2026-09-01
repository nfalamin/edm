using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record GenerateLicenseRequestDto(
        Guid PlanId,
        Guid? UserId,
        int MaxActivations = 3,
        int? DurationDays = null);

    public record RevokeLicenseRequestDto(string Reason);
    public record ActivateLicenseRequestDto(string LicenseKey, Guid InstallationId);

    [ApiController]
    [Route("api/v1/licenses")]
    public class LicenseController : ControllerBase
    {
        private readonly ILicenseService _licenseService;

        public LicenseController(ILicenseService licenseService)
        {
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpGet]
        public async Task<IActionResult> GetLicensesAsync(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] LicenseStatus? status = null)
        {
            var (totalCount, licenses) = await _licenseService.GetLicensesAsync(page, pageSize, search, status);
            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                licenses = licenses.ConvertAll(l => new
                {
                    l.Id,
                    l.KeyPrefix,
                    l.PlanId,
                    PlanName = l.Plan?.Name ?? "Unknown",
                    Status = l.Status.ToString(),
                    l.MaxActivations,
                    l.CurrentActivations,
                    l.ExpiresAtUtc,
                    l.CreatedAtUtc,
                    UserEmail = l.User?.Email
                })
            });
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlansAsync()
        {
            var plans = await _licenseService.GetPlansAsync();
            return Ok(plans.ConvertAll(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                Tier = p.Tier.ToString(),
                p.PriceMonthlyUsd,
                p.PriceYearlyUsd,
                p.MaxDevices,
                p.MaxConcurrentDownloads,
                p.FeaturesJson,
                p.IsActive
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateLicenseAsync([FromBody] GenerateLicenseRequestDto request)
        {
            if (request == null || request.PlanId == Guid.Empty)
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Plan ID is required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var result = await _licenseService.GenerateLicenseAsync(
                planId: request.PlanId,
                userId: request.UserId,
                maxActivations: request.MaxActivations,
                durationDays: request.DurationDays,
                adminActorId: adminId);

            return Ok(result);
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpPost("{id}/revoke")]
        public async Task<IActionResult> RevokeLicenseAsync(Guid id, [FromBody] RevokeLicenseRequestDto request)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _licenseService.RevokeLicenseAsync(id, request?.Reason ?? "Admin revocation", adminId);
            if (!success) return NotFound(new { error = "NOT_FOUND", message = "License not found." });

            return Ok(new { success = true, message = $"License {id} has been revoked." });
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpPost("{id}/suspend")]
        public async Task<IActionResult> SuspendLicenseAsync(Guid id, [FromBody] RevokeLicenseRequestDto request)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _licenseService.SuspendLicenseAsync(id, request?.Reason ?? "Admin suspension", adminId);
            if (!success) return NotFound(new { error = "NOT_FOUND", message = "License not found." });

            return Ok(new { success = true, message = $"License {id} has been suspended." });
        }

        [Authorize]
        [RequirePermission(Permissions.LicensesManage)]
        [HttpPost("{id}/reactivate")]
        public async Task<IActionResult> ReactivateLicenseAsync(Guid id)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _licenseService.ReactivateLicenseAsync(id, adminId);
            if (!success) return NotFound(new { error = "NOT_FOUND", message = "License not found." });

            return Ok(new { success = true, message = $"License {id} has been reactivated." });
        }

        [HttpPost("activate")]
        public async Task<IActionResult> ActivateLicenseAsync([FromBody] ActivateLicenseRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LicenseKey) || request.InstallationId == Guid.Empty)
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "LicenseKey and InstallationId are required." });
            }

            var result = await _licenseService.ValidateAndActivateLicenseAsync(
                rawLicenseKey: request.LicenseKey,
                installationId: request.InstallationId,
                clientIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers["User-Agent"].ToString());

            if (!result.IsValid)
            {
                return StatusCode(400, new { error = result.ErrorCode, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                planTier = result.Plan?.Tier.ToString(),
                planName = result.Plan?.Name,
                maxDevices = result.Plan?.MaxDevices,
                expiresAtUtc = result.License?.ExpiresAtUtc
            });
        }
    }
}
