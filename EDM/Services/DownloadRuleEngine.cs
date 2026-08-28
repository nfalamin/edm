using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public interface IDownloadRuleEngine
    {
        RuleResolutionResult Resolve(DownloadRequest request, string defaultDownloadDir);
        RuleResolutionResult Resolve(string url, string filename, string? contentType, IngestionSource? source, string defaultDownloadDir);
        List<DownloadRule> GetRules();
        void AddOrUpdateRule(DownloadRule rule);
        bool DeleteRule(string ruleId);
        List<DownloadProfile> GetProfiles();
        void AddOrUpdateProfile(DownloadProfile profile);
        bool DeleteProfile(string profileId);
        void SaveRules();
        void LoadRules();
    }

    /// <summary>
    /// Intelligent Download Rule & Profile Engine.
    /// Classifies incoming downloads by extension, MIME type, domain/URL patterns, and source,
    /// safely determining category, destination path, queue, and priority without downloading files.
    /// </summary>
    public class DownloadRuleEngine : IDownloadRuleEngine
    {
        private static readonly Lazy<DownloadRuleEngine> _lazy = new(() => new DownloadRuleEngine());
        public static DownloadRuleEngine Instance => _lazy.Value;

        private readonly List<DownloadRule> _rules = new();
        private readonly List<DownloadProfile> _profiles = new();
        private readonly object _lock = new();
        private readonly string _persistencePath;

        public DownloadRuleEngine(string? storagePath = null)
        {
            string baseDir = !string.IsNullOrWhiteSpace(storagePath)
                ? storagePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM");

            try
            {
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                _persistencePath = Path.Combine(baseDir, "download_rules.json");
            }
            catch
            {
                _persistencePath = Path.Combine(AppContext.BaseDirectory, "download_rules.json");
            }

            InitializeDefaults();
            LoadRules();
        }

        private void InitializeDefaults()
        {
            lock (_lock)
            {
                if (_profiles.Count == 0)
                {
                    _profiles.Add(new DownloadProfile
                    {
                        ProfileId = "default_profile",
                        Name = "Standard Profile",
                        DefaultCategory = "General",
                        DefaultSubFolder = "General",
                        DefaultQueueId = "default",
                        DefaultPriority = DownloadPriority.Normal
                    });
                    _profiles.Add(new DownloadProfile
                    {
                        ProfileId = "media_profile",
                        Name = "Media Stream Profile",
                        DefaultCategory = "Video",
                        DefaultSubFolder = "Videos",
                        DefaultQueueId = "high_priority",
                        DefaultPriority = DownloadPriority.High
                    });
                }

                if (_rules.Count == 0)
                {
                    // Video Rule
                    _rules.Add(new DownloadRule
                    {
                        RuleId = "rule_video",
                        Name = "Video Files",
                        Order = 10,
                        Extensions = new List<string> { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".flv", ".ts", ".m4v", ".m3u8" },
                        MimeTypes = new List<string> { "video/*", "application/vnd.apple.mpegurl", "application/x-mpegurl" },
                        TargetCategory = "Video",
                        TargetSubFolder = "Videos",
                        TargetQueueId = "default",
                        TargetPriority = DownloadPriority.Normal
                    });

                    // Audio Rule
                    _rules.Add(new DownloadRule
                    {
                        RuleId = "rule_audio",
                        Name = "Music & Audio",
                        Order = 20,
                        Extensions = new List<string> { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".opus" },
                        MimeTypes = new List<string> { "audio/*" },
                        TargetCategory = "Music",
                        TargetSubFolder = "Music",
                        TargetQueueId = "default",
                        TargetPriority = DownloadPriority.Normal
                    });

                    // Documents Rule
                    _rules.Add(new DownloadRule
                    {
                        RuleId = "rule_docs",
                        Name = "Documents",
                        Order = 30,
                        Extensions = new List<string> { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".epub", ".csv" },
                        MimeTypes = new List<string> { "application/pdf", "application/msword", "text/*" },
                        TargetCategory = "Documents",
                        TargetSubFolder = "Documents",
                        TargetQueueId = "default",
                        TargetPriority = DownloadPriority.Normal
                    });

                    // Compressed Archives Rule
                    _rules.Add(new DownloadRule
                    {
                        RuleId = "rule_compressed",
                        Name = "Compressed Archives",
                        Order = 40,
                        Extensions = new List<string> { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso" },
                        MimeTypes = new List<string> { "application/zip", "application/x-rar-compressed", "application/x-7z-compressed" },
                        TargetCategory = "Compressed",
                        TargetSubFolder = "Compressed",
                        TargetQueueId = "default",
                        TargetPriority = DownloadPriority.Normal
                    });

                    // Programs Rule
                    _rules.Add(new DownloadRule
                    {
                        RuleId = "rule_programs",
                        Name = "Executable Programs",
                        Order = 50,
                        Extensions = new List<string> { ".exe", ".msi", ".bat", ".cmd", ".apk", ".jar" },
                        MimeTypes = new List<string> { "application/octet-stream", "application/x-msdownload" },
                        TargetCategory = "Programs",
                        TargetSubFolder = "Programs",
                        TargetQueueId = "high_priority",
                        TargetPriority = DownloadPriority.High
                    });
                }
            }
        }

        // ==================== RULE RESOLUTION ====================

        public RuleResolutionResult Resolve(DownloadRequest request, string defaultDownloadDir)
        {
            if (request == null)
            {
                return CreateDefaultResult(defaultDownloadDir, "download.bin");
            }

            var result = Resolve(request.Url, request.SuggestedFileName ?? string.Empty, request.ContentType, request.Source, defaultDownloadDir);

            // Explicit user overrides have highest priority
            if (!string.IsNullOrWhiteSpace(request.TargetCategory))
            {
                result.Category = request.TargetCategory;
            }
            if (!string.IsNullOrWhiteSpace(request.TargetQueueId))
            {
                result.QueueId = request.TargetQueueId;
            }
            if (!string.IsNullOrWhiteSpace(request.TargetDirectory))
            {
                string safeFileName = SecuritySanitizer.SanitizeFileName(request.SuggestedFileName ?? string.Empty);
                string cleanTargetDir = SanitizeDirectory(request.TargetDirectory, defaultDownloadDir);
                result.DestinationPath = Path.Combine(cleanTargetDir, safeFileName);
            }

            return result;
        }

        public RuleResolutionResult Resolve(string url, string filename, string? contentType, IngestionSource? source, string defaultDownloadDir)
        {
            string safeFileName = SecuritySanitizer.SanitizeFileName(filename);
            string cleanExt = Path.GetExtension(safeFileName).ToLowerInvariant();
            string cleanMime = (contentType ?? string.Empty).Trim().ToLowerInvariant();
            string host = string.Empty;

            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    host = uri.Host.ToLowerInvariant();
                }
            }
            catch { }

            lock (_lock)
            {
                var activeRules = _rules.Where(r => r.IsEnabled).OrderBy(r => r.Order).ToList();

                // 1. Check Domain / URL Pattern Match (highest rule precedence)
                if (!string.IsNullOrEmpty(host))
                {
                    foreach (var rule in activeRules)
                    {
                        if ((rule.Domains != null && rule.Domains.Any(d => IsDomainMatch(host, d))) ||
                            (rule.UrlPatterns != null && rule.UrlPatterns.Any(p => IsUrlMatch(url, p))))
                        {
                            return BuildResultFromRule(rule, safeFileName, defaultDownloadDir);
                        }
                    }
                }

                // 2. Check MIME Type Match
                if (!string.IsNullOrEmpty(cleanMime))
                {
                    foreach (var rule in activeRules)
                    {
                        if (rule.MimeTypes != null && rule.MimeTypes.Any(m => IsMimeMatch(cleanMime, m)))
                        {
                            return BuildResultFromRule(rule, safeFileName, defaultDownloadDir);
                        }
                    }
                }

                // 3. Check Extension Match (supports compound extensions like .tar.gz)
                if (!string.IsNullOrEmpty(cleanExt))
                {
                    foreach (var rule in activeRules)
                    {
                        if (rule.Extensions != null && rule.Extensions.Any(e =>
                            string.Equals(NormalizeExtension(e), cleanExt, StringComparison.OrdinalIgnoreCase) ||
                            safeFileName.EndsWith(NormalizeExtension(e), StringComparison.OrdinalIgnoreCase)))
                        {
                            return BuildResultFromRule(rule, safeFileName, defaultDownloadDir);
                        }
                    }
                }

                // 4. Check Source Match
                if (source.HasValue)
                {
                    var sourceRule = activeRules.FirstOrDefault(r => r.MatchingSource == source.Value);
                    if (sourceRule != null)
                    {
                        return BuildResultFromRule(sourceRule, safeFileName, defaultDownloadDir);
                    }
                }
            }

            // 5. Default Fallback
            return CreateDefaultResult(defaultDownloadDir, safeFileName);
        }

        private RuleResolutionResult BuildResultFromRule(DownloadRule rule, string safeFileName, string baseDownloadDir)
        {
            string cleanSubfolder = SanitizeSubfolderName(rule.TargetSubFolder);
            string targetDir = Path.Combine(baseDownloadDir, cleanSubfolder);

            try
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            }
            catch { targetDir = baseDownloadDir; }

            return new RuleResolutionResult
            {
                Category = string.IsNullOrWhiteSpace(rule.TargetCategory) ? "General" : rule.TargetCategory,
                DestinationPath = Path.Combine(targetDir, safeFileName),
                QueueId = string.IsNullOrWhiteSpace(rule.TargetQueueId) ? "default" : rule.TargetQueueId,
                Priority = rule.TargetPriority ?? DownloadPriority.Normal,
                AppliedRuleId = rule.RuleId,
                AppliedProfileId = rule.ProfileId,
                SpeedLimitKbps = rule.SpeedLimitKbps ?? 0,
                AutoStart = rule.AutoStart ?? true
            };
        }

        private RuleResolutionResult CreateDefaultResult(string baseDownloadDir, string safeFileName)
        {
            string subfolder = FileCategorizationService.GetTargetSubfolder(safeFileName);
            string targetDir = Path.Combine(baseDownloadDir, subfolder);

            try
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            }
            catch { targetDir = baseDownloadDir; }

            return new RuleResolutionResult
            {
                Category = subfolder,
                DestinationPath = Path.Combine(targetDir, safeFileName),
                QueueId = "default",
                Priority = DownloadPriority.Normal,
                AppliedRuleId = null,
                AppliedProfileId = "default_profile",
                SpeedLimitKbps = 0,
                AutoStart = true
            };
        }

        // ==================== MATCHING HELPERS ====================

        private static bool IsDomainMatch(string host, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            string cleanPattern = pattern.Trim().ToLowerInvariant().TrimStart('*', '.');
            return host.Equals(cleanPattern, StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith("." + cleanPattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMimeMatch(string actualMime, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            string cleanPat = pattern.Trim().ToLowerInvariant();

            if (cleanPat.EndsWith("/*"))
            {
                string prefix = cleanPat[..^2];
                return actualMime.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return actualMime.Equals(cleanPat, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUrlMatch(string url, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                return Regex.IsMatch(url, regex, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
            }
            catch
            {
                return url.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            string clean = ext.Trim().ToLowerInvariant();
            return clean.StartsWith('.') ? clean : "." + clean;
        }

        private static string SanitizeSubfolderName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "General";
            string nameOnly = Path.GetFileName(raw.Trim());
            var invalid = Path.GetInvalidFileNameChars();
            string cleaned = new string(nameOnly.Where(c => !invalid.Contains(c) && c >= 32).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "General" : cleaned;
        }

        private static string SanitizeDirectory(string targetDir, string fallbackDir)
        {
            if (string.IsNullOrWhiteSpace(targetDir) || targetDir.Contains("..")) return fallbackDir;
            try
            {
                string full = Path.GetFullPath(targetDir.Trim());
                return full;
            }
            catch
            {
                return fallbackDir;
            }
        }

        // ==================== CRUD & PERSISTENCE ====================

        public List<DownloadRule> GetRules()
        {
            lock (_lock)
            {
                return _rules.Select(r => new DownloadRule
                {
                    RuleId = r.RuleId,
                    Name = r.Name,
                    IsEnabled = r.IsEnabled,
                    Order = r.Order,
                    Extensions = r.Extensions?.ToList() ?? new List<string>(),
                    MimeTypes = r.MimeTypes?.ToList() ?? new List<string>(),
                    Domains = r.Domains?.ToList() ?? new List<string>(),
                    UrlPatterns = r.UrlPatterns?.ToList() ?? new List<string>(),
                    MatchingSource = r.MatchingSource,
                    TargetCategory = r.TargetCategory,
                    TargetSubFolder = r.TargetSubFolder,
                    TargetQueueId = r.TargetQueueId,
                    TargetPriority = r.TargetPriority,
                    ProfileId = r.ProfileId,
                    SpeedLimitKbps = r.SpeedLimitKbps,
                    AutoStart = r.AutoStart
                }).ToList();
            }
        }

        public void AddOrUpdateRule(DownloadRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId)) return;

            lock (_lock)
            {
                int idx = _rules.FindIndex(r => string.Equals(r.RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _rules[idx] = rule;
                else _rules.Add(rule);
                SaveRules();
            }
        }

        public bool DeleteRule(string ruleId)
        {
            lock (_lock)
            {
                int removed = _rules.RemoveAll(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    SaveRules();
                    return true;
                }
                return false;
            }
        }

        public List<DownloadProfile> GetProfiles()
        {
            lock (_lock)
            {
                return _profiles.Select(p => new DownloadProfile
                {
                    ProfileId = p.ProfileId,
                    Name = p.Name,
                    DefaultCategory = p.DefaultCategory,
                    DefaultSubFolder = p.DefaultSubFolder,
                    DefaultQueueId = p.DefaultQueueId,
                    DefaultPriority = p.DefaultPriority,
                    SpeedLimitKbps = p.SpeedLimitKbps,
                    AutoStart = p.AutoStart
                }).ToList();
            }
        }

        public void AddOrUpdateProfile(DownloadProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId)) return;

            lock (_lock)
            {
                int idx = _profiles.FindIndex(p => string.Equals(p.ProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _profiles[idx] = profile;
                else _profiles.Add(profile);
                SaveRules();
            }
        }

        public bool DeleteProfile(string profileId)
        {
            lock (_lock)
            {
                int removed = _profiles.RemoveAll(p => string.Equals(p.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    SaveRules();
                    return true;
                }
                return false;
            }
        }

        public void SaveRules()
        {
            try
            {
                var state = new
                {
                    Profiles = _profiles,
                    Rules = _rules
                };
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadRuleEngine] Failed to save download rules", ex);
            }
        }

        public void LoadRules()
        {
            try
            {
                if (!File.Exists(_persistencePath)) return;

                string json = File.ReadAllText(_persistencePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Profiles", out var pProp))
                {
                    var loadedProfiles = JsonSerializer.Deserialize<List<DownloadProfile>>(pProp.GetRawText());
                    if (loadedProfiles != null && loadedProfiles.Count > 0)
                    {
                        lock (_lock)
                        {
                            _profiles.Clear();
                            _profiles.AddRange(loadedProfiles);
                        }
                    }
                }

                if (root.TryGetProperty("Rules", out var rProp))
                {
                    var loadedRules = JsonSerializer.Deserialize<List<DownloadRule>>(rProp.GetRawText());
                    if (loadedRules != null && loadedRules.Count > 0)
                    {
                        lock (_lock)
                        {
                            _rules.Clear();
                            _rules.AddRange(loadedRules);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadRuleEngine] Failed to load download rules", ex);
            }
        }
    }
}
