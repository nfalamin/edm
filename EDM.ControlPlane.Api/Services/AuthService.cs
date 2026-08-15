using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record AuthResult(
        bool Success,
        string Message,
        string? AccessToken = null,
        string? RefreshToken = null,
        int? ExpiresInSeconds = null,
        UserDto? User = null,
        string? ErrorCode = null);

    public record UserDto(Guid Id, string Username, string Email, string Role);

    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(string username, string email, string password, string? rawIp = null);
        Task<AuthResult> LoginAsync(string usernameOrEmail, string password, Guid? installationId, string? userAgent = null, string? rawIp = null);
        Task<AuthResult> RefreshTokenAsync(string refreshToken, Guid? installationId, string? rawIp = null);
        Task<bool> LogoutAsync(Guid sessionId, string reason = "USER_LOGOUT");
        Task<bool> LogoutAllAsync(Guid userId, string reason = "LOGOUT_ALL");
        Task<AuthResult> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, Guid currentSessionId);
        Task<Session?> ValidateSessionAsync(Guid sessionId);
    }

    public class AuthService : IAuthService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IPrivacySafeDeviceService _deviceService;
        private readonly IBanEnforcementService _banService;
        private readonly IAuditLoggingService _auditLogger;

        private const int RefreshTokenLifetimeDays = 30;
        private const int SessionIdleTimeoutHours = 24 * 7; // 7 days idle timeout

        public AuthService(
            ControlPlaneDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IPrivacySafeDeviceService deviceService,
            IBanEnforcementService banService,
            IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _banService = banService ?? throw new ArgumentNullException(nameof(banService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<AuthResult> RegisterAsync(string username, string email, string password, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult(false, "Username, email and password are required.", ErrorCode: "INVALID_INPUT");
            }

            if (password.Length < 8)
            {
                return new AuthResult(false, "Password must be at least 8 characters long.", ErrorCode: "WEAK_PASSWORD");
            }

            string cleanUsername = username.Trim();
            string cleanEmail = email.Trim().ToLowerInvariant();

            bool exists = await _dbContext.Users.AnyAsync(u => u.Email == cleanEmail || u.Username == cleanUsername);
            if (exists)
            {
                // Generic response to prevent user enumeration
                return new AuthResult(false, "Registration could not be completed. Please try with different credentials.", ErrorCode: "CONFLICT");
            }

            string passwordHash = _passwordHasher.HashPassword(password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = cleanUsername,
                Email = cleanEmail,
                PasswordHash = passwordHash,
                Role = UserRole.USER,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "USER_REGISTER",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                rawIpAddress: rawIp);

            return new AuthResult(true, "User registered successfully.", User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString()));
        }

        public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password, Guid? installationId, string? userAgent = null, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult(false, "Invalid credentials.", ErrorCode: "INVALID_CREDENTIALS");
            }

            var identifier = usernameOrEmail.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == identifier || u.Username.ToLower() == identifier);

            if (user == null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                await _auditLogger.LogActionAsync(
                    actorId: null,
                    actorUsername: usernameOrEmail,
                    action: "LOGIN_FAILED",
                    targetEntity: "User",
                    targetId: null,
                    detailsJson: "{\"reason\":\"invalid_credentials\"}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "DENIED",
                    rawIpAddress: rawIp);

                return new AuthResult(false, "Invalid credentials.", ErrorCode: "INVALID_CREDENTIALS");
            }

            if (!user.IsActive)
            {
                return new AuthResult(false, "Account is suspended.", ErrorCode: "ACCOUNT_SUSPENDED");
            }

            Guid installId = installationId ?? _deviceService.GenerateInstallationId();

            // Check if user, installation or IP is banned
            if (await _banService.IsRequestBannedAsync(user.Id, installId, rawIp))
            {
                await _auditLogger.LogActionAsync(
                    actorId: user.Id,
                    actorUsername: user.Username,
                    action: "LOGIN_BANNED_ATTEMPT",
                    targetEntity: "User",
                    targetId: user.Id.ToString(),
                    detailsJson: "{}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "DENIED",
                    rawIpAddress: rawIp);

                return new AuthResult(false, "Access denied.", ErrorCode: "ACCESS_DENIED");
            }

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

            // Create new Session with token family
            Guid familyId = Guid.NewGuid();
            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceId = device.Id,
                FamilyId = familyId,
                UserAgent = userAgent ?? string.Empty,
                CoarseIpAddress = _deviceService.AnonymizeIpAddress(rawIp),
                IsRevoked = false,
                LastActivityAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
                CreatedAtUtc = DateTime.UtcNow
            };

            // Generate initial Refresh Token
            var (plaintextRefresh, refreshHash) = _tokenService.GenerateRefreshToken();
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                UserId = user.Id,
                DeviceId = device.Id,
                FamilyId = familyId,
                TokenHash = refreshHash,
                IsUsed = false,
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Sessions.Add(session);
            _dbContext.RefreshTokens.Add(refreshTokenEntity);

            // Generate Access Token
            string accessToken = _tokenService.GenerateAccessToken(user, session, installId);
            session.AccessTokenHash = _tokenService.HashToken(accessToken);

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "LOGIN_SUCCESS",
                targetEntity: "Session",
                targetId: session.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS",
                rawIpAddress: rawIp);

            return new AuthResult(
                Success: true,
                Message: "Login successful.",
                AccessToken: accessToken,
                RefreshToken: plaintextRefresh,
                ExpiresInSeconds: 15 * 60,
                User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString()));
        }

        private static readonly System.Threading.SemaphoreSlim _refreshLock = new(1, 1);

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken, Guid? installationId, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new AuthResult(false, "Refresh token is required.", ErrorCode: "INVALID_TOKEN");
            }

            await _refreshLock.WaitAsync();
            try
            {
                string tokenHash = _tokenService.HashToken(refreshToken);

                // Look up the refresh token record
                var tokenRecord = await _dbContext.RefreshTokens
                    .Include(r => r.Session)
                    .Include(r => r.User)
                    .Include(r => r.Device)
                    .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

                if (tokenRecord == null)
                {
                    return new AuthResult(false, "Invalid refresh token.", ErrorCode: "INVALID_TOKEN");
                }

                // REUSE DETECTION: If token was already used or revoked, revoke entire family!
                if (tokenRecord.IsUsed || tokenRecord.IsRevoked)
                {
                    // Revoke all tokens in this family and revoke the session immediately
                    var familyTokens = await _dbContext.RefreshTokens
                        .Where(r => r.FamilyId == tokenRecord.FamilyId && !r.IsRevoked)
                        .ToListAsync();

                    foreach (var t in familyTokens)
                    {
                        t.IsRevoked = true;
                        t.RevokedAtUtc = DateTime.UtcNow;
                    }

                    if (tokenRecord.Session != null)
                    {
                        tokenRecord.Session.IsRevoked = true;
                        tokenRecord.Session.RevocationReason = "TOKEN_REUSE_DETECTED";
                        tokenRecord.Session.RevokedAtUtc = DateTime.UtcNow;
                    }

                    await _dbContext.SaveChangesAsync();

                    await _auditLogger.LogActionAsync(
                        actorId: tokenRecord.UserId,
                        actorUsername: tokenRecord.User?.Username ?? "UNKNOWN",
                        action: "REFRESH_TOKEN_REUSE_DETECTED",
                        targetEntity: "Session",
                        targetId: tokenRecord.SessionId.ToString(),
                        detailsJson: $"{{\"familyId\":\"{tokenRecord.FamilyId}\"}}",
                        correlationId: Guid.NewGuid().ToString("N"),
                        resultStatus: "DENIED",
                        rawIpAddress: rawIp);

                    return new AuthResult(false, "Token reuse detected. Session has been revoked.", ErrorCode: "TOKEN_REUSE");
                }

                // Check if expired
                if (tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
                {
                    tokenRecord.IsRevoked = true;
                    tokenRecord.RevokedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new AuthResult(false, "Refresh token has expired.", ErrorCode: "TOKEN_EXPIRED");
                }

                // Check session status & idle timeout
                var session = tokenRecord.Session;
                if (session == null || session.IsRevoked || session.ExpiresAtUtc < DateTime.UtcNow)
                {
                    return new AuthResult(false, "Session is revoked or expired.", ErrorCode: "SESSION_EXPIRED");
                }

                if (DateTime.UtcNow - session.LastActivityAtUtc > TimeSpan.FromHours(SessionIdleTimeoutHours))
                {
                    session.IsRevoked = true;
                    session.RevocationReason = "IDLE_TIMEOUT";
                    session.RevokedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new AuthResult(false, "Session expired due to inactivity.", ErrorCode: "SESSION_IDLE_TIMEOUT");
                }

                var user = tokenRecord.User;
                if (user == null || !user.IsActive)
                {
                    return new AuthResult(false, "User is inactive.", ErrorCode: "USER_INACTIVE");
                }

                Guid installId = installationId ?? tokenRecord.Device?.InstallationId ?? Guid.NewGuid();

                // Check bans
                if (await _banService.IsRequestBannedAsync(user.Id, installId, rawIp))
                {
                    session.IsRevoked = true;
                    session.RevocationReason = "ACCOUNT_BANNED";
                    session.RevokedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new AuthResult(false, "Access denied.", ErrorCode: "ACCESS_DENIED");
                }

                // Mark current token as used
                tokenRecord.IsUsed = true;
                tokenRecord.UsedAtUtc = DateTime.UtcNow;

                // Generate new rotated refresh token in the SAME family
                var (newPlaintextRefresh, newRefreshHash) = _tokenService.GenerateRefreshToken();
                tokenRecord.ReplacedByTokenHash = newRefreshHash;

                var newRefreshTokenEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    UserId = user.Id,
                    DeviceId = session.DeviceId,
                    FamilyId = session.FamilyId,
                    TokenHash = newRefreshHash,
                    IsUsed = false,
                    IsRevoked = false,
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
                    CreatedAtUtc = DateTime.UtcNow
                };

                _dbContext.RefreshTokens.Add(newRefreshTokenEntity);

                // Update session activity
                session.LastActivityAtUtc = DateTime.UtcNow;
                string newAccessToken = _tokenService.GenerateAccessToken(user, session, installId);
                session.AccessTokenHash = _tokenService.HashToken(newAccessToken);

                await _dbContext.SaveChangesAsync();

                return new AuthResult(
                    Success: true,
                    Message: "Token refreshed successfully.",
                    AccessToken: newAccessToken,
                    RefreshToken: newPlaintextRefresh,
                    ExpiresInSeconds: 15 * 60,
                    User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString()));
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task<bool> LogoutAsync(Guid sessionId, string reason = "USER_LOGOUT")
        {
            var session = await _dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null || session.IsRevoked) return true;

            session.IsRevoked = true;
            session.RevocationReason = reason;
            session.RevokedAtUtc = DateTime.UtcNow;

            // Revoke all refresh tokens for this session
            var tokens = await _dbContext.RefreshTokens
                .Where(r => r.SessionId == sessionId && !r.IsRevoked)
                .ToListAsync();

            foreach (var t in tokens)
            {
                t.IsRevoked = true;
                t.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LogoutAllAsync(Guid userId, string reason = "LOGOUT_ALL")
        {
            var activeSessions = await _dbContext.Sessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .ToListAsync();

            foreach (var session in activeSessions)
            {
                session.IsRevoked = true;
                session.RevocationReason = reason;
                session.RevokedAtUtc = DateTime.UtcNow;
            }

            var activeTokens = await _dbContext.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
                t.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<AuthResult> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, Guid currentSessionId)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return new AuthResult(false, "New password must be at least 8 characters long.", ErrorCode: "WEAK_PASSWORD");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || !_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
            {
                return new AuthResult(false, "Current password is incorrect.", ErrorCode: "INVALID_CREDENTIALS");
            }

            user.PasswordHash = _passwordHasher.HashPassword(newPassword);
            user.UpdatedAtUtc = DateTime.UtcNow;

            // Invalidate all OTHER sessions for security
            var otherSessions = await _dbContext.Sessions
                .Where(s => s.UserId == userId && s.Id != currentSessionId && !s.IsRevoked)
                .ToListAsync();

            foreach (var s in otherSessions)
            {
                s.IsRevoked = true;
                s.RevocationReason = "PASSWORD_CHANGED";
                s.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "PASSWORD_CHANGED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"));

            return new AuthResult(true, "Password changed successfully.");
        }

        public async Task<Session?> ValidateSessionAsync(Guid sessionId)
        {
            var session = await _dbContext.Sessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsRevoked);

            if (session == null || session.ExpiresAtUtc < DateTime.UtcNow) return null;
            return session;
        }
    }
}
