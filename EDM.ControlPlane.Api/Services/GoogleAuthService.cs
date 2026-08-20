using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

        public GoogleAuthService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _expectedClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? _configuration["Google:ClientId"];
        }

        public Task<GoogleUserPayload?> ValidateGoogleTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken)) return Task.FromResult<GoogleUserPayload?>(null);

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(idToken)) return Task.FromResult<GoogleUserPayload?>(null);

                var jwt = handler.ReadJwtToken(idToken);

                // Verify issuer
                if (jwt.Issuer != "accounts.google.com" && jwt.Issuer != "https://accounts.google.com")
                {
                    return Task.FromResult<GoogleUserPayload?>(null);
                }

                // Verify expiry
                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    return Task.FromResult<GoogleUserPayload?>(null);
                }

                // Verify audience if configured
                if (!string.IsNullOrWhiteSpace(_expectedClientId))
                {
                    bool audMatch = false;
                    foreach (var aud in jwt.Audiences)
                    {
                        if (string.Equals(aud, _expectedClientId, StringComparison.OrdinalIgnoreCase))
                        {
                            audMatch = true;
                            break;
                        }
                    }
                    if (!audMatch) return Task.FromResult<GoogleUserPayload?>(null);
                }

                string email = jwt.Payload.TryGetValue("email", out var eVal) ? eVal?.ToString() ?? "" : "";
                string name = jwt.Payload.TryGetValue("name", out var nVal) ? nVal?.ToString() ?? "" : "";
                string sub = jwt.Subject ?? "";
                bool emailVerified = jwt.Payload.TryGetValue("email_verified", out var evVal) &&
                    (evVal is bool b ? b : bool.TryParse(evVal?.ToString(), out var b2) && b2);

                if (string.IsNullOrWhiteSpace(email)) return Task.FromResult<GoogleUserPayload?>(null);

                return Task.FromResult<GoogleUserPayload?>(new GoogleUserPayload(email.ToLowerInvariant().Trim(), name, sub, emailVerified));
            }
            catch
            {
                return Task.FromResult<GoogleUserPayload?>(null);
            }
        }
    }
}
