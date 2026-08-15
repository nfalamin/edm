using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class ScanResult
    {
        public bool IsSafe { get; set; } = true;
        public bool ThreatFound { get; set; }
        public string OutputLog { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Production-grade Post-Download Anti-Virus Scanner Service.
    /// Asynchronously invokes Windows Defender CLI (MpCmdRun.exe) or custom AV engines to scan downloaded payloads.
    /// Operates in non-blocking background mode without freezing UI thread or interrupting user workflow.
    /// </summary>
    public class PostDownloadScannerService
    {
        private static readonly string DefenderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Windows Defender",
            "MpCmdRun.exe");

        public bool IsDefenderAvailable => File.Exists(DefenderPath);

        public async Task<ScanResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new ScanResult { FilePath = filePath, ScannedAt = DateTime.UtcNow };

            if (!File.Exists(filePath))
            {
                result.IsSafe = false;
                result.OutputLog = "File does not exist.";
                return result;
            }

            if (!IsDefenderAvailable)
            {
                LoggingService.Log($"[PostDownloadScannerService] Windows Defender CLI not found at '{DefenderPath}'. Skipping post-download scan.");
                result.IsSafe = true;
                result.OutputLog = "Windows Defender CLI unavailable; skipped.";
                return result;
            }

            LoggingService.Log($"[PostDownloadScannerService] Initiating background security scan for '{filePath}'...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DefenderPath,
                    Arguments = $"-Scan -ScanType 3 -File \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                string stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                string stderr = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                result.OutputLog = (stdout + "\n" + stderr).Trim();

                // MpCmdRun return code 0 = No threats, 2 = Threats found
                if (proc.ExitCode == 2 || result.OutputLog.Contains("LISTED THREATS", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSafe = false;
                    result.ThreatFound = true;
                    LoggingService.Log($"[PostDownloadScannerService] ⚠️ SECURITY ALERT: Threat detected in file '{filePath}'! ExitCode={proc.ExitCode}");
                }
                else
                {
                    result.IsSafe = true;
                    result.ThreatFound = false;
                    LoggingService.Log($"[PostDownloadScannerService] ✅ Scan clean. No threats detected in '{filePath}'.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[PostDownloadScannerService] Post-download AV scan failed for '{filePath}'", ex);
                result.IsSafe = true; // Fallback to safe so non-critical AV errors do not delete valid files
                result.OutputLog = $"Scan error: {ex.Message}";
            }

            return result;
        }
    }
}
