using System;
using System.Security.Cryptography;
using System.Text;

namespace EDM.Services
{
    /// <summary>
    /// Windows DPAPI-protected zero-trust credential vault.
    /// Encrypts and decrypts sensitive passwords, authorization tokens, and proxy credentials
    /// bound exclusively to the current Windows user profile.
    /// </summary>
    public static class SecureCredentialVault
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EDM-ZeroTrust-Entropy-2026");

        public static string EncryptSecret(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecureCredentialVault] Failed to encrypt secret with DPAPI", ex);
                return string.Empty;
            }
        }

        public static string DecryptSecret(string ciphertextBase64)
        {
            if (string.IsNullOrEmpty(ciphertextBase64)) return string.Empty;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(ciphertextBase64);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecureCredentialVault] Failed to decrypt secret with DPAPI", ex);
                return string.Empty;
            }
        }

        public static string RedactCredentialsFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Redact Basic Auth
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"Basic\s+[A-Za-z0-9+/=]+",
                "Basic [REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact Bearer Tokens
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"Bearer\s+[A-Za-z0-9_\-\.]+",
                "Bearer [REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact password= query params
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"(password|pwd|secret|token)=[^&\s]+",
                "$1=[REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return text;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string user, string encryptedPass)> _savedCredentials = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string VaultFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EDM", "vault.dat");

        static SecureCredentialVault()
        {
            LoadVaultFromDisk();
        }

        private static void LoadVaultFromDisk()
        {
            try
            {
                if (System.IO.File.Exists(VaultFilePath))
                {
                    string json = System.IO.File.ReadAllText(VaultFilePath);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, (string user, string encryptedPass)>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _savedCredentials[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecureCredentialVault] Failed to load credentials from disk", ex);
            }
        }

        private static void SaveVaultToDisk()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(VaultFilePath)!;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                string json = System.Text.Json.JsonSerializer.Serialize(_savedCredentials);
                System.IO.File.WriteAllText(VaultFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecureCredentialVault] Failed to save credentials to disk", ex);
            }
        }

        public static void SaveCredentials(string host, string username, string password)
        {
            if (string.IsNullOrEmpty(host)) return;
            string cipher = EncryptSecret(password);
            _savedCredentials[host] = (username, cipher);
            SaveVaultToDisk();
        }

        public static bool TryGetCredentials(string host, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;
            if (string.IsNullOrEmpty(host)) return false;

            if (_savedCredentials.TryGetValue(host, out var entry))
            {
                username = entry.user;
                password = DecryptSecret(entry.encryptedPass);
                return true;
            }
            return false;
        }

        public static void DeleteCredentials(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            _savedCredentials.TryRemove(host, out _);
            SaveVaultToDisk();
        }

        public static IReadOnlyList<(string Host, string Username)> GetAllCredentials()
        {
            return _savedCredentials.Select(kvp => (kvp.Key, kvp.Value.user)).ToList();
        }
    }
}
