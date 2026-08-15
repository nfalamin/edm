using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record LoginDto(string UsernameOrEmail, string Password, Guid? InstallationId);
    public record RegisterDto(string Username, string Email, string Password);
    public record RefreshDto(string RefreshToken, Guid? InstallationId);
    public record ChangePasswordDto(string OldPassword, string NewPassword);
    public record SessionDto(Guid Id, Guid DeviceId, string UserAgent, string? CoarseIpAddress, DateTime CreatedAtUtc, DateTime LastActivityAtUtc, bool IsCurrent);

    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ControlPlaneDbContext _dbContext;

        public AuthController(IAuthService authService, ControlPlaneDbContext dbContext)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Invalid registration payload." });

            var result = await _authService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "CONFLICT") return Conflict(new { error = result.ErrorCode, message = result.Message });
                return BadRequest(new { error = result.ErrorCode, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                user = result.User
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Invalid login payload." });

            var result = await _authService.LoginAsync(
                request.UsernameOrEmail,
                request.Password,
                request.InstallationId,
                Request.Headers["User-Agent"].ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCESS_DENIED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "ACCOUNT_SUSPENDED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshAsync([FromBody] RefreshDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { error = "INVALID_TOKEN", message = "Refresh token is required." });
            }

            var result = await _authService.RefreshTokenAsync(
                request.RefreshToken,
                request.InstallationId,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCESS_DENIED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "TOKEN_REUSE") return StatusCode(401, new { error = result.ErrorCode, message = result.Message });
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync()
        {
            var sessionClaim = User.FindFirst("session_id");
            if (sessionClaim != null && Guid.TryParse(sessionClaim.Value, out var sessionId))
            {
                await _authService.LogoutAsync(sessionId, "USER_LOGOUT");
            }
            return Ok(new { success = true, message = "Logged out successfully." });
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAllAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                await _authService.LogoutAllAsync(userId, "LOGOUT_ALL_REQUESTED");
            }
            return Ok(new { success = true, message = "All active sessions revoked." });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Invalid password change payload." });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            var sessionClaim = User.FindFirst("session_id");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId) ||
                sessionClaim == null || !Guid.TryParse(sessionClaim.Value, out var sessionId))
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "Invalid token context." });
            }

            var result = await _authService.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword, sessionId);
            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorCode, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                return NotFound(new { error = "USER_NOT_FOUND", message = "User not found or inactive." });
            }

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                role = user.Role.ToString(),
                createdAtUtc = user.CreatedAtUtc
            });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetMySessionsAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            var sessionClaim = User.FindFirst("session_id");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            Guid currentSessionId = Guid.Empty;
            if (sessionClaim != null) Guid.TryParse(sessionClaim.Value, out currentSessionId);

            // ANTI-IDOR: Filter strictly by logged-in userId
            var sessions = await _dbContext.Sessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .OrderByDescending(s => s.LastActivityAtUtc)
                .Select(s => new SessionDto(
                    s.Id,
                    s.DeviceId,
                    s.UserAgent,
                    s.CoarseIpAddress,
                    s.CreatedAtUtc,
                    s.LastActivityAtUtc,
                    s.Id == currentSessionId))
                .ToListAsync();

            return Ok(sessions);
        }

        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSessionAsync(Guid sessionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            // ANTI-IDOR: Verify session belongs to the requesting user
            var session = await _dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
            if (session == null)
            {
                return NotFound(new { error = "SESSION_NOT_FOUND", message = "Session not found or does not belong to you." });
            }

            await _authService.LogoutAsync(sessionId, "USER_REVOKED_REMOTE_SESSION");
            return Ok(new { success = true, message = "Session revoked successfully." });
        }
    }
}
