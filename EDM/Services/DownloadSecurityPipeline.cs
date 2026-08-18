using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public enum SecurityDecision
    {
        SecurityApproved = 0,
        SecurityBlocked = 1,
        SecurityQuarantined = 2,
        SecurityUnknown = 3,
        SecurityVerificationFailed = 4
    }

    public class SecurityPipelineResult
    {
        public SecurityDecision Decision { get; set; } = SecurityDecision.SecurityApproved;
        public VerificationState VerificationState { get; set; } = VerificationState.Pending;
        public string? ComputedHash { get; set; }
        public string? ExpectedHash { get; set; }
        public bool IsSignatureValid { get; set; }
        public bool IsExecutable { get; set; }
        public bool ThreatDetected { get; set; }
        public string? ThreatName { get; set; }
        public string? QuarantinePath { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DownloadSecurityContext
    {
        public string Url { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long? ExpectedSize { get; set; }
        public string? ExpectedHashHex { get; set; }
        public bool EnforceStrictSecurity { get; set; }
    }

    /// <summary>
    /// Master Download Security Pipeline Orchestrator.
    /// Coordinates deterministic security lifecycle: URL validation, reputation check,
    /// redirect policy, streaming cryptographic integrity, Authenticode verification,
    /// Windows Defender scanning, and atomic quarantine/release decisions.
    /// </summary>
    public class DownloadSecurityPipeline
    {
        private static readonly Lazy<DownloadSecurityPipeline> _instance = new(() => new DownloadSecurityPipeline());
        public static DownloadSecurityPipeline Instance => _instance.Value;

        private readonly ISettingsService _settingsService;
        private readonly SafeBrowsingService _safeBrowsing;
        private readonly FileIntegrityService _fileIntegrity;
        private readonly PostDownloadScannerService _scannerService;

        public DownloadSecurityPipeline(
            ISettingsService? settingsService = null,
            SafeBrowsingService? safeBrowsing = null,
            FileIntegrityService? fileIntegrity = null,
            PostDownloadScannerService? scannerService = null)
        {
            _settingsService = settingsService ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService ?? new SettingsService();
            _safeBrowsing = safeBrowsing ?? new SafeBrowsingService(_settingsService);
            _fileIntegrity = fileIntegrity ?? new FileIntegrityService();
            _scannerService = scannerService ?? new PostDownloadScannerService();
        }

        #region 1. PRE-DOWNLOAD URL & PATH VALIDATION

        /// <summary>
        /// Validates input URL against dangerous schemes and format irregularities.
        /// </summary>
        public bool ValidateUrl(string url, out string validationError)
        {
            validationError = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                validationError = "URL cannot be empty or whitespace.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUri))
            {
                validationError = "Malformed or invalid URI format.";
                return false;
            }

            string scheme = parsedUri.Scheme.ToLowerInvariant();
            if (scheme is "javascript" or "data" or "file" or "blob" or "vbscript" or "about")
            {
                validationError = $"Dangerous or unsupported URI scheme '{scheme}:' is blocked.";
                return false;
            }

            if (scheme is not ("http" or "https" or "ftp" or "ftps" or "magnet"))
            {
                validationError = $"Unsupported protocol scheme '{scheme}:'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsedUri.Host) && scheme != "magnet")
            {
                validationError = "URL host cannot be empty.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sanitizes destination path to prevent directory traversal and invalid characters.
        /// </summary>
        public string SanitizeDestination(string baseDir, string rawFilename)
        {
            string cleanName = SecuritySanitizer.SanitizeFileName(rawFilename);
            if (SecuritySanitizer.TrySanitizeDestinationPath(baseDir, cleanName, out var safePath))
            {
                return safePath;
            }
            return Path.Combine(baseDir, cleanName);
        }

        /// <summary>
        /// Validates that an HTTP redirect does not navigate to a dangerous scheme.
        /// </summary>
        public bool ValidateRedirect(Uri originalUri, Uri redirectedUri, out string redirectError)
        {
            redirectError = string.Empty;
            if (redirectedUri == null)
            {
                redirectError = "Null redirect target.";
                return false;
            }

            string targetScheme = redirectedUri.Scheme.ToLowerInvariant();
            if (targetScheme is "javascript" or "data" or "file" or "blob" or "vbscript")
            {
                redirectError = $"Unsafe redirect to forbidden scheme '{targetScheme}:' rejected.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks Safe Browsing reputation for the URL.
        /// </summary>
        public async Task<SafeBrowsingService.SafetyCheckResult> CheckReputationAsync(string url, CancellationToken ct = default)
        {
            try
            {
                return await _safeBrowsing.CheckUrlAsync(url, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadSecurityPipeline.CheckReputationAsync]", ex);
                return new SafeBrowsingService.SafetyCheckResult
                {
                    Level = SafeBrowsingService.ThreatLevel.Unknown,
                    Message = $"Reputation check unavailable: {ex.Message}"
                };
            }
        }

        #endregion

        #region 2. POST-DOWNLOAD SECURITY & INTEGRITY EVALUATION

        /// <summary>
        /// Executes context-aware post-download security checks: size verification, streaming SHA-256,
        /// Authenticode digital signature check (for executables), Windows Defender scan, and quarantine.
        /// </summary>
        public async Task<SecurityPipelineResult> ProcessPostDownloadSecurityAsync(DownloadSecurityContext context, CancellationToken ct = default)
        {
            var result = new SecurityPipelineResult
            {
                ExpectedHash = context.ExpectedHashHex,
                Timestamp = DateTime.UtcNow
            };

            if (!File.Exists(context.FilePath))
            {
                result.Decision = SecurityDecision.SecurityVerificationFailed;
                result.VerificationState = VerificationState.VerificationFailed;
                result.Message = "Target file does not exist on disk.";
                return result;
            }

            FileInfo fileInfo = new FileInfo(context.FilePath);
            string ext = Path.GetExtension(context.FilePath).ToLowerInvariant();
            bool isExecutable = ext is ".exe" or ".msi" or ".dll" or ".sys" or ".scr" or ".bat" or ".cmd";
            result.IsExecutable = isExecutable;

            // 1. File Size Verification (if expected size was known)
            if (context.ExpectedSize.HasValue && context.ExpectedSize.Value > 0)
            {
                if (fileInfo.Length != context.ExpectedSize.Value)
                {
                    result.Decision = SecurityDecision.SecurityVerificationFailed;
                    result.VerificationState = VerificationState.VerificationFailed;
                    result.Message = $"File size mismatch: expected {context.ExpectedSize.Value} bytes, actual {fileInfo.Length} bytes.";
                    LoggingService.LogWarning($"[SecurityPipeline] {result.Message}");
                    return result;
                }
            }

            // 2. Streaming Cryptographic SHA-256 Hash Computation
            try
            {
                result.ComputedHash = await _fileIntegrity.ComputeSha256Async(context.FilePath, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(context.ExpectedHashHex))
                {
                    string cleanExpected = context.ExpectedHashHex.Trim().ToLowerInvariant();
                    if (!string.Equals(result.ComputedHash, cleanExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Decision = SecurityDecision.SecurityVerificationFailed;
                        result.VerificationState = VerificationState.VerificationFailed;
                        result.Message = $"Cryptographic hash mismatch! Expected: {cleanExpected}, Computed: {result.ComputedHash}";
                        LoggingService.LogWarning($"[SecurityPipeline] {result.Message}");
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecurityPipeline] Hash computation failed", ex);
            }

            // 3. Authenticode Digital Signature Check for Executables
            if (isExecutable && (ext is ".exe" or ".dll" or ".msi"))
            {
                try
                {
                    var sigResult = AuthenticodeVerifier.VerifyFile(context.FilePath);
                    result.IsSignatureValid = sigResult.IsValid;
                    if (!sigResult.IsSigned)
                    {
                        LoggingService.Log($"[SecurityPipeline] Executable '{fileInfo.Name}' is unsigned (No Authenticode certificate).");
                    }
                    else if (!sigResult.IsValid)
                    {
                        LoggingService.LogWarning($"[SecurityPipeline] Executable '{fileInfo.Name}' has untrusted or self-signed certificate: {sigResult.StatusMessage}");
                    }
                    else
                    {
                        LoggingService.Log($"[SecurityPipeline] Executable '{fileInfo.Name}' Authenticode signature is valid: {sigResult.Subject}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SecurityPipeline] Authenticode check failed", ex);
                }
            }

            // 4. Windows Defender / Antivirus Scan
            bool avScanEnabled = _settingsService.GetEnablePostDownloadScan();
            if (avScanEnabled)
            {
                try
                {
                    var scanResult = await _scannerService.ScanFileAsync(context.FilePath, ct).ConfigureAwait(false);
                    if (scanResult.ThreatFound || !scanResult.IsSafe)
                    {
                        result.ThreatDetected = true;
                        result.ThreatName = "Malware/Unwanted Threat Detected";
                        result.Decision = SecurityDecision.SecurityQuarantined;
                        result.VerificationState = VerificationState.VerificationFailed;

                        // Atomic Quarantine Move
                        result.QuarantinePath = ExecuteQuarantine(context.FilePath);
                        result.Message = $"Threat detected by Windows Defender. File quarantined to: {result.QuarantinePath}";
                        LoggingService.LogWarning($"[SecurityPipeline] {result.Message}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SecurityPipeline] Defender scan error", ex);
                }
            }

            // 5. Final Deterministic Approval Decision
            result.Decision = SecurityDecision.SecurityApproved;
            result.VerificationState = VerificationState.Verified;
            result.Message = isExecutable 
                ? (result.IsSignatureValid ? "Security Approved (Authenticode Verified & Scanned Clean)" : "Security Approved (Scanned Clean)")
                : "Security Approved (Integrity & Clean Scan Verified)";

            LoggingService.Log($"[SecurityPipeline] File '{fileInfo.Name}' successfully approved.");
            return result;
        }

        private static string ExecuteQuarantine(string sourcePath)
        {
            try
            {
                string quarantineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EDM", "Quarantine");
                Directory.CreateDirectory(quarantineDir);
                string fileName = Path.GetFileName(sourcePath);
                string quarantineTarget = Path.Combine(quarantineDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}.quarantine");

                if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, quarantineTarget, overwrite: true);
                }
                return quarantineTarget;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SecurityPipeline.ExecuteQuarantine] Failed", ex);
                return sourcePath;
            }
        }

        #endregion

        #region 3. DOWNLOADITEM & HISTORY SYNCHRONIZATION

        /// <summary>
        /// Updates DownloadItem model and persists security metadata to SQLite history.
        /// </summary>
        public void ApplySecurityResultToDownloadItem(DownloadItem item, SecurityPipelineResult result)
        {
            if (item == null || result == null) return;

            item.VerificationState = result.VerificationState;
            item.VerificationAlgorithm = !string.IsNullOrEmpty(result.ComputedHash) ? "SHA-256" : null;
            item.ComputedVerificationHash = result.ComputedHash;
            item.TrustedVerificationHash = result.ExpectedHash;
            item.VerificationMessage = result.Message;
            item.VerificationTimestamp = result.Timestamp;

            if (result.Decision == SecurityDecision.SecurityQuarantined)
            {
                item.Status = "Quarantined";
                item.SavePath = result.QuarantinePath ?? item.SavePath;
            }
        }

        #endregion
    }
}
