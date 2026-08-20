using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EDM.ControlPlane.Api.Services
{
    public interface ICsrfProtectionService
    {
        string GenerateCsrfToken(HttpContext context);
        bool ValidateCsrfToken(HttpContext context, string? clientToken);
    }

    public class CsrfProtectionService : ICsrfProtectionService
    {
        private readonly byte[] _key;

        public CsrfProtectionService(IConfiguration configuration)
        {
            string? secret = configuration["Csrf:SecretKey"] ?? Environment.GetEnvironmentVariable("EDM_CSRF_SECRET");
            if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= 32)
            {
                _key = Encoding.UTF8.GetBytes(secret);
            }
            else
            {
                // Generate a cryptographically secure ephemeral 256-bit key
                _key = new byte[32];
                RandomNumberGenerator.Fill(_key);
            }
        }

        public string GenerateCsrfToken(HttpContext context)
        {
            byte[] nonce = new byte[16];
            RandomNumberGenerator.Fill(nonce);
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string raw = $"{Convert.ToHexString(nonce)}:{timestamp}";
            using var hmac = new HMACSHA256(_key);
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));

            string token = $"{raw}:{Convert.ToHexString(signature).ToLowerInvariant()}";
            return token;
        }

        public bool ValidateCsrfToken(HttpContext context, string? clientToken)
        {
            if (string.IsNullOrWhiteSpace(clientToken)) return false;

            var parts = clientToken.Split(':');
            if (parts.Length != 3) return false;

            string nonceHex = parts[0];
            if (!long.TryParse(parts[1], out long timestamp)) return false;
            string signature = parts[2];

            // Token valid for 2 hours (7200 seconds) with 5-minute clock drift allowance
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - timestamp > 7200 || timestamp > now + 300) return false;

            string raw = $"{nonceHex}:{timestamp}";
            using var hmac = new HMACSHA256(_key);
            byte[] expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            string expectedSigHex = Convert.ToHexString(expectedSig).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()),
                Encoding.UTF8.GetBytes(expectedSigHex));
        }
    }
}
