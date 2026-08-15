using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class PacResolutionResult
    {
        public bool IsDirect { get; set; } = true;
        public string? ProxyHost { get; set; }
        public int ProxyPort { get; set; } = 8080;
        public string ProxyType { get; set; } = "HTTP"; // HTTP, HTTPS, SOCKS
    }

    /// <summary>
    /// Proxy Auto-Configuration (PAC) Script Engine.
    /// Evaluates PAC scripts to determine optimal per-host proxy routing rules (DIRECT vs PROXY).
    /// </summary>
    public class PacProxyService
    {
        private readonly HttpClient _httpClient;
        private string? _pacScriptContent;
        private readonly ConcurrentDictionary<string, PacResolutionResult> _resolutionCache = new(StringComparer.OrdinalIgnoreCase);

        public PacProxyService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public void SetScriptContent(string script)
        {
            _pacScriptContent = script;
            _resolutionCache.Clear();
        }

        public async Task LoadPacFromUrlAsync(string pacUrl, CancellationToken ct = default)
        {
            try
            {
                _pacScriptContent = await _httpClient.GetStringAsync(pacUrl, ct).ConfigureAwait(false);
                _resolutionCache.Clear();
                LoggingService.Log($"[PacProxyService] Successfully loaded PAC script from '{pacUrl}'.");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[PacProxyService] Failed to load PAC script", ex);
                _pacScriptContent = null;
            }
        }

        public PacResolutionResult ResolveProxyForUrl(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl)) return new PacResolutionResult { IsDirect = true };

            if (_resolutionCache.TryGetValue(targetUrl, out var cached))
            {
                return cached;
            }

            var result = new PacResolutionResult { IsDirect = true };

            if (!string.IsNullOrEmpty(_pacScriptContent))
            {
                try
                {
                    var uri = new Uri(targetUrl);
                    string host = uri.DnsSafeHost;

                    // Evaluate PAC rules
                    if (!host.Contains(".") || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsDirect = true;
                    }
                    else
                    {
                        var lines = _pacScriptContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        string? defaultReturnLine = null;
                        bool matched = false;

                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            var matchShExp = Regex.Match(trimmed, @"shExpMatch\s*\(\s*(?:host|url)\s*,\s*""([^""]+)""\s*\)");
                            if (matchShExp.Success)
                            {
                                string glob = matchShExp.Groups[1].Value;
                                string pattern = "^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                                if (Regex.IsMatch(host, pattern, RegexOptions.IgnoreCase) || Regex.IsMatch(targetUrl, pattern, RegexOptions.IgnoreCase))
                                {
                                    matched = true;
                                    ApplyDirective(trimmed, result);
                                    break;
                                }
                            }
                            else if (trimmed.StartsWith("return ", StringComparison.OrdinalIgnoreCase))
                            {
                                defaultReturnLine = trimmed;
                            }
                        }

                        if (!matched && defaultReturnLine != null)
                        {
                            ApplyDirective(defaultReturnLine, result);
                        }
                    }
                }
                catch { }
            }

            _resolutionCache[targetUrl] = result;
            return result;
        }

        private static void ApplyDirective(string directiveText, PacResolutionResult result)
        {
            if (directiveText.Contains("DIRECT", StringComparison.OrdinalIgnoreCase) && !directiveText.Contains("PROXY", StringComparison.OrdinalIgnoreCase) && !directiveText.Contains("SOCKS", StringComparison.OrdinalIgnoreCase))
            {
                result.IsDirect = true;
                return;
            }

            var proxyMatch = Regex.Match(directiveText, @"PROXY\s+([a-zA-Z0-9\.-]+):(\d+)", RegexOptions.IgnoreCase);
            if (proxyMatch.Success)
            {
                result.IsDirect = false;
                result.ProxyHost = proxyMatch.Groups[1].Value;
                result.ProxyPort = int.Parse(proxyMatch.Groups[2].Value);
                result.ProxyType = "HTTP";
                return;
            }

            var socksMatch = Regex.Match(directiveText, @"SOCKS5?\s+([a-zA-Z0-9\.-]+):(\d+)", RegexOptions.IgnoreCase);
            if (socksMatch.Success)
            {
                result.IsDirect = false;
                result.ProxyHost = socksMatch.Groups[1].Value;
                result.ProxyPort = int.Parse(socksMatch.Groups[2].Value);
                result.ProxyType = "SOCKS5";
                return;
            }

            result.IsDirect = true;
        }
    }
}
