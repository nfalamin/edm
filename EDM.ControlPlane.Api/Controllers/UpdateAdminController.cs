using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record ToggleFeatureFlagDto(bool Enabled);
    public record ArchiveRequestDto(string? Reason);

    [ApiController]
    [Route("api/v1")]
    public class UpdateAdminController : ControllerBase
    {
        private readonly IUpdateManagerService _updateService;

        public UpdateAdminController(IUpdateManagerService updateService)
        {
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        }

        private Guid? GetAdminUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }

        // ==========================================
        // 1. PUBLIC PUBLISHED ENDPOINTS (NO DRAFTS)
        // ==========================================
        [HttpGet("updates/published/latest")]
        public async Task<IActionResult> GetPublicLatestAsync([FromQuery] string component = "App")
        {
            var latest = await _updateService.GetPublishedLatestAsync(component);
            if (latest == null)
            {
                return NotFound(new { error = "NO_PUBLISHED_RELEASE", message = $"No active published release found for component '{component}'." });
            }
            return Ok(latest);
        }

        [HttpGet("updates/published")]
        public async Task<IActionResult> GetPublicPublishedListAsync([FromQuery] string? component = null)
        {
            var list = await _updateService.GetAllUpdatesAsync(component, includeDrafts: false);
            return Ok(list);
        }

        // ==========================================
        // 2. ADMIN SECURED ENDPOINTS
        // ==========================================
        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpGet("admin/updates")]
        public async Task<IActionResult> GetAllUpdatesAsync([FromQuery] string? component = null, [FromQuery] bool includeDrafts = true)
        {
            var updates = await _updateService.GetAllUpdatesAsync(component, includeDrafts);
            return Ok(updates);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpGet("admin/updates/{id}")]
        public async Task<IActionResult> GetUpdateByIdAsync(Guid id)
        {
            var update = await _updateService.GetUpdateByIdAsync(id);
            if (update == null) return NotFound(new { error = "NOT_FOUND", message = "Update release not found." });
            return Ok(update);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates")]
        public async Task<IActionResult> CreateUpdateDraftAsync([FromBody] CreateUpdateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Version))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Version is required." });
            }

            var created = await _updateService.CreateUpdateDraftAsync(dto, GetAdminUserId());
            return CreatedAtAction(nameof(GetUpdateByIdAsync), new { id = created.Id }, created);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPut("admin/updates/{id}/metadata")]
        public async Task<IActionResult> UpdateMetadataAsync(Guid id, [FromBody] UpdateMetadataDto dto)
        {
            if (dto == null) return BadRequest(new { error = "INVALID_PAYLOAD" });
            var updated = await _updateService.UpdateMetadataAsync(id, dto, GetAdminUserId());
            return Ok(updated);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/{id}/publish")]
        public async Task<IActionResult> PublishUpdateAsync(Guid id, [FromQuery] bool setAsLatest = true)
        {
            var published = await _updateService.PublishUpdateAsync(id, setAsLatest, GetAdminUserId());
            return Ok(published);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/{id}/unpublish")]
        public async Task<IActionResult> UnpublishUpdateAsync(Guid id)
        {
            var unpublished = await _updateService.UnpublishUpdateAsync(id, GetAdminUserId());
            return Ok(unpublished);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPut("admin/updates/{id}/toggle-download")]
        public async Task<IActionResult> ToggleDownloadAsync(Guid id, [FromBody] ToggleFeatureFlagDto flag)
        {
            var result = await _updateService.ToggleWebsiteDownloadAsync(id, flag.Enabled, GetAdminUserId());
            return Ok(result);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPut("admin/updates/{id}/toggle-auto-update")]
        public async Task<IActionResult> ToggleAutoUpdateAsync(Guid id, [FromBody] ToggleFeatureFlagDto flag)
        {
            var result = await _updateService.ToggleAutoUpdateAsync(id, flag.Enabled, GetAdminUserId());
            return Ok(result);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/{id}/set-latest")]
        public async Task<IActionResult> SetAsLatestAsync(Guid id)
        {
            var result = await _updateService.SetAsLatestAsync(id, GetAdminUserId());
            return Ok(result);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/{id}/archive")]
        public async Task<IActionResult> ArchiveUpdateAsync(Guid id, [FromBody] ArchiveRequestDto? dto)
        {
            var result = await _updateService.ArchiveUpdateAsync(id, dto?.Reason, GetAdminUserId());
            return Ok(result);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpDelete("admin/updates/{id}")]
        public async Task<IActionResult> DeleteUpdateAsync(Guid id)
        {
            bool deleted = await _updateService.DeleteUpdateAsync(id, GetAdminUserId());
            if (!deleted) return NotFound();
            return Ok(new { success = true, message = "Update deleted successfully." });
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/{id}/upload-artifact")]
        [RequestSizeLimit(150 * 1024 * 1024)] // 150 MB limit for desktop installer binaries
        public async Task<IActionResult> UploadArtifactAsync(Guid id, IFormFile file, [FromQuery] string architecture = "x64")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "NO_FILE_UPLOADED", message = "Please select a file to upload." });
            }

            using var stream = file.OpenReadStream();
            var artifact = await _updateService.UploadOrReplaceArtifactAsync(id, file.FileName, stream, architecture, GetAdminUserId());
            return Ok(artifact);
        }

        [Authorize]
        [RequirePermission(Permissions.ReleasesManage)]
        [HttpPost("admin/updates/scan-workspace")]
        public async Task<IActionResult> ScanWorkspaceAsync()
        {
            int discovered = await _updateService.ScanLocalUpdateWorkspaceAsync();
            return Ok(new { success = true, discoveredCount = discovered, message = $"Scanned local update workspace. Discovered {discovered} new draft releases." });
        }
    }
}
