using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record UpdateCheckRequest(
        ClientType Platform,
        string CurrentVersion,
        Guid InstallationId,
        string? Channel = "stable");

    public record UpdateCheckResponse(
        bool UpdateAvailable,
        string CurrentVersion,
        string LatestVersion,
        string MinimumSupportedVersion,
        bool IsMandatory,
        string Severity,
        string Title,
        string ReleaseNotes,
        DateTime PublishedAtUtc,
        string? DownloadUrl,
        string? Sha256Hash,
        long FileSizeBytes,
        string? SignatureBase64);

    [ApiController]
    [Route("api/v1/updates")]
    public class UpdateController : ControllerBase
    {
        private readonly IReleaseService _releaseService;
        private readonly ControlPlaneDbContext _dbContext;

        public UpdateController(IReleaseService releaseService, ControlPlaneDbContext dbContext)
        {
            _releaseService = releaseService ?? throw new ArgumentNullException(nameof(releaseService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpPost("check")]
        public async Task<ActionResult<UpdateCheckResponse>> CheckForUpdateAsync([FromBody] UpdateCheckRequest request)
        {
            if (request == null) return BadRequest("Invalid update request");

            // Query single source of truth for active published release
            var latestRelease = await _releaseService.GetLatestActiveReleaseAsync(request.Platform, request.Channel ?? "stable");

            if (latestRelease == null)
            {
                return Ok(new UpdateCheckResponse(
                    UpdateAvailable: false,
                    CurrentVersion: request.CurrentVersion,
                    LatestVersion: request.CurrentVersion,
                    MinimumSupportedVersion: "1.0.0",
                    IsMandatory: false,
                    Severity: "OPTIONAL",
                    Title: "Up to date",
                    ReleaseNotes: string.Empty,
                    PublishedAtUtc: DateTime.UtcNow,
                    DownloadUrl: null,
                    Sha256Hash: null,
                    FileSizeBytes: 0,
                    SignatureBase64: null));
            }

            bool isNewer = IsVersionNewer(latestRelease.Version, request.CurrentVersion);
            bool isBelowMin = IsVersionNewer(latestRelease.MinimumSupportedVersion, request.CurrentVersion);
            bool isRequired = latestRelease.IsMandatory || isBelowMin || latestRelease.Severity == ReleaseSeverity.Critical;

            string severityStr = isRequired ? "REQUIRED" : (latestRelease.Severity == ReleaseSeverity.Recommended ? "RECOMMENDED" : "OPTIONAL");

            var primaryArtifact = latestRelease.Artifacts.FirstOrDefault();
            string? downloadUrl = primaryArtifact != null
                ? (primaryArtifact.DownloadUrl ?? $"/api/v1/releases/artifacts/{primaryArtifact.Id}/download")
                : null;

            return Ok(new UpdateCheckResponse(
                UpdateAvailable: isNewer,
                CurrentVersion: request.CurrentVersion,
                LatestVersion: latestRelease.Version,
                MinimumSupportedVersion: latestRelease.MinimumSupportedVersion,
                IsMandatory: isRequired,
                Severity: severityStr,
                Title: latestRelease.Title,
                ReleaseNotes: latestRelease.ReleaseNotes,
                PublishedAtUtc: latestRelease.PublishedAtUtc,
                DownloadUrl: downloadUrl,
                Sha256Hash: primaryArtifact?.Sha256Hash,
                FileSizeBytes: primaryArtifact?.FileSizeBytes ?? 0,
                SignatureBase64: primaryArtifact?.SignatureBase64));
        }

        [HttpGet("releases/{platform}")]
        public async Task<IActionResult> GetReleasesByPlatformAsync(ClientType platform)
        {
            var releases = await _dbContext.Releases
                .Include(r => r.Artifacts)
                .Where(r => r.Platform == platform && !r.IsWithdrawn)
                .OrderByDescending(r => r.PublishedAtUtc)
                .Take(20)
                .ToListAsync();

            return Ok(releases);
        }

        private static bool IsVersionNewer(string targetVersion, string currentVersion)
        {
            if (Version.TryParse(targetVersion, out var tVer) && Version.TryParse(currentVersion, out var cVer))
            {
                return tVer > cVer;
            }
            return string.Compare(targetVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
