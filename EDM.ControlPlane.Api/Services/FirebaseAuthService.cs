using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EDM.ControlPlane.Api.Services
{
    public record FirebaseUserPayload(
        string Uid,
        string Email,
        string? DisplayName,
        string? PhotoUrl,
        bool EmailVerified,
        string? ProviderId);

    public interface IFirebaseAuthService
    {
        Task<FirebaseUserPayload?> ValidateFirebaseTokenAsync(string idToken);
    }

    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string? _expectedProjectId;

        public FirebaseAuthService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _expectedProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID") 
                ?? _configuration["Firebase:ProjectId"] 
                ?? "edm-download-manager";
        }

        public Task<FirebaseUserPayload?> ValidateFirebaseTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return Task.FromResult<FirebaseUserPayload?>(null);
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(idToken))
                {
                    return Task.FromResult<FirebaseUserPayload?>(null);
                }

                var jwt = handler.ReadJwtToken(idToken);

                // Expiry Check
                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    return Task.FromResult<FirebaseUserPayload?>(null);
                }

                // Verify Issuer
                bool isFirebaseIssuer = !string.IsNullOrEmpty(jwt.Issuer) &&
                    (jwt.Issuer.StartsWith("https://securetoken.google.com/", StringComparison.OrdinalIgnoreCase) ||
                     jwt.Issuer.Contains("firebase", StringComparison.OrdinalIgnoreCase) ||
                     jwt.Issuer.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase));

                if (!isFirebaseIssuer)
                {
                    return Task.FromResult<FirebaseUserPayload?>(null);
                }

                // Extract Claims
                string uid = jwt.Subject ?? "";
                if (jwt.Payload.TryGetValue("user_id", out var uidVal) && uidVal != null)
                {
                    uid = uidVal.ToString() ?? uid;
                }

                if (string.IsNullOrWhiteSpace(uid))
                {
                    return Task.FromResult<FirebaseUserPayload?>(null);
                }

                string email = jwt.Payload.TryGetValue("email", out var eVal) ? eVal?.ToString() ?? "" : "";
                string? name = jwt.Payload.TryGetValue("name", out var nVal) ? nVal?.ToString() : null;
                string? picture = jwt.Payload.TryGetValue("picture", out var pVal) ? pVal?.ToString() : null;
                
                bool emailVerified = jwt.Payload.TryGetValue("email_verified", out var evVal) &&
                    (evVal is bool b ? b : bool.TryParse(evVal?.ToString(), out var b2) && b2);

                string? providerId = "firebase";
                if (jwt.Payload.TryGetValue("firebase", out var fbObj) && fbObj is JsonElement fbJson)
                {
                    if (fbJson.TryGetProperty("sign_in_provider", out var sip))
                    {
                        providerId = sip.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    email = $"{uid}@firebase.edm.local";
                }

                var payload = new FirebaseUserPayload(
                    Uid: uid,
                    Email: email.ToLowerInvariant().Trim(),
                    DisplayName: name,
                    PhotoUrl: picture,
                    EmailVerified: emailVerified,
                    ProviderId: providerId);

                return Task.FromResult<FirebaseUserPayload?>(payload);
            }
            catch
            {
                return Task.FromResult<FirebaseUserPayload?>(null);
            }
        }
    }
}
