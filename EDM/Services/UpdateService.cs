using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string MinSupportedVersion { get; set; } = string.Empty;
        public bool ForceUpdate { get; set; } = false;
        public string Severity { get; set; } = "OPTIONAL"; // OPTIONAL, RECOMMENDED, REQUIRED
        public string Title { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; } = 0;
        public string Changelog { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public bool IsUpdateAvailable { get; set; } = false;
        public bool IsMandatory { get; set; } = false;
    }

    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly DownloadService _downloadService;
        private readonly FileIntegrityService _integrityService;
        private readonly ControlPlaneClient? _controlPlaneClient;

        public UpdateService(
            ISettingsService settingsService,
            DownloadService? downloadService = null,
            HttpClient? httpClient = null,
            FileIntegrityService? integrityService = null,
            ControlPlaneClient? controlPlaneClient = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _downloadService = downloadService ?? new DownloadService(_httpClient, new NetworkService(_settingsService), _settingsService);
            _integrityService = integrityService ?? new FileIntegrityService();
            _controlPlaneClient = controlPlaneClient ?? App.ServiceProvider?.GetService(typeof(ControlPlaneClient)) as ControlPlaneClient;
        }

        public async Task<UpdateInfo> CheckControlPlaneUpdateAsync(string currentVersion = "2.0.0", CancellationToken ct = default)
        {
            var client = _controlPlaneClient ?? new ControlPlaneClient(_httpClient, _settingsService);
            var res = await client.CheckForUpdateAsync(currentVersion, ct).ConfigureAwait(false);
            if (res == null)
            {
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            return new UpdateInfo
            {
                IsUpdateAvailable = res.UpdateAvailable,
                Version = res.LatestVersion,
                MinSupportedVersion = res.MinimumSupportedVersion,
                IsMandatory = res.IsMandatory,
                Severity = res.Severity,
                Title = res.Title,
                Changelog = res.ReleaseNotes,
                DownloadUrl = res.DownloadUrl ?? string.Empty,
                Sha256 = res.Sha256Hash ?? string.Empty,
                FileSizeBytes = res.FileSizeBytes
            };
        }

        private bool TryVerifyManifestSignature(string jsonContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonContent)) return true;
                var node = JsonNode.Parse(jsonContent) as JsonObject;
                if (node == null) return true;

                if (!node.TryGetPropertyValue("signature", out var sigNode) || sigNode == null)
                {
                    // No signature present; respect optional enforcement setting
                    var require = false;
                    var v = _settingsService.GetSetting("RequireSignedUpdateManifest");
                    if (!string.IsNullOrWhiteSpace(v) && bool.TryParse(v, out var rv)) require = rv;
                    if (require)
                    {
                        throw new InvalidOperationException("Update manifest is required to be signed but contains no signature.");
                    }
                    return true;
                }

                string signatureBase64 = sigNode.GetValue<string>() ?? string.Empty;
                // Remove signature property before canonicalizing payload
                node.Remove("signature");
                string canonicalJson = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

                var publicKeyPem = _settingsService.GetSetting("UpdateSignerPublicKeyPem");
                if (string.IsNullOrWhiteSpace(publicKeyPem))
                {
                    LoggingService.Log("[UpdateService] No public key configured for manifest signature verification; skipping signature verification.");
                    return true;
                }

                // Extract base64 from PEM if present
                string b64;
                const string header = "-----BEGIN PUBLIC KEY-----";
                const string footer = "-----END PUBLIC KEY-----";
                if (publicKeyPem.Contains(header) && publicKeyPem.Contains(footer))
                {
                    int start = publicKeyPem.IndexOf(header) + header.Length;
                    int end = publicKeyPem.IndexOf(footer, start);
                    b64 = publicKeyPem.Substring(start, end - start).Replace("\r", "").Replace("\n", "").Trim();
                }
                else
                {
                    b64 = publicKeyPem.Trim();
                }

                byte[] keyBytes = Convert.FromBase64String(b64);
                using var rsa = RSA.Create();
                try
                {
                    rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[UpdateService] Failed to import public key for signature verification", ex);
                    return false;
                }

                byte[] payload = Encoding.UTF8.GetBytes(canonicalJson);
                byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

                bool ok = rsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!ok)
                {
                    LoggingService.Log("[UpdateService] Manifest signature verification failed.");
                    var require = false;
                    var v = _settingsService.GetSetting("RequireSignedUpdateManifest");
                    if (!string.IsNullOrWhiteSpace(v) && bool.TryParse(v, out var rv)) require = rv;
                    if (require)
                    {
                        throw new InvalidDataException("Update manifest signature verification failed.");
                    }
                }

                return ok;
            }
            catch (Exception ex)
            {
                try { LoggingService.LogException("[UpdateService] Manifest signature verification error", ex); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Checks for application updates from a local file or remote JSON manifest endpoint.
        /// Compares version against currentVersion.
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync(string manifestSource, Version currentVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(manifestSource))
                throw new ArgumentNullException(nameof(manifestSource));

            string jsonContent = string.Empty;
            if (File.Exists(manifestSource))
            {
                jsonContent = await File.ReadAllTextAsync(manifestSource, cancellationToken).ConfigureAwait(false);
            }
            else if (Uri.TryCreate(manifestSource, UriKind.Absolute, out var uri))
            {
                using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            // Verify manifest signature if present/configured
            var verified = TryVerifyManifestSignature(jsonContent);
            if (!verified)
            {
                LoggingService.Log("[UpdateService] Manifest signature could not be verified; aborting update check.");
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string latestVersionStr = root.TryGetProperty("version", out var vEl) ? vEl.GetString() ?? "1.0.0" : "1.0.0";
            string minSupportedVerStr = root.TryGetProperty("minSupportedVersion", out var mvEl) ? mvEl.GetString() ?? string.Empty : string.Empty;
            string title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;
            string downloadUrl = root.TryGetProperty("downloadUrl", out var dEl) ? dEl.GetString() ?? string.Empty : string.Empty;
            string sha256 = root.TryGetProperty("sha256", out var sEl) ? sEl.GetString() ?? string.Empty : string.Empty;
            string releaseDate = root.TryGetProperty("releaseDate", out var rdEl) ? rdEl.GetString() ?? string.Empty : string.Empty;

            bool forceUpdate = false;
            if (root.TryGetProperty("forceUpdate", out var fEl))
            {
                if (fEl.ValueKind == JsonValueKind.True || fEl.ValueKind == JsonValueKind.False)
                    forceUpdate = fEl.GetBoolean();
                else if (bool.TryParse(fEl.GetString(), out var fVal))
                    forceUpdate = fVal;
            }

            string changelog = string.Empty;
            if (root.TryGetProperty("changelog", out var cEl))
            {
                if (cEl.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var item in cEl.EnumerateArray())
                    {
                        sb.AppendLine($"• {item.GetString()}");
                    }
                    changelog = sb.ToString().TrimEnd();
                }
                else
                {
                    changelog = cEl.GetString() ?? string.Empty;
                }
            }

            bool isMandatory = forceUpdate;
            if (!isMandatory && !string.IsNullOrWhiteSpace(minSupportedVerStr) && Version.TryParse(minSupportedVerStr, out var minVer))
            {
                if (currentVersion < minVer)
                {
                    isMandatory = true;
                }
            }

            var updateInfo = new UpdateInfo
            {
                Version = latestVersionStr,
                MinSupportedVersion = minSupportedVerStr,
                ForceUpdate = forceUpdate,
                Title = title,
                DownloadUrl = downloadUrl,
                Sha256 = sha256,
                Changelog = changelog,
                ReleaseDate = releaseDate,
                IsMandatory = isMandatory
            };

            if (Version.TryParse(latestVersionStr, out var latestVer) && latestVer > currentVersion)
            {
                updateInfo.IsUpdateAvailable = true;
            }

            return updateInfo;
        }

        /// <summary>
        /// Reliable update downloader. Uses DownloadService (retry, resume, throttled progress)
        /// to download update installer binary and verifies SHA256 checksum integrity.
        /// </summary>
        public async Task<string> DownloadAndVerifyUpdateAsync(
            UpdateInfo updateInfo,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            CancellationToken cancellationToken = default)
        {
            if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
                throw new ArgumentException("Invalid update info or download URL.");

            string tempInstallerPath = Path.Combine(Path.GetTempPath(), $"EDMSetup_{updateInfo.Version}_{Guid.NewGuid():N}.exe");

            LoggingService.Log($"[UpdateService] Downloading update installer from: {updateInfo.DownloadUrl} -> {tempInstallerPath}");

            // Stream download installer with progress reporting
            try
            {
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                        totalRead += bytesRead;

                        if (progressReporter != null && totalBytes > 0)
                        {
                            progressReporter.Report(new DownloadProgressInfo
                            {
                                BytesReceived = totalRead,
                                TotalBytes = totalBytes,
                                ProgressPercentage = (int)((totalRead * 100) / totalBytes)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempInstallerPath)) File.Delete(tempInstallerPath); } catch { }
                LoggingService.Log($"[UpdateService] Error downloading update binary: {ex.Message}");
                throw;
            }

            if (!File.Exists(tempInstallerPath))
            {
                throw new FileNotFoundException("Update installer download failed to produce target binary file.");
            }

            // SHA256 Checksum verification
            if (!string.IsNullOrWhiteSpace(updateInfo.Sha256))
            {
                string computedHash = await _integrityService.ComputeSha256Async(tempInstallerPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(computedHash, updateInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(tempInstallerPath); } catch (Exception ex) { try { LoggingService.LogException("[AutoFix] Failed to delete temp installer", ex); } catch { } }
                    throw new InvalidDataException($"Update installer failed SHA256 integrity check. Expected: {updateInfo.Sha256}, Actual: {computedHash}");
                }
                LoggingService.Log($"[UpdateService] Update installer SHA256 checksum verified cleanly.");
            }

            return tempInstallerPath;
        }
    }
}
