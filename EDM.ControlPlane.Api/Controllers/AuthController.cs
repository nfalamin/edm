using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record LoginDto(string UsernameOrEmail, string Password, Guid? InstallationId, bool RememberDevice = false);
    public record RegisterDto(string Username, string Email, string Password);
    public record SetupInitialAdminDto(string Username, string Email, string Password);
    public record RefreshDto(string? RefreshToken, Guid? InstallationId);
    public record ChangePasswordDto(string OldPassword, string NewPassword);
    public record Verify2FaDto(string TwoFactorTicket, string Code, bool IsRecoveryCode, Guid? InstallationId);
    public record Confirm2FaDto(string Code);
    public record Disable2FaDto(string Password);
    public record RegenerateRecoveryCodesDto(string Password);
    public record RequestRecoveryEmailDto(string Password, string NewRecoveryEmail);
    public record ConfirmRecoveryEmailDto(string Token);
    public record GoogleLoginDto(string IdToken, Guid? InstallationId);
    public record FirebaseLoginDto(string IdToken, Guid? InstallationId, string? ClientType = null);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordDto(string Token, string NewPassword, string? TwoFactorCode = null, bool IsRecoveryCode = false);
    public record PasskeyRegisterDto(string ClientDataJson, string AttestationObject, string DeviceName);
    public record PasskeyLoginDto(string CredentialId, string ClientDataJson, string AuthenticatorData, string Signature, Guid? InstallationId);
    public record RenamePasskeyDto(string NewName);
    public record SessionDto(Guid Id, Guid DeviceId, string UserAgent, string? CoarseIpAddress, DateTime CreatedAtUtc, DateTime LastActivityAtUtc, bool IsCurrent);

    [ApiController]
    [EnableRateLimiting("AuthRateLimit")]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ControlPlaneDbContext _dbContext;
        private readonly ICsrfProtectionService _csrfService;
        private readonly IPasskeyService _passkeyService;

        public AuthController(
            IAuthService authService,
            ControlPlaneDbContext dbContext,
            ICsrfProtectionService csrfService,
            IPasskeyService passkeyService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _csrfService = csrfService ?? throw new ArgumentNullException(nameof(csrfService));
            _passkeyService = passkeyService ?? throw new ArgumentNullException(nameof(passkeyService));
        }

        [HttpGet("csrf-token")]
        public IActionResult GetCsrfToken()
        {
            string token = _csrfService.GenerateCsrfToken(HttpContext);
            Response.Headers["X-CSRF-Token"] = token;
            return Ok(new { csrfToken = token });
        }

        [HttpPost("setup-initial-admin")]
        public async Task<IActionResult> SetupInitialAdminAsync([FromBody] SetupInitialAdminDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Invalid setup payload." });

            var result = await _authService.SetupInitialAdminAsync(
                request.Username,
                request.Email,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "SETUP_ALREADY_COMPLETED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                return BadRequest(new { error = result.ErrorCode, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
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
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                request.RememberDevice);

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCESS_DENIED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "ACCOUNT_SUSPENDED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "ACCOUNT_LOCKED") return StatusCode(429, new { error = result.ErrorCode, message = result.Message });
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            if (result.Requires2FA)
            {
                return Ok(new
                {
                    success = true,
                    requires2FA = true,
                    message = result.Message,
                    twoFactorTicket = result.TwoFactorTicket,
                    user = result.User
                });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
            });
        }

        [HttpPost("2fa/verify")]
        public async Task<IActionResult> Verify2FaAsync([FromBody] Verify2FaDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TwoFactorTicket) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "2FA ticket and verification code are required." });
            }

            var result = await _authService.Verify2FaAsync(
                request.TwoFactorTicket,
                request.Code,
                request.IsRecoveryCode,
                request.InstallationId,
                Request.Headers["User-Agent"].ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCESS_DENIED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
            });
        }

        [Authorize]
        [HttpPost("2fa/setup")]
        public async Task<IActionResult> Setup2FaAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.Generate2FaSetupAsync(userId.Value);
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                secret = result.TotpSecret,
                qrCodeUri = result.QrCodeUri
            });
        }

        [Authorize]
        [HttpPost("2fa/confirm")]
        public async Task<IActionResult> Confirm2FaAsync([FromBody] Confirm2FaDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Verification code is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            try
            {
                var result = await _authService.Confirm2FaSetupAsync(userId.Value, request.Code);
                if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    recoveryCodes = result.RecoveryCodes
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { error = "CONCURRENCY_CONFLICT", message = "A concurrency conflict occurred. Please retry the operation." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred while confirming 2FA." });
            }
        }

        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<IActionResult> Disable2FaAsync([FromBody] Disable2FaDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Password is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.Disable2FaAsync(userId.Value, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpPost("2fa/recovery-codes")]
        [HttpPost("2fa/regenerate-recovery-codes")]
        public async Task<IActionResult> RegenerateRecoveryCodesAsync([FromBody] RegenerateRecoveryCodesDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Password is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.RegenerateRecoveryCodesAsync(userId.Value, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message, recoveryCodes = result.RecoveryCodes });
        }

        [Authorize]
        [HttpPost("recovery-email/request")]
        public async Task<IActionResult> RequestRecoveryEmailAsync([FromBody] RequestRecoveryEmailDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.NewRecoveryEmail))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Password and valid recovery email are required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.RequestRecoveryEmailChangeAsync(userId.Value, request.Password, request.NewRecoveryEmail, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpPost("recovery-email/confirm")]
        public async Task<IActionResult> ConfirmRecoveryEmailAsync([FromBody] ConfirmRecoveryEmailDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Verification token is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.ConfirmRecoveryEmailChangeAsync(userId.Value, request.Token, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpGet("security-overview")]
        public async Task<IActionResult> GetSecurityOverviewAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var overview = await _authService.GetSecurityOverviewAsync(userId.Value);
            return Ok(overview);
        }

        [HttpPost("google")]
        [HttpPost("google/login")]
        public async Task<IActionResult> GoogleLoginAsync([FromBody] GoogleLoginDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Google ID token is required." });
            }

            var result = await _authService.VerifyGoogleLoginAsync(
                request.IdToken,
                request.InstallationId,
                Request.Headers["User-Agent"].ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "UNAUTHORIZED_GOOGLE_ACCOUNT" || result.ErrorCode == "FORBIDDEN" || result.ErrorCode == "ACCESS_DENIED")
                    return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "ACCOUNT_SUSPENDED")
                    return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                return BadRequest(new { error = result.ErrorCode ?? "INVALID_TOKEN", message = result.Message });
            }

            if (result.Requires2FA)
            {
                return Ok(new
                {
                    success = true,
                    requires2FA = true,
                    message = result.Message,
                    twoFactorTicket = result.TwoFactorTicket,
                    user = result.User
                });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
            });
        }

        [HttpPost("firebase")]
        [HttpPost("firebase/login")]
        public async Task<IActionResult> FirebaseLoginAsync([FromBody] FirebaseLoginDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Firebase ID token is required." });
            }

            var result = await _authService.VerifyFirebaseLoginAsync(
                request.IdToken,
                request.InstallationId,
                Request.Headers["User-Agent"].ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCOUNT_SUSPENDED" || result.ErrorCode == "ACCESS_DENIED")
                    return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                return BadRequest(new { error = result.ErrorCode ?? "INVALID_TOKEN", message = result.Message });
            }

            if (result.Requires2FA)
            {
                return Ok(new
                {
                    success = true,
                    requires2FA = true,
                    message = result.Message,
                    twoFactorTicket = result.TwoFactorTicket,
                    user = result.User
                });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
            });
        }

        [HttpGet("passkey/login-options")]
        public IActionResult GetPasskeyLoginOptions()
        {
            var options = _passkeyService.CreateAssertionOptions();
            return Ok(options);
        }

        [HttpPost("passkey/login-verify")]
        public async Task<IActionResult> VerifyPasskeyLoginAsync([FromBody] PasskeyLoginDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CredentialId))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Passkey credential details required." });
            }

            var result = await _authService.VerifyPasskeyLoginAsync(
                request.CredentialId,
                request.ClientDataJson,
                request.AuthenticatorData,
                request.Signature,
                request.InstallationId,
                Request.Headers["User-Agent"].ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
            });
        }

        [Authorize]
        [HttpGet("passkey/register-options")]
        public async Task<IActionResult> GetPasskeyRegisterOptions()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { error = "USER_NOT_FOUND" });

            var options = _passkeyService.CreateRegistrationOptions(user.Username, user.Username, user.Id);
            return Ok(options);
        }

        [Authorize]
        [HttpPost("passkey/register-verify")]
        public async Task<IActionResult> RegisterPasskeyAsync([FromBody] PasskeyRegisterDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Passkey attestation required." });

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.RegisterPasskeyAsync(userId.Value, request.ClientDataJson, request.AttestationObject, request.DeviceName);
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> GetPasskeysAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var passkeys = await _authService.GetUserPasskeysAsync(userId.Value);
            return Ok(passkeys);
        }

        [Authorize]
        [HttpDelete("passkeys/{id}")]
        public async Task<IActionResult> DeletePasskeyAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.DeletePasskeyAsync(userId.Value, id);
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [Authorize]
        [HttpPost("passkeys/{id}/rename")]
        [HttpPatch("passkeys/{id}")]
        public async Task<IActionResult> RenamePasskeyAsync([FromRoute] Guid id, [FromBody] RenamePasskeyDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewName))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "New passkey name is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED", message = "User context missing." });

            var result = await _authService.RenamePasskeyAsync(userId.Value, id, request.NewName);
            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Email is required." });
            }

            var result = await _authService.ForgotPasswordAsync(request.Email, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { error = "INVALID_REQUEST", message = "Reset token and new password are required." });
            }

            var result = await _authService.ResetPasswordAsync(
                request.Token,
                request.NewPassword,
                request.TwoFactorCode,
                request.IsRecoveryCode,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success) return BadRequest(new { error = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshAsync([FromBody] RefreshDto request)
        {
            string? refresh = request?.RefreshToken;
            if (string.IsNullOrWhiteSpace(refresh))
            {
                return BadRequest(new { error = "INVALID_TOKEN", message = "Refresh token is required." });
            }

            var result = await _authService.RefreshTokenAsync(
                refresh,
                request?.InstallationId,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Success)
            {
                if (result.ErrorCode == "ACCESS_DENIED") return StatusCode(403, new { error = result.ErrorCode, message = result.Message });
                if (result.ErrorCode == "TOKEN_REUSE") return StatusCode(401, new { error = result.ErrorCode, message = result.Message });
                return Unauthorized(new { error = result.ErrorCode ?? "UNAUTHORIZED", message = result.Message });
            }

            SetAuthCookie(result.AccessToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresInSeconds = result.ExpiresInSeconds,
                user = result.User,
                csrfToken = _csrfService.GenerateCsrfToken(HttpContext)
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

            Response.Cookies.Delete("edm_admin_jwt", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });

            return Ok(new { success = true, message = "Logged out successfully." });
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAllAsync()
        {
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                await _authService.LogoutAllAsync(userId.Value, "LOGOUT_ALL_REQUESTED");
            }

            Response.Cookies.Delete("edm_admin_jwt", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });

            return Ok(new { success = true, message = "All active sessions revoked." });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_REQUEST", message = "Invalid password change payload." });

            var userId = GetCurrentUserId();
            var sessionClaim = User.FindFirst("session_id");

            if (userId == null || sessionClaim == null || !Guid.TryParse(sessionClaim.Value, out var sessionId))
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "Invalid token context." });
            }

            var result = await _authService.ChangePasswordAsync(userId.Value, request.OldPassword, request.NewPassword, sessionId);
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
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            var user = await _dbContext.Users.FindAsync(userId.Value);
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
                twoFactorEnabled = user.TwoFactorEnabled,
                createdAtUtc = user.CreatedAtUtc
            });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetMySessionsAsync()
        {
            var userId = GetCurrentUserId();
            var sessionClaim = User.FindFirst("session_id");

            if (userId == null)
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            Guid currentSessionId = Guid.Empty;
            if (sessionClaim != null) Guid.TryParse(sessionClaim.Value, out currentSessionId);

            var sessions = await _dbContext.Sessions
                .Where(s => s.UserId == userId.Value && !s.IsRevoked)
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
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "User claim missing." });
            }

            var session = await _dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId.Value);
            if (session == null)
            {
                return NotFound(new { error = "SESSION_NOT_FOUND", message = "Session not found or does not belong to you." });
            }

            await _authService.LogoutAsync(sessionId, "USER_REVOKED_REMOTE_SESSION");
            return Ok(new { success = true, message = "Session revoked successfully." });
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }

        private void SetAuthCookie(string? accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return;

            Response.Cookies.Append("edm_admin_jwt", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });
        }
    }
}
