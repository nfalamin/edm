using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    [Route("api/v1/releases")]
    public class ReleaseDownloadController : ControllerBase
    {
        private readonly IReleaseService _releaseService;

        public ReleaseDownloadController(IReleaseService releaseService)
        {
            _releaseService = releaseService ?? throw new ArgumentNullException(nameof(releaseService));
        }

        [HttpGet("latest")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetLatestReleaseAsync(
            [FromQuery] ClientType platform = ClientType.DesktopWindows,
            [FromQuery] string channel = "stable")
        {
            var release = await _releaseService.GetLatestActiveReleaseAsync(platform, channel);
            if (release == null)
            {
                return NotFound(new { error = "NO_ACTIVE_RELEASE", message = $"No active {channel} release found for platform {platform}." });
            }

            var primaryArtifact = release.Artifacts.FirstOrDefault();

            return Ok(new
            {
                release.Id,
                Platform = release.Platform.ToString(),
                release.Version,
                release.Channel,
                release.MinimumSupportedVersion,
                release.Title,
                release.ReleaseNotes,
                release.IsMandatory,
                Severity = release.Severity.ToString(),
                release.PublishedAtUtc,
                downloadUrl = primaryArtifact?.DownloadUrl ?? $"/api/v1/releases/artifacts/{primaryArtifact?.Id}/download",
                sha256Hash = primaryArtifact?.Sha256Hash,
                fileSizeBytes = primaryArtifact?.FileSizeBytes ?? 0,
                artifacts = release.Artifacts.Select(a => new
                {
                    a.Id,
                    a.ArtifactName,
                    a.Architecture,
                    DownloadUrl = $"/api/v1/releases/artifacts/{a.Id}/download",
                    a.Sha256Hash,
                    a.FileSizeBytes,
                    a.DownloadCount
                })
            });
        }

        [HttpGet("artifacts/{artifactId}/download")]
        public async Task<IActionResult> DownloadArtifactAsync(Guid artifactId)
        {
            try
            {
                var (stream, contentType, fileName, fileLength) = await _releaseService.GetArtifactFileStreamAsync(artifactId);

                string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                string? userAgent = Request.Headers.UserAgent.ToString();
                string? countryHeader = Request.Headers["CF-IPCountry"].ToString();
                if (string.IsNullOrWhiteSpace(countryHeader))
                {
                    countryHeader = Request.Headers["X-Country-Code"].ToString();
                }

                // Fire & forget or background telemetry log
                _ = _releaseService.RecordArtifactDownloadAsync(artifactId, clientIp, userAgent, countryHeader);

                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

                return File(stream, contentType, fileName, enableRangeProcessing: true);
            }
            catch (FileNotFoundException fnf)
            {
                return NotFound(new { error = "FILE_NOT_FOUND", message = fnf.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "DOWNLOAD_FAILED", message = ex.Message });
            }
        }

        [HttpGet("latest/download")]
        public async Task<IActionResult> DownloadLatestReleaseAsync(
            [FromQuery] ClientType platform = ClientType.DesktopWindows,
            [FromQuery] string channel = "stable")
        {
            var release = await _releaseService.GetLatestActiveReleaseAsync(platform, channel);
            if (release == null)
            {
                return NotFound(new { error = "NO_ACTIVE_RELEASE", message = "No active release available for download." });
            }

            var primaryArtifact = release.Artifacts.FirstOrDefault();
            if (primaryArtifact == null)
            {
                return NotFound(new { error = "NO_ARTIFACTS", message = "No installer binary attached to latest release." });
            }

            return await DownloadArtifactAsync(primaryArtifact.Id);
        }
    }
}
