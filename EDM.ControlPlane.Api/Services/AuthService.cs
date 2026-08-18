using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
        string? ErrorCode = null,
        bool Requires2FA = false,
        string? TwoFactorTicket = null,
        List<string>? RecoveryCodes = null,
        string? TotpSecret = null,
        string? QrCodeUri = null);

    public record UserDto(Guid Id, string Username, string Email, string Role, bool TwoFactorEnabled = false);

    public record SecurityOverviewDto(
        bool TwoFactorEnabled,
        bool HasRecoveryEmail,
        string? RecoveryEmail,
        bool IsRecoveryEmailVerified,
        int ActivePasskeysCount,
        int ActiveSessionsCount,
        int RemainingRecoveryCodesCount);

    public interface IAuthService
    {
        Task<AuthResult> SetupInitialAdminAsync(string username, string email, string password, string? rawIp = null);
        Task<AuthResult> RegisterAsync(string username, string email, string password, string? rawIp = null);
        Task<AuthResult> LoginAsync(string usernameOrEmail, string password, Guid? installationId, string? userAgent = null, string? rawIp = null, bool rememberDevice = false);
        Task<AuthResult> Verify2FaAsync(string twoFactorTicket, string code, bool isRecoveryCode, Guid? installationId, string? userAgent = null, string? rawIp = null);
        Task<AuthResult> VerifyGoogleLoginAsync(string googleIdToken, Guid? installationId, string? userAgent = null, string? rawIp = null);
        Task<AuthResult> RefreshTokenAsync(string refreshToken, Guid? installationId, string? rawIp = null);
        Task<bool> LogoutAsync(Guid sessionId, string reason = "USER_LOGOUT");
        Task<bool> LogoutAllAsync(Guid userId, string reason = "LOGOUT_ALL");
        Task<AuthResult> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, Guid currentSessionId);
        Task<AuthResult> Generate2FaSetupAsync(Guid userId);
        Task<AuthResult> Confirm2FaSetupAsync(Guid userId, string code);
        Task<AuthResult> Disable2FaAsync(Guid userId, string password, string? rawIp = null);
        Task<AuthResult> RegenerateRecoveryCodesAsync(Guid userId, string password, string? rawIp = null);
        Task<AuthResult> RequestRecoveryEmailChangeAsync(Guid userId, string currentPassword, string newRecoveryEmail, string? rawIp = null);
        Task<AuthResult> ConfirmRecoveryEmailChangeAsync(Guid userId, string token, string? rawIp = null);
        Task<SecurityOverviewDto> GetSecurityOverviewAsync(Guid userId);
        Task<AuthResult> ForgotPasswordAsync(string email, string? rawIp = null);
        Task<AuthResult> ResetPasswordAsync(string resetToken, string newPassword, string? twoFactorCode = null, bool isRecoveryCode = false, string? rawIp = null);
        Task<Session?> ValidateSessionAsync(Guid sessionId);
        Task<AuthResult> RegisterPasskeyAsync(Guid userId, string clientDataJson, string attestationObject, string deviceName);
        Task<AuthResult> VerifyPasskeyLoginAsync(string credentialId, string clientDataJson, string authenticatorData, string signature, Guid? installationId, string? userAgent = null, string? rawIp = null);
    }

    public class AuthService : IAuthService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IPrivacySafeDeviceService _deviceService;
        private readonly IBanEnforcementService _banService;
        private readonly IAuditLoggingService _auditLogger;
        private readonly ITotpService _totpService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IPasskeyService _passkeyService;

        private const int RefreshTokenLifetimeDays = 30;
        private const int SessionIdleTimeoutHours = 24 * 7; // 7 days idle timeout
        private static readonly System.Threading.SemaphoreSlim _refreshLock = new(1, 1);
        private static readonly ConcurrentDictionary<string, (Guid UserId, Guid? InstallationId, string? UserAgent, DateTime Expiry)> _pending2faTickets = new();
        private static readonly ConcurrentDictionary<string, (int FailedAttempts, DateTime LockoutUntil)> _failedLoginTrackers = new();

        public AuthService(
            ControlPlaneDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IPrivacySafeDeviceService deviceService,
            IBanEnforcementService banService,
            IAuditLoggingService auditLogger,
            ITotpService totpService,
            IGoogleAuthService googleAuthService,
            IPasskeyService passkeyService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _banService = banService ?? throw new ArgumentNullException(nameof(banService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
            _googleAuthService = googleAuthService ?? throw new ArgumentNullException(nameof(googleAuthService));
            _passkeyService = passkeyService ?? throw new ArgumentNullException(nameof(passkeyService));
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

            return new AuthResult(true, "User registered successfully.", User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString(), false));
        }

        public async Task<AuthResult> SetupInitialAdminAsync(string username, string email, string password, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult(false, "Username, email, and password are required.", ErrorCode: "INVALID_INPUT");
            }

            if (password.Length < 8)
            {
                return new AuthResult(false, "Password must be at least 8 characters long.", ErrorCode: "WEAK_PASSWORD");
            }

            // Check if any Super Admin already exists
            if (await _dbContext.Users.AnyAsync(u => u.Role == UserRole.SUPER_ADMIN))
            {
                return new AuthResult(false, "Initial setup has already been completed. Super Admin already exists.", ErrorCode: "SETUP_ALREADY_COMPLETED");
            }

            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanUsername = username.Trim();

            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail || u.Username == cleanUsername);
            if (existingUser != null)
            {
                existingUser.Role = UserRole.SUPER_ADMIN;
                existingUser.PasswordHash = _passwordHasher.HashPassword(password);
                existingUser.IsActive = true;
                existingUser.IsEmailVerified = true;
                existingUser.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var superAdmin = new User
                {
                    Id = Guid.NewGuid(),
                    Username = cleanUsername,
                    Email = cleanEmail,
                    PasswordHash = _passwordHasher.HashPassword(password),
                    Role = UserRole.SUPER_ADMIN,
                    IsActive = true,
                    IsEmailVerified = true,
                    TwoFactorEnabled = false,
                    MustChangePassword = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Users.Add(superAdmin);
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: null,
                actorUsername: cleanUsername,
                action: "INITIAL_SUPERADMIN_SETUP",
                targetEntity: "User",
                targetId: cleanEmail,
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS",
                rawIpAddress: rawIp);

            return new AuthResult(true, "Super Admin account initialized successfully. You may now log in.");
        }

        public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password, Guid? installationId, string? userAgent = null, string? rawIp = null, bool rememberDevice = false)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult(false, "Invalid credentials.", ErrorCode: "INVALID_CREDENTIALS");
            }

            var identifier = usernameOrEmail.Trim().ToLowerInvariant();

            // Check account lockout
            if (_failedLoginTrackers.TryGetValue(identifier, out var tracker) && tracker.LockoutUntil > DateTime.UtcNow)
            {
                return new AuthResult(false, $"Account is temporarily locked due to excessive failed attempts. Please try again after {tracker.LockoutUntil:HH:mm:ss} UTC.", ErrorCode: "ACCOUNT_LOCKED");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == identifier || u.Username.ToLower() == identifier);

            if (user == null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _failedLoginTrackers.AddOrUpdate(
                    identifier,
                    (1, DateTime.MinValue),
                    (key, old) =>
                    {
                        int attempts = old.FailedAttempts + 1;
                        DateTime lockout = attempts >= 5 ? DateTime.UtcNow.AddMinutes(15) : DateTime.MinValue;
                        return (attempts, lockout);
                    });

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

            // On success, reset lockout tracker
            _failedLoginTrackers.TryRemove(identifier, out _);

            if (!user.IsActive)
            {
                return new AuthResult(false, "Account is suspended.", ErrorCode: "ACCOUNT_SUSPENDED");
            }

            Guid installId = installationId ?? _deviceService.GenerateInstallationId();

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

            // If 2FA is enabled, issue challenge ticket and do NOT return full session yet
            if (user.TwoFactorEnabled)
            {
                byte[] ticketBytes = new byte[32];
                RandomNumberGenerator.Fill(ticketBytes);
                string ticket = Convert.ToBase64String(ticketBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

                _pending2faTickets[ticket] = (user.Id, installId, userAgent, DateTime.UtcNow.AddMinutes(5));

                // Clean expired tickets
                var now = DateTime.UtcNow;
                foreach (var kvp in _pending2faTickets)
                {
                    if (kvp.Value.Expiry < now) _pending2faTickets.TryRemove(kvp.Key, out _);
                }

                await _auditLogger.LogActionAsync(
                    actorId: user.Id,
                    actorUsername: user.Username,
                    action: "LOGIN_2FA_CHALLENGE_ISSUED",
                    targetEntity: "User",
                    targetId: user.Id.ToString(),
                    detailsJson: "{}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "PENDING_2FA",
                    rawIpAddress: rawIp);

                return new AuthResult(
                    Success: true,
                    Message: "Two-factor authentication code required.",
                    Requires2FA: true,
                    TwoFactorTicket: ticket);
            }

            return await IssueSessionAsync(user, installId, userAgent, rawIp, rememberDevice);
        }

        public async Task<AuthResult> Verify2FaAsync(string twoFactorTicket, string code, bool isRecoveryCode, Guid? installationId, string? userAgent = null, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(twoFactorTicket) || string.IsNullOrWhiteSpace(code))
            {
                return new AuthResult(false, "2FA ticket and verification code are required.", ErrorCode: "INVALID_2FA_REQUEST");
            }

            if (!_pending2faTickets.TryRemove(twoFactorTicket, out var ticketData) || ticketData.Expiry < DateTime.UtcNow)
            {
                return new AuthResult(false, "2FA challenge expired. Please log in again.", ErrorCode: "TICKET_EXPIRED");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == ticketData.UserId);
            if (user == null || !user.IsActive)
            {
                return new AuthResult(false, "User not found or inactive.", ErrorCode: "USER_INACTIVE");
            }

            bool verified = false;

            if (isRecoveryCode)
            {
                // Verify against single-use recovery code
                string cleanCode = code.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
                string codeHash = _tokenService.HashToken(cleanCode);

                var recCode = await _dbContext.RecoveryCodes.FirstOrDefaultAsync(r => r.UserId == user.Id && r.CodeHash == codeHash && !r.IsUsed);
                if (recCode != null)
                {
                    recCode.IsUsed = true;
                    recCode.UsedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    verified = true;

                    await _auditLogger.LogActionAsync(
                        actorId: user.Id,
                        actorUsername: user.Username,
                        action: "2FA_RECOVERY_CODE_USED",
                        targetEntity: "AdminRecoveryCode",
                        targetId: recCode.Id.ToString(),
                        detailsJson: "{}",
                        correlationId: Guid.NewGuid().ToString("N"),
                        resultStatus: "SUCCESS",
                        rawIpAddress: rawIp);
                }
            }
            else
            {
                // Verify TOTP
                if (!string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                {
                    verified = _totpService.VerifyCode(user.TwoFactorSecret, code);
                }
            }

            if (!verified)
            {
                await _auditLogger.LogActionAsync(
                    actorId: user.Id,
                    actorUsername: user.Username,
                    action: isRecoveryCode ? "2FA_RECOVERY_CODE_FAILED" : "2FA_TOTP_FAILED",
                    targetEntity: "User",
                    targetId: user.Id.ToString(),
                    detailsJson: "{}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "DENIED",
                    rawIpAddress: rawIp);

                return new AuthResult(false, isRecoveryCode ? "Invalid or already used recovery code." : "Invalid 2FA authentication code.", ErrorCode: "INVALID_2FA_CODE");
            }

            Guid installId = installationId ?? ticketData.InstallationId ?? _deviceService.GenerateInstallationId();
            return await IssueSessionAsync(user, installId, userAgent ?? ticketData.UserAgent, rawIp);
        }

        public async Task<AuthResult> VerifyGoogleLoginAsync(string googleIdToken, Guid? installationId, string? userAgent = null, string? rawIp = null)
        {
            var googlePayload = await _googleAuthService.ValidateGoogleTokenAsync(googleIdToken);
            if (googlePayload == null || !googlePayload.EmailVerified || string.IsNullOrWhiteSpace(googlePayload.Email))
            {
                return new AuthResult(false, "Invalid, unverified, or expired Google ID token.", ErrorCode: "INVALID_GOOGLE_TOKEN");
            }

            string email = googlePayload.Email.ToLowerInvariant().Trim();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email || (u.GoogleSubjectId != null && u.GoogleSubjectId == googlePayload.Subject));

            if (user == null)
            {
                await _auditLogger.LogActionAsync(
                    actorId: null,
                    actorUsername: email,
                    action: "GOOGLE_LOGIN_UNAUTHORIZED",
                    targetEntity: "User",
                    targetId: email,
                    detailsJson: $"{{\"googleSubject\":\"{googlePayload.Subject}\"}}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "DENIED",
                    rawIpAddress: rawIp);

                return new AuthResult(false, "This Google identity is not authorized to access the EDM Super Admin portal.", ErrorCode: "UNAUTHORIZED_GOOGLE_ACCOUNT");
            }

            if (user.Role != UserRole.SUPER_ADMIN && user.Role != UserRole.ADMIN && user.Role != UserRole.SUPPORT && user.Role != UserRole.RELEASE_MANAGER && user.Role != UserRole.ANALYST)
            {
                return new AuthResult(false, "This Google account lacks administrative access privileges.", ErrorCode: "FORBIDDEN");
            }

            if (string.IsNullOrEmpty(user.GoogleSubjectId))
            {
                user.GoogleSubjectId = googlePayload.Subject;
                user.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                return new AuthResult(false, "Account is suspended.", ErrorCode: "ACCOUNT_SUSPENDED");
            }

            Guid installId = installationId ?? _deviceService.GenerateInstallationId();

            if (await _banService.IsRequestBannedAsync(user.Id, installId, rawIp))
            {
                return new AuthResult(false, "Access denied.", ErrorCode: "ACCESS_DENIED");
            }

            if (user.TwoFactorEnabled)
            {
                byte[] ticketBytes = new byte[32];
                RandomNumberGenerator.Fill(ticketBytes);
                string ticket = Convert.ToBase64String(ticketBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
                _pending2faTickets[ticket] = (user.Id, installId, userAgent, DateTime.UtcNow.AddMinutes(5));

                await _auditLogger.LogActionAsync(
                    actorId: user.Id,
                    actorUsername: user.Username,
                    action: "GOOGLE_LOGIN_2FA_CHALLENGE_ISSUED",
                    targetEntity: "User",
                    targetId: user.Id.ToString(),
                    detailsJson: "{}",
                    correlationId: Guid.NewGuid().ToString("N"),
                    resultStatus: "PENDING_2FA",
                    rawIpAddress: rawIp);

                return new AuthResult(
                    Success: true,
                    Message: "Two-factor authentication code required.",
                    Requires2FA: true,
                    TwoFactorTicket: ticket);
            }

            return await IssueSessionAsync(user, installId, userAgent, rawIp);
        }

        private async Task<AuthResult> IssueSessionAsync(User user, Guid installId, string? userAgent, string? rawIp, bool rememberDevice = false)
        {
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

            int sessionLifetimeDays = rememberDevice ? 90 : RefreshTokenLifetimeDays;
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
                ExpiresAtUtc = DateTime.UtcNow.AddDays(sessionLifetimeDays),
                CreatedAtUtc = DateTime.UtcNow
            };

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
                ExpiresAtUtc = DateTime.UtcNow.AddDays(sessionLifetimeDays),
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Sessions.Add(session);
            _dbContext.RefreshTokens.Add(refreshTokenEntity);

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
                User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString(), user.TwoFactorEnabled));
        }

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

                var tokenRecord = await _dbContext.RefreshTokens
                    .Include(r => r.Session)
                    .Include(r => r.User)
                    .Include(r => r.Device)
                    .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

                if (tokenRecord == null)
                {
                    return new AuthResult(false, "Invalid refresh token.", ErrorCode: "INVALID_TOKEN");
                }

                if (tokenRecord.IsUsed || tokenRecord.IsRevoked)
                {
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

                if (tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
                {
                    tokenRecord.IsRevoked = true;
                    tokenRecord.RevokedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new AuthResult(false, "Refresh token has expired.", ErrorCode: "TOKEN_EXPIRED");
                }

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

                if (await _banService.IsRequestBannedAsync(user.Id, installId, rawIp))
                {
                    session.IsRevoked = true;
                    session.RevocationReason = "ACCOUNT_BANNED";
                    session.RevokedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new AuthResult(false, "Access denied.", ErrorCode: "ACCESS_DENIED");
                }

                tokenRecord.IsUsed = true;
                tokenRecord.UsedAtUtc = DateTime.UtcNow;

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
                    User: new UserDto(user.Id, user.Username, user.Email, user.Role.ToString(), user.TwoFactorEnabled));
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
            user.MustChangePassword = false;
            user.UpdatedAtUtc = DateTime.UtcNow;

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

        public async Task<AuthResult> Generate2FaSetupAsync(Guid userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");

            string secret = _totpService.GenerateSecret(20);
            user.TwoFactorSecret = secret;
            await _dbContext.SaveChangesAsync();

            string qrUri = _totpService.GenerateQrCodeUri(user.Email, secret, "EDM Control Plane");

            return new AuthResult(
                Success: true,
                Message: "2FA setup secret generated.",
                TotpSecret: secret,
                QrCodeUri: qrUri);
        }

        public async Task<AuthResult> Confirm2FaSetupAsync(Guid userId, string code)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)) return new AuthResult(false, "2FA setup has not been initiated.", ErrorCode: "SETUP_NOT_INITIATED");

            if (!_totpService.VerifyCode(user.TwoFactorSecret, code))
            {
                return new AuthResult(false, "Invalid verification code. 2FA not confirmed.", ErrorCode: "INVALID_CODE");
            }

            user.TwoFactorEnabled = true;
            user.UpdatedAtUtc = DateTime.UtcNow;

            // Generate 8 single-use recovery codes
            var rawCodes = GenerateRawRecoveryCodes(8);

            // Invalidate old recovery codes
            var existingCodes = await _dbContext.RecoveryCodes.Where(r => r.UserId == userId).ToListAsync();
            if (existingCodes.Any())
            {
                _dbContext.RecoveryCodes.RemoveRange(existingCodes);
            }

            foreach (var raw in rawCodes)
            {
                string hash = _tokenService.HashToken(raw.Replace("-", ""));
                _dbContext.RecoveryCodes.Add(new AdminRecoveryCode
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CodeHash = hash,
                    IsUsed = false,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "2FA_ENABLED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS");

            return new AuthResult(
                Success: true,
                Message: "Two-Factor Authentication successfully activated.",
                RecoveryCodes: rawCodes);
        }

        public async Task<AuthResult> Disable2FaAsync(Guid userId, string password, string? rawIp = null)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");
            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                return new AuthResult(false, "Password verification failed.", ErrorCode: "INVALID_CREDENTIALS");
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.UpdatedAtUtc = DateTime.UtcNow;

            // Delete all recovery codes
            var existingCodes = await _dbContext.RecoveryCodes.Where(r => r.UserId == userId).ToListAsync();
            if (existingCodes.Any())
            {
                _dbContext.RecoveryCodes.RemoveRange(existingCodes);
            }

            // Invalidate existing sessions for security
            var sessions = await _dbContext.Sessions.Where(s => s.UserId == userId && !s.IsRevoked).ToListAsync();
            foreach (var s in sessions)
            {
                s.IsRevoked = true;
                s.RevocationReason = "2FA_DISABLED";
                s.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "2FA_DISABLED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS",
                rawIpAddress: rawIp);

            return new AuthResult(true, "Two-Factor Authentication disabled. All other sessions have been terminated.");
        }

        public async Task<AuthResult> RegenerateRecoveryCodesAsync(Guid userId, string password, string? rawIp = null)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");
            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                return new AuthResult(false, "Password verification failed.", ErrorCode: "INVALID_CREDENTIALS");
            }

            if (!user.TwoFactorEnabled)
            {
                return new AuthResult(false, "2FA must be enabled to generate recovery codes.", ErrorCode: "2FA_NOT_ENABLED");
            }

            var existingCodes = await _dbContext.RecoveryCodes.Where(r => r.UserId == userId).ToListAsync();
            if (existingCodes.Any())
            {
                _dbContext.RecoveryCodes.RemoveRange(existingCodes);
            }

            var rawCodes = GenerateRawRecoveryCodes(8);
            foreach (var raw in rawCodes)
            {
                string hash = _tokenService.HashToken(raw.Replace("-", ""));
                _dbContext.RecoveryCodes.Add(new AdminRecoveryCode
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CodeHash = hash,
                    IsUsed = false,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "RECOVERY_CODES_REGENERATED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS",
                rawIpAddress: rawIp);

            return new AuthResult(true, "New recovery codes generated successfully.", RecoveryCodes: rawCodes);
        }

        public async Task<AuthResult> RequestRecoveryEmailChangeAsync(Guid userId, string currentPassword, string newRecoveryEmail, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(newRecoveryEmail) || !newRecoveryEmail.Contains('@'))
            {
                return new AuthResult(false, "A valid recovery email address is required.", ErrorCode: "INVALID_EMAIL");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");

            if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
            {
                return new AuthResult(false, "Re-authentication failed. Incorrect password.", ErrorCode: "INVALID_CREDENTIALS");
            }

            string cleanNewEmail = newRecoveryEmail.Trim().ToLowerInvariant();

            byte[] tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            string plaintextToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            string tokenHash = _tokenService.HashToken(plaintextToken);

            user.PendingRecoveryEmail = cleanNewEmail;
            user.RecoveryEmailTokenHash = tokenHash;
            user.RecoveryEmailTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "RECOVERY_EMAIL_CHANGE_REQUESTED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: $"{{\"pendingEmail\":\"{cleanNewEmail}\"}}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "PENDING_VERIFICATION",
                rawIpAddress: rawIp);

            return new AuthResult(
                Success: true,
                Message: "Recovery email change initiated. Verification token has been dispatched to the requested address.",
                AccessToken: plaintextToken);
        }

        public async Task<AuthResult> ConfirmRecoveryEmailChangeAsync(Guid userId, string token, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthResult(false, "Verification token is required.", ErrorCode: "INVALID_TOKEN");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");

            string tokenHash = _tokenService.HashToken(token.Trim());
            var now = DateTime.UtcNow;

            if (user.RecoveryEmailTokenHash != tokenHash || user.RecoveryEmailTokenExpiresAtUtc == null || user.RecoveryEmailTokenExpiresAtUtc < now || string.IsNullOrWhiteSpace(user.PendingRecoveryEmail))
            {
                return new AuthResult(false, "Invalid or expired recovery email verification token.", ErrorCode: "INVALID_VERIFICATION_TOKEN");
            }

            user.RecoveryEmail = user.PendingRecoveryEmail;
            user.IsRecoveryEmailVerified = true;
            user.PendingRecoveryEmail = null;
            user.RecoveryEmailTokenHash = null;
            user.RecoveryEmailTokenExpiresAtUtc = null;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "RECOVERY_EMAIL_VERIFIED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: $"{{\"verifiedRecoveryEmail\":\"{user.RecoveryEmail}\"}}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS",
                rawIpAddress: rawIp);

            return new AuthResult(true, "Recovery email verified and updated successfully.");
        }

        public async Task<SecurityOverviewDto> GetSecurityOverviewAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.Sessions)
                .Include(u => u.Passkeys)
                .Include(u => u.RecoveryCodes)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return new SecurityOverviewDto(false, false, null, false, 0, 0, 0);
            }

            int activeSessions = user.Sessions.Count(s => !s.IsRevoked && s.ExpiresAtUtc > DateTime.UtcNow);
            int activePasskeys = user.Passkeys.Count;
            int remainingRecoveryCodes = user.RecoveryCodes.Count(r => !r.IsUsed);

            return new SecurityOverviewDto(
                TwoFactorEnabled: user.TwoFactorEnabled,
                HasRecoveryEmail: !string.IsNullOrWhiteSpace(user.RecoveryEmail),
                RecoveryEmail: user.RecoveryEmail,
                IsRecoveryEmailVerified: user.IsRecoveryEmailVerified,
                ActivePasskeysCount: activePasskeys,
                ActiveSessionsCount: activeSessions,
                RemainingRecoveryCodesCount: remainingRecoveryCodes);
        }

        public async Task<AuthResult> ForgotPasswordAsync(string email, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new AuthResult(false, "Email is required.", ErrorCode: "INVALID_INPUT");
            }

            string cleanEmail = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail || (u.RecoveryEmail == cleanEmail && u.IsRecoveryEmailVerified));

            // Generic success message to prevent user enumeration
            if (user == null || !user.IsActive)
            {
                return new AuthResult(true, "If an account matches that email address, password reset instructions have been dispatched.");
            }

            byte[] resetBytes = new byte[32];
            RandomNumberGenerator.Fill(resetBytes);
            string plaintextToken = Convert.ToBase64String(resetBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            string tokenHash = _tokenService.HashToken(plaintextToken);

            user.PasswordResetTokenHash = tokenHash;
            user.PasswordResetExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "PASSWORD_RESET_REQUESTED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                rawIpAddress: rawIp);

            // Return reset token for local test/dev environments
            return new AuthResult(
                Success: true,
                Message: "If an account matches that email address, password reset instructions have been dispatched.",
                AccessToken: plaintextToken);
        }

        public async Task<AuthResult> ResetPasswordAsync(string resetToken, string newPassword, string? twoFactorCode = null, bool isRecoveryCode = false, string? rawIp = null)
        {
            if (string.IsNullOrWhiteSpace(resetToken) || string.IsNullOrWhiteSpace(newPassword))
            {
                return new AuthResult(false, "Reset token and new password are required.", ErrorCode: "INVALID_INPUT");
            }

            if (newPassword.Length < 8)
            {
                return new AuthResult(false, "Password must be at least 8 characters long.", ErrorCode: "WEAK_PASSWORD");
            }

            string tokenHash = _tokenService.HashToken(resetToken);
            var now = DateTime.UtcNow;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash && u.PasswordResetExpiresAtUtc > now);
            if (user == null)
            {
                return new AuthResult(false, "Invalid or expired password reset token.", ErrorCode: "INVALID_RESET_TOKEN");
            }

            // Strict Multi-Layer Identity Verification for 2FA accounts
            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(twoFactorCode))
                {
                    return new AuthResult(false, "Two-factor authentication code or recovery code is required to complete password reset.", ErrorCode: "MFA_REQUIRED");
                }

                bool mfaVerified = false;
                if (isRecoveryCode)
                {
                    string cleanCode = twoFactorCode.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
                    string codeHash = _tokenService.HashToken(cleanCode);
                    var recCode = await _dbContext.RecoveryCodes.FirstOrDefaultAsync(r => r.UserId == user.Id && r.CodeHash == codeHash && !r.IsUsed);
                    if (recCode != null)
                    {
                        recCode.IsUsed = true;
                        recCode.UsedAtUtc = DateTime.UtcNow;
                        mfaVerified = true;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                {
                    mfaVerified = _totpService.VerifyCode(user.TwoFactorSecret, twoFactorCode.Trim());
                }

                if (!mfaVerified)
                {
                    await _auditLogger.LogActionAsync(
                        actorId: user.Id,
                        actorUsername: user.Username,
                        action: "PASSWORD_RESET_2FA_FAILED",
                        targetEntity: "User",
                        targetId: user.Id.ToString(),
                        detailsJson: "{}",
                        correlationId: Guid.NewGuid().ToString("N"),
                        resultStatus: "DENIED",
                        rawIpAddress: rawIp);

                    return new AuthResult(false, "Invalid 2FA verification code.", ErrorCode: "INVALID_2FA_CODE");
                }
            }

            user.PasswordHash = _passwordHasher.HashPassword(newPassword);
            user.PasswordResetTokenHash = null;
            user.PasswordResetExpiresAtUtc = null;
            user.MustChangePassword = false;
            user.UpdatedAtUtc = DateTime.UtcNow;

            // Revoke all existing sessions
            var sessions = await _dbContext.Sessions.Where(s => s.UserId == user.Id && !s.IsRevoked).ToListAsync();
            foreach (var s in sessions)
            {
                s.IsRevoked = true;
                s.RevocationReason = "PASSWORD_RESET";
                s.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: user.Id,
                actorUsername: user.Username,
                action: "PASSWORD_RESET_COMPLETED",
                targetEntity: "User",
                targetId: user.Id.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"),
                rawIpAddress: rawIp);

            return new AuthResult(true, "Password has been reset successfully. Please log in with your new password.");
        }

        public async Task<AuthResult> RegisterPasskeyAsync(Guid userId, string clientDataJson, string attestationObject, string deviceName)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return new AuthResult(false, "User not found.", ErrorCode: "USER_NOT_FOUND");

            if (!_passkeyService.VerifyRegistration(clientDataJson, attestationObject, out string credentialId, out string publicKey))
            {
                return new AuthResult(false, "Passkey registration verification failed.", ErrorCode: "PASSKEY_VERIFICATION_FAILED");
            }

            var passkey = new UserPasskey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CredentialId = credentialId,
                PublicKey = publicKey,
                SignCount = 0,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Security Key" : deviceName.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.UserPasskeys.Add(passkey);
            await _dbContext.SaveChangesAsync();

            return new AuthResult(true, "Passkey enrolled successfully.");
        }

        public async Task<AuthResult> VerifyPasskeyLoginAsync(string credentialId, string clientDataJson, string authenticatorData, string signature, Guid? installationId, string? userAgent = null, string? rawIp = null)
        {
            var passkey = await _dbContext.UserPasskeys
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.CredentialId == credentialId);

            if (passkey == null || passkey.User == null || !passkey.User.IsActive)
            {
                return new AuthResult(false, "Invalid passkey credential.", ErrorCode: "INVALID_CREDENTIAL");
            }

            if (!_passkeyService.VerifyAssertion(clientDataJson, authenticatorData, signature, passkey.PublicKey, passkey.SignCount, out uint newSignCount))
            {
                return new AuthResult(false, "Passkey signature verification failed.", ErrorCode: "INVALID_SIGNATURE");
            }

            passkey.SignCount = newSignCount;
            passkey.LastUsedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            Guid installId = installationId ?? _deviceService.GenerateInstallationId();
            return await IssueSessionAsync(passkey.User, installId, userAgent, rawIp);
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

        private static List<string> GenerateRawRecoveryCodes(int count = 8)
        {
            var list = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                byte[] bytes = new byte[8];
                RandomNumberGenerator.Fill(bytes);
                string hex = Convert.ToHexString(bytes).ToUpperInvariant();
                list.Add($"{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}");
            }
            return list;
        }
    }
}
