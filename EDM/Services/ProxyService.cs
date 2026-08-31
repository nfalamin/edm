using System;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using EDM.Models;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    /// <summary>
    /// Builds .NET <see cref="IWebProxy"/> instances from user-configured <see cref="ProxySettings"/>,
    /// and encrypts/decrypts the stored proxy password using Windows DPAPI so it is never
    /// persisted in plain text inside settings.json.
    /// </summary>
    public static class ProxyService
    {
        // DPAPI entropy scopes the encrypted blob to this application specifically.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EDM.ProxyService.v1");

        [SupportedOSPlatform("windows")]
        public static string EncryptPassword(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedBytes);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ProxyService.EncryptPassword] Failed to encrypt: {ex.Message}");
                return string.Empty;
            }
        }

        [SupportedOSPlatform("windows")]
        public static string DecryptPassword(string? encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;
            try
            {
                var protectedBytes = Convert.FromBase64String(encryptedBase64);
                var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ProxyService.DecryptPassword] Failed to decrypt (settings may have been copied from another machine/user): {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds an <see cref="IWebProxy"/> ready to assign to an HttpClientHandler/SocketsHttpHandler.
        /// Returns null when proxying is disabled or misconfigured (falls back to direct connection).
        /// </summary>
        public static IWebProxy? BuildWebProxy(ProxySettings? settings)
        {
            if (settings == null || !settings.Enabled) return null;
            if (string.IsNullOrWhiteSpace(settings.Host) || settings.Port <= 0)
            {
                LoggingService.Log("[ProxyService.BuildWebProxy] Proxy enabled but host/port invalid; falling back to direct connection.");
                return null;
            }

            try
            {
                // SOCKS5 and HTTP/HTTPS proxies both use the "scheme://host:port" URI form
                // supported natively by System.Net.WebProxy / SocketsHttpHandler since .NET 6.
                string scheme = settings.Type switch
                {
                    ProxyType.Socks5 => "socks5",
                    ProxyType.Https => "https",
                    _ => "http"
                };

                var proxyUri = new Uri($"{scheme}://{settings.Host}:{settings.Port}");
                var webProxy = new WebProxy(proxyUri);

                if (settings.HasCredentials)
                {
                    var password = DecryptPassword(settings.EncryptedPassword);
                    webProxy.Credentials = new NetworkCredential(settings.Username, password);
                }

                if (settings.BypassLocalAddresses)
                {
                    webProxy.BypassProxyOnLocal = true;
                }

                if (!string.IsNullOrWhiteSpace(settings.BypassList))
                {
                    var patterns = settings.BypassList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var validPatterns = new List<string>();
                    foreach (var p in patterns)
                    {
                        string safePattern = p.StartsWith("^") ? p : "^" + Regex.Escape(p).Replace(@"\*", ".*") + "$";
                        validPatterns.Add(safePattern);
                    }
                    if (validPatterns.Count > 0)
                    {
                        try
                        {
                            webProxy.BypassList = validPatterns.ToArray();
                        }
                        catch { }
                    }
                }

                return webProxy;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ProxyService.BuildWebProxy] Failed to build proxy from settings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Quick connectivity test: tries to reach a well-known URL through the given proxy settings.
        /// Used by the "Test Connection" button in Settings so users get instant feedback.
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool Success, string Message)> TestProxyAsync(ProxySettings settings, System.Threading.CancellationToken ct = default)
        {
            try
            {
                var proxy = BuildWebProxy(settings);
                if (proxy == null)
                {
                    return (false, "প্রক্সি কনফিগারেশন অসম্পূর্ণ বা ডিসেবল করা আছে。");
                }

                using var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
                using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                using var resp = await client.GetAsync("https://www.gstatic.com/generate_204", ct).ConfigureAwait(false);
                return (true, $"প্রক্সি সংযোগ সফল হয়েছে (HTTP {(int)resp.StatusCode})।");
            }
            catch (Exception ex)
            {
                return (false, $"প্রক্সি সংযোগ ব্যর্থ হয়েছে: {ex.Message}");
            }
        }
    }
}
