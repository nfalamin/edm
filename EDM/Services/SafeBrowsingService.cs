using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Checks URLs against Google Safe Browsing API for malware, phishing, and unsafe content.
    /// Uses free tier API v4 with user-supplied API key.
    /// API documentation: https://developers.google.com/safe-browsing/v4
    /// </summary>
    public class SafeBrowsingService
    {
        private readonly ISettingsService _settingsService;
        private const string API_ENDPOINT = "https://safebrowsing.googleapis.com/v4/threatMatches:find";
        private const int REQUEST_TIMEOUT_SECONDS = 5;

        /// <summary>
        /// Levels of threat detected by Google Safe Browsing API.
        /// </summary>
        public enum ThreatLevel
        {
            Unknown = 0,      // No threat data or API not available
            Safe = 1,         // URL is safe
            Malicious = 2,    // URL is malicious (phishing, malware, etc.)
        }

        /// <summary>
        /// Result of a safety check.
        /// </summary>
        public class SafetyCheckResult
        {
            public ThreatLevel Level { get; set; } = ThreatLevel.Unknown;
            public string ThreatType { get; set; } = string.Empty;  // e.g., "MALWARE", "PHISHING"
            public string Message { get; set; } = string.Empty;
            public bool IsEnabled { get; set; } = false;  // Whether safety check feature is enabled
        }

        public SafeBrowsingService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Checks if a URL is safe using Google Safe Browsing API.
        /// Returns Unknown if API key is not configured or if the check fails.
        /// Non-blocking and graceful - errors don't throw.
        /// </summary>
        public async Task<SafetyCheckResult> CheckUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            // Check if feature is enabled
            var isEnabled = _settingsService.GetEnableUrlSafetyCheck();
            var apiKey = _settingsService.GetGoogleSafeBrowsingApiKey();

            if (!isEnabled || string.IsNullOrWhiteSpace(apiKey))
            {
                return new SafetyCheckResult { Level = ThreatLevel.Unknown, IsEnabled = false };
            }

            try
            {
                LoggingService.Log($"[SafeBrowsingService] Checking URL: {url}");

                using (var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true })
                {
                    Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS)
                })
                {
                    // Build Google Safe Browsing API v4 request
                    var requestBody = new
                    {
                        client = new
                        {
                            clientId = "edm-downloader",
                            clientVersion = "1.0"
                        },
                        threatInfo = new
                        {
                            threatTypes = new[] { "MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE", "POTENTIALLY_HARMFUL_APPLICATION" },
                            platformTypes = new[] { "WINDOWS", "ANY_PLATFORM" },
                            threatEntryTypes = new[] { "URL" },
                            threatEntries = new[] { new { url = url } }
                        }
                    };

                    var jsonContent = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                    // Call Google Safe Browsing API
                    var requestUri = $"{API_ENDPOINT}?key={Uri.EscapeDataString(apiKey)}";
                    using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
                    {
                        request.Content = content;

                        using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                LoggingService.Log($"[SafeBrowsingService] API returned status {response.StatusCode}");
                                return new SafetyCheckResult { Level = ThreatLevel.Unknown, IsEnabled = true };
                            }

                            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            // Parse response
                            if (!string.IsNullOrEmpty(responseJson))
                            {
                                using (var doc = JsonDocument.Parse(responseJson))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("matches", out var matches) && matches.GetArrayLength() > 0)
                                    {
                                        // URL is malicious
                                        var threatType = "UNKNOWN";
                                        if (matches[0].TryGetProperty("threatType", out var threatTypeElement))
                                        {
                                            threatType = threatTypeElement.GetString() ?? "UNKNOWN";
                                        }

                                        LoggingService.Log($"[SafeBrowsingService] URL is MALICIOUS - Threat: {threatType}");
                                        return new SafetyCheckResult
                                        {
                                            Level = ThreatLevel.Malicious,
                                            ThreatType = threatType,
                                            Message = $"Google Safe Browsing detected this link as {threatType}",
                                            IsEnabled = true
                                        };
                                    }
                                }
                            }

                            // URL is safe (no matches in response)
                            LoggingService.Log($"[SafeBrowsingService] URL is SAFE");
                            return new SafetyCheckResult
                            {
                                Level = ThreatLevel.Safe,
                                Message = "Google Safe Browsing verified this URL as safe",
                                IsEnabled = true
                            };
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning("[SafeBrowsingService] Safety check was cancelled or timed out");
                return new SafetyCheckResult { Level = ThreatLevel.Unknown, IsEnabled = true };
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SafeBrowsingService.CheckUrlAsync]", ex);
                // Graceful failure - don't break download flow
                return new SafetyCheckResult { Level = ThreatLevel.Unknown, IsEnabled = true };
            }
        }

        /// <summary>
        /// Gets a visual representation of the threat level.
        /// </summary>
        public static string GetThreatLevelIcon(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.Safe => "🟢",
                ThreatLevel.Malicious => "🔴",
                _ => "⭕"  // Unknown/disabled
            };
        }

        /// <summary>
        /// Result of a post-download file scan.
        /// </summary>
        public class FileScanResult
        {
            public bool IsThreat { get; set; } = false;
            public string ThreatName { get; set; } = string.Empty;
            public string QuarantinePath { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool Executed { get; set; } = false;
        }

        /// <summary>
        /// Scans a completed file using Windows Defender CLI (MpCmdRun.exe).
        /// Quarantines file if a threat is detected. Skips gracefully if Defender CLI is missing.
        /// </summary>
        public async Task<FileScanResult> ScanDownloadedFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.GetEnablePostDownloadScan())
            {
                return new FileScanResult { IsThreat = false, Message = "Post-download scan is disabled", Executed = false };
            }

            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                return new FileScanResult { IsThreat = false, Message = "File does not exist", Executed = false };
            }

            string? mpCmdRunPath = FindMpCmdRunExecutable();
            if (string.IsNullOrEmpty(mpCmdRunPath) || !System.IO.File.Exists(mpCmdRunPath))
            {
                LoggingService.Log("[SafeBrowsingService] Windows Defender MpCmdRun.exe not found; skipping file scan gracefully.");
                return new FileScanResult { IsThreat = false, Message = "Defender CLI not found, skipped gracefully", Executed = false };
            }

            try
            {
                LoggingService.Log($"[SafeBrowsingService] Initiating Windows Defender scan for file: {filePath}");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mpCmdRunPath,
                    Arguments = $"-Scan -ScanType 3 -File \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = psi };
                process.Start();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                int exitCode = process.ExitCode;

                if (exitCode == 2)
                {
                    LoggingService.LogWarning($"[SafeBrowsingService] THREAT DETECTED in file: {filePath} by Windows Defender!");

                    string quarantineDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EDM", "Quarantine");
                    System.IO.Directory.CreateDirectory(quarantineDir);
                    string fileName = System.IO.Path.GetFileName(filePath);
                    string quarantinePath = System.IO.Path.Combine(quarantineDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}");

                    try
                    {
                        System.IO.File.Move(filePath, quarantinePath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[SafeBrowsingService] Failed to quarantine threat file", ex);
                    }

                    return new FileScanResult
                    {
                        IsThreat = true,
                        ThreatName = "Windows Defender Threat Detected",
                        QuarantinePath = quarantinePath,
                        Message = $"Threat detected in download! File moved to quarantine: {quarantinePath}",
                        Executed = true
                    };
                }

                LoggingService.Log($"[SafeBrowsingService] File scan completed cleanly. (Exit code: {exitCode})");
                return new FileScanResult { IsThreat = false, Message = "File scanned clean", Executed = true };
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SafeBrowsingService.ScanDownloadedFileAsync]", ex);
                return new FileScanResult { IsThreat = false, Message = $"Scan error: {ex.Message}", Executed = false };
            }
        }

        private static string? FindMpCmdRunExecutable()
        {
            var candidates = new[]
            {
                @"C:\Program Files\Windows Defender\MpCmdRun.exe",
                @"C:\Program Files (x86)\Windows Defender\MpCmdRun.exe"
            };

            foreach (var path in candidates)
            {
                if (System.IO.File.Exists(path)) return path;
            }

            try
            {
                string platformDir = @"C:\ProgramData\Microsoft\Windows Defender\Platform";
                if (System.IO.Directory.Exists(platformDir))
                {
                    var exe = System.IO.Directory.GetFiles(platformDir, "MpCmdRun.exe", System.IO.SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(exe) && System.IO.File.Exists(exe)) return exe;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Gets a user-friendly description of the threat level.
        /// </summary>
        public static string GetThreatLevelDescription(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.Safe => "Safe",
                ThreatLevel.Malicious => "Malicious/Phishing Warning",
                _ => "Unchecked"
            };
        }
    }
}
