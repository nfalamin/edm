using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EDM.ControlPlane.Api.Services
{
    public record GoogleUserPayload(string Email, string Name, string Subject, bool EmailVerified);

    public interface IGoogleAuthService
    {
        Task<GoogleUserPayload?> ValidateGoogleTokenAsync(string idToken);
    }

    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string? _expectedClientId;
        private static readonly HttpClient _httpClient = new();
        private static IList<SecurityKey>? _cachedSigningKeys;
        private static DateTime _keysExpiryUtc = DateTime.MinValue;
        private static readonly object _keysLock = new();

        public GoogleAuthService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _expectedClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? _configuration["Google:ClientId"];
        }

        public async Task<GoogleUserPayload?> ValidateGoogleTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken)) return null;

            try
            {
                var keys = await GetGoogleSigningKeysAsync();
                if (keys == null || keys.Count == 0) return null;

                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(idToken)) return null;

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = keys,
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" },
                    ValidateAudience = !string.IsNullOrWhiteSpace(_expectedClientId),
                    ValidAudience = _expectedClientId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                ClaimsPrincipal principal = handler.ValidateToken(idToken, validationParameters, out SecurityToken validatedToken);
                if (validatedToken == null) return null;

                string email = principal.FindFirst(ClaimTypes.Email)?.Value 
                    ?? principal.FindFirst("email")?.Value 
                    ?? string.Empty;
                string name = principal.FindFirst(ClaimTypes.Name)?.Value 
                    ?? principal.FindFirst("name")?.Value 
                    ?? string.Empty;
                string sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? principal.FindFirst("sub")?.Value 
                    ?? string.Empty;
                
                string? emailVerifiedStr = principal.FindFirst("email_verified")?.Value;
                bool emailVerified = bool.TryParse(emailVerifiedStr, out bool ev) && ev;

                if (string.IsNullOrWhiteSpace(email) || !emailVerified)
                {
                    return null;
                }

                return new GoogleUserPayload(email.ToLowerInvariant().Trim(), name, sub, emailVerified);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<IList<SecurityKey>> GetGoogleSigningKeysAsync()
        {
            lock (_keysLock)
            {
                if (_cachedSigningKeys != null && DateTime.UtcNow < _keysExpiryUtc)
                {
                    return _cachedSigningKeys;
                }
            }

            try
            {
                string jwksJson = await _httpClient.GetStringAsync("https://www.googleapis.com/oauth2/v3/certs");
                var keySet = new JsonWebKeySet(jwksJson);
                var keys = keySet.GetSigningKeys();

                lock (_keysLock)
                {
                    _cachedSigningKeys = keys;
                    _keysExpiryUtc = DateTime.UtcNow.AddHours(24);
                }

                return keys;
            }
            catch
            {
                lock (_keysLock)
                {
                    return _cachedSigningKeys ?? Array.Empty<SecurityKey>();
                }
            }
        }
    }
}
