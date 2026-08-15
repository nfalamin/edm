using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class AntivirusScannerService
    {
        private static readonly string[] PossibleDefenderPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Defender", "MpCmdRun.exe"),
            @"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.23050.5-0\MpCmdRun.exe"
        };

        public bool IsDefenderAvailable()
        {
            foreach (var path in PossibleDefenderPaths)
            {
                if (File.Exists(path)) return true;
            }
            return false;
        }

        public string GetDefenderPath()
        {
            foreach (var path in PossibleDefenderPaths)
            {
                if (File.Exists(path)) return path;
            }
            return PossibleDefenderPaths[0]; // fallback
        }

        public async Task<bool> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return true; // Nothing to scan
            }

            string exePath = GetDefenderPath();
            if (!File.Exists(exePath))
            {
                // Windows Defender CLI not installed/available; treat as safe
                return true;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-Scan -ScanType 3 -File \"{filePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null) return true;

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                // ExitCode 0 = No threat found; ExitCode 2 = Threat found
                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                // Non-fatal error during scan execution
                return true;
            }
        }
    }
}
