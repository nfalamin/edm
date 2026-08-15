using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class AntivirusProfile
    {
        public string ProfileId { get; set; } = "defender";
        public string ProfileName { get; set; } = "Windows Defender";
        public string ExecutablePath { get; set; } = @"C:\Program Files\Windows Defender\MpCmdRun.exe";
        public string ArgumentsTemplate { get; set; } = "-Scan -ScanType 3 -File \"%FILE%\"";
        public List<int> CleanExitCodes { get; set; } = new() { 0 };
        public List<int> ThreatExitCodes { get; set; } = new() { 2 };
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class AntivirusScanReport
    {
        public bool IsSafe { get; set; }
        public bool ThreatFound { get; set; }
        public int ExitCode { get; set; }
        public string ScanOutput { get; set; } = string.Empty;
        public string ScannedFilePath { get; set; } = string.Empty;
        public string ScannerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Advanced Multi-Engine Antivirus Integration Subsystem.
    /// Supports customizable AV profiles (Windows Defender, Avast, Kaspersky, ESET, Custom),
    /// parameter token resolution (%FILE%, %DIR%, %NAME%), and secure non-shell argument execution.
    /// </summary>
    public class CustomAntivirusScannerService
    {
        private static readonly Lazy<CustomAntivirusScannerService> _instance = new(() => new CustomAntivirusScannerService());
        public static CustomAntivirusScannerService Instance => _instance.Value;

        private readonly List<AntivirusProfile> _profiles = new();
        private AntivirusProfile _activeProfile;

        public AntivirusProfile ActiveProfile => _activeProfile;

        public CustomAntivirusScannerService()
        {
            // 1. Windows Defender
            var defender = new AntivirusProfile
            {
                ProfileId = "defender",
                ProfileName = "Windows Defender",
                ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe"),
                ArgumentsTemplate = "-Scan -ScanType 3 -File \"%FILE%\"",
                CleanExitCodes = new List<int> { 0 },
                ThreatExitCodes = new List<int> { 2 }
            };
            _profiles.Add(defender);

            // 2. Avast / AVG
            _profiles.Add(new AntivirusProfile
            {
                ProfileId = "avast",
                ProfileName = "Avast / AVG Command Line",
                ExecutablePath = @"C:\Program Files\AVAST Software\Avast\ashCmd.exe",
                ArgumentsTemplate = "\"%FILE%\" /p",
                CleanExitCodes = new List<int> { 0 },
                ThreatExitCodes = new List<int> { 1, 2 }
            });

            // 3. Kaspersky
            _profiles.Add(new AntivirusProfile
            {
                ProfileId = "kaspersky",
                ProfileName = "Kaspersky Anti-Virus",
                ExecutablePath = @"C:\Program Files (x86)\Kaspersky Lab\Kaspersky\avp.com",
                ArgumentsTemplate = "scan \"%FILE%\"",
                CleanExitCodes = new List<int> { 0 },
                ThreatExitCodes = new List<int> { 1, 2 }
            });

            // 4. ESET NOD32
            _profiles.Add(new AntivirusProfile
            {
                ProfileId = "eset",
                ProfileName = "ESET Command Line Scanner",
                ExecutablePath = @"C:\Program Files\ESET\ESET Security\ecls.exe",
                ArgumentsTemplate = "/files \"%FILE%\"",
                CleanExitCodes = new List<int> { 0 },
                ThreatExitCodes = new List<int> { 1, 10 }
            });

            _activeProfile = defender;
        }

        public void SetActiveProfile(string profileId)
        {
            var found = _profiles.Find(p => string.Equals(p.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
            if (found != null) _activeProfile = found;
        }

        public async Task<AntivirusScanReport> ScanPayloadAsync(string filePath, CancellationToken ct = default)
        {
            var report = new AntivirusScanReport
            {
                ScannedFilePath = filePath,
                ScannerName = _activeProfile.ProfileName
            };

            if (!File.Exists(filePath))
            {
                report.IsSafe = false;
                report.ScanOutput = "File does not exist.";
                return report;
            }

            if (!File.Exists(_activeProfile.ExecutablePath))
            {
                LoggingService.Log($"[CustomAntivirusScanner] Scanner executable '{_activeProfile.ExecutablePath}' not found on machine.");
                report.IsSafe = true; // Skip gracefully
                report.ScanOutput = "Scanner executable not found; scan skipped.";
                return report;
            }

            try
            {
                string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                string name = Path.GetFileName(filePath);

                // Resolve placeholders
                string resolvedArgs = _activeProfile.ArgumentsTemplate
                    .Replace("%FILE%", filePath)
                    .Replace("%DIR%", dir)
                    .Replace("%NAME%", name);

                var psi = new ProcessStartInfo
                {
                    FileName = _activeProfile.ExecutablePath,
                    Arguments = resolvedArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    report.IsSafe = true;
                    return report;
                }

                string stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                string stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                report.ExitCode = proc.ExitCode;
                report.ScanOutput = (stdout + "\n" + stderr).Trim();

                if (_activeProfile.ThreatExitCodes.Contains(proc.ExitCode))
                {
                    report.IsSafe = false;
                    report.ThreatFound = true;
                    LoggingService.Log($"[CustomAntivirusScanner] ⚠️ Threat detected by {_activeProfile.ProfileName} in '{filePath}'!");
                }
                else
                {
                    report.IsSafe = true;
                    report.ThreatFound = false;
                }

                return report;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[CustomAntivirusScanner] AV Scan exception", ex);
                report.IsSafe = true;
                report.ScanOutput = $"Scan error: {ex.Message}";
                return report;
            }
        }
    }
}
