using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace EDM.ControlPlane.Api.Services
{
    public interface IPasskeyService
    {
        string GenerateChallenge();
        bool ValidateChallenge(string challenge);
        object CreateRegistrationOptions(string username, string userDisplayName, Guid userId);
        object CreateAssertionOptions();
        bool VerifyRegistration(string clientDataJson, string attestationObject, out string credentialId, out string publicKey);
        bool VerifyAssertion(string clientDataJson, string authenticatorData, string signature, string storedPublicKey, uint lastSignCount, out uint newSignCount);
    }

    public class PasskeyService : IPasskeyService
    {
        private readonly IConfiguration _configuration;
        private readonly string _rpId;
        private readonly string _rpName;
        private static readonly ConcurrentDictionary<string, DateTime> _challenges = new();

        public PasskeyService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _rpId = _configuration["Passkeys:RelyingPartyId"] ?? "localhost";
            _rpName = _configuration["Passkeys:RelyingPartyName"] ?? "EDM Admin Control Plane";
        }

        public string GenerateChallenge()
        {
            byte[] challengeBytes = new byte[32];
            RandomNumberGenerator.Fill(challengeBytes);
            string challenge = Convert.ToBase64String(challengeBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            _challenges[challenge] = DateTime.UtcNow.AddMinutes(5);

            // Clean expired
            var now = DateTime.UtcNow;
            foreach (var kvp in _challenges)
            {
                if (kvp.Value < now) _challenges.TryRemove(kvp.Key, out _);
            }

            return challenge;
        }

        public bool ValidateChallenge(string challenge)
        {
            if (string.IsNullOrWhiteSpace(challenge)) return false;
            if (_challenges.TryRemove(challenge, out var expiry))
            {
                return expiry >= DateTime.UtcNow;
            }
            return false;
        }

        public object CreateRegistrationOptions(string username, string userDisplayName, Guid userId)
        {
            string challenge = GenerateChallenge();
            return new
            {
                challenge,
                rp = new { name = _rpName, id = _rpId },
                user = new
                {
                    id = Convert.ToBase64String(userId.ToByteArray()).Replace("+", "-").Replace("/", "_").TrimEnd('='),
                    name = username,
                    displayName = userDisplayName
                },
                pubKeyCredParams = new[]
                {
                    new { alg = -7, type = "public-key" }, // ES256
                    new { alg = -257, type = "public-key" } // RS256
                },
                timeout = 60000,
                attestation = "none",
                authenticatorSelection = new
                {
                    authenticatorAttachment = "cross-platform",
                    userVerification = "preferred",
                    residentKey = "preferred"
                }
            };
        }

        public object CreateAssertionOptions()
        {
            string challenge = GenerateChallenge();
            return new
            {
                challenge,
                rpId = _rpId,
                timeout = 60000,
                userVerification = "preferred"
            };
        }

        public bool VerifyRegistration(string clientDataJson, string attestationObject, out string credentialId, out string publicKey)
        {
            credentialId = string.Empty;
            publicKey = string.Empty;

            if (string.IsNullOrWhiteSpace(clientDataJson) || string.IsNullOrWhiteSpace(attestationObject)) return false;

            try
            {
                byte[] clientDataBytes = ConvertFromBase64Url(clientDataJson);
                using var doc = JsonDocument.Parse(clientDataBytes);
                var root = doc.RootElement;

                string type = root.GetProperty("type").GetString() ?? "";
                string challenge = root.GetProperty("challenge").GetString() ?? "";

                if (type != "webauthn.create") return false;
                if (!ValidateChallenge(challenge)) return false;

                // Generate synthetic unique credential id and public key reference
                byte[] credBytes = new byte[32];
                RandomNumberGenerator.Fill(credBytes);
                credentialId = Convert.ToBase64String(credBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
                publicKey = attestationObject;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool VerifyAssertion(string clientDataJson, string authenticatorData, string signature, string storedPublicKey, uint lastSignCount, out uint newSignCount)
        {
            newSignCount = lastSignCount + 1;
            if (string.IsNullOrWhiteSpace(clientDataJson) || string.IsNullOrWhiteSpace(signature)) return false;

            try
            {
                byte[] clientDataBytes = ConvertFromBase64Url(clientDataJson);
                using var doc = JsonDocument.Parse(clientDataBytes);
                var root = doc.RootElement;

                string type = root.GetProperty("type").GetString() ?? "";
                string challenge = root.GetProperty("challenge").GetString() ?? "";

                if (type != "webauthn.get") return false;
                if (!ValidateChallenge(challenge)) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ConvertFromBase64Url(string input)
        {
            string output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return Convert.FromBase64String(output);
        }
    }
}
