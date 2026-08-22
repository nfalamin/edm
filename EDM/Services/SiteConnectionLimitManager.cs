using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public class SiteConnectionRule
    {
        public string HostPattern { get; set; } = string.Empty;
        public int MaxConnections { get; set; } = 4;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Enterprise Domain-Specific Max Connection Limits & Anti-Throttling Manager.
    /// Allows users to specify domain and wildcard rules (e.g. *.rapidgator.net -> 1, *.mega.nz -> 2)
    /// to avoid 429 Too Many Requests errors and anti-leech host bans.
    /// </summary>
    public class SiteConnectionLimitManager
    {
        private static readonly Lazy<SiteConnectionLimitManager> _instance = new(() => new SiteConnectionLimitManager());
        public static SiteConnectionLimitManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, SiteConnectionRule> _rules = new(StringComparer.OrdinalIgnoreCase);

        public SiteConnectionLimitManager()
        {
            LoadDefaultRules();
        }

        public void SetRule(string hostPattern, int maxConnections)
        {
            if (string.IsNullOrWhiteSpace(hostPattern)) return;

            maxConnections = Math.Clamp(maxConnections, 1, 32);
            _rules[hostPattern.Trim()] = new SiteConnectionRule
            {
                HostPattern = hostPattern.Trim(),
                MaxConnections = maxConnections,
                IsEnabled = true
            };
        }

        public bool RemoveRule(string hostPattern)
        {
            return _rules.TryRemove(hostPattern, out _);
        }

        public IReadOnlyCollection<SiteConnectionRule> GetRules() => new List<SiteConnectionRule>(_rules.Values);

        public int GetMaxConnectionsForHost(string hostOrUrl, int defaultMax = 8)
        {
            if (string.IsNullOrWhiteSpace(hostOrUrl)) return defaultMax;

            string host = hostOrUrl;
            if (Uri.TryCreate(hostOrUrl, UriKind.Absolute, out var uri))
            {
                host = uri.Host;
            }

            // 1. Exact match
            if (_rules.TryGetValue(host, out var rule) && rule.IsEnabled)
            {
                return rule.MaxConnections;
            }

            // 2. Wildcard match (e.g. *.rapidgator.net)
            foreach (var kvp in _rules)
            {
                if (!kvp.Value.IsEnabled) continue;

                string pattern = kvp.Key;
                if (pattern.StartsWith("*."))
                {
                    string rootDomain = pattern.Substring(2);
                    if (host.EndsWith(rootDomain, StringComparison.OrdinalIgnoreCase) || host.Equals(rootDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value.MaxConnections;
                    }
                }
            }

            return defaultMax;
        }

        private void LoadDefaultRules()
        {
            // Well-known rate-limited hosters default rules
            SetRule("*.rapidgator.net", 1);
            SetRule("*.nitroflare.com", 1);
            SetRule("*.uploaded.net", 1);
            SetRule("*.turbobit.net", 2);
            SetRule("*.1fichier.com", 2);
            SetRule("*.github.com", 16);
            SetRule("*.archive.org", 4);
        }
    }
}
