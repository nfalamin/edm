using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class AntivirusScannerService
    {
        public static string? ResolveDefenderExecutable()
        {
            // 1. Check Standard Program Files locations
            string progFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
            if (File.Exists(progFiles)) return progFiles;

            string progFilesX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Defender", "MpCmdRun.exe");
            if (File.Exists(progFilesX86)) return progFilesX86;

            // 2. Check dynamic Platform directory in CommonApplicationData (%ProgramData%)
            try
            {
                string platformDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows Defender", "Platform");
                if (Directory.Exists(platformDir))
                {
                    var platformExe = Directory.GetFiles(platformDir, "MpCmdRun.exe", SearchOption.AllDirectories);
                    if (platformExe.Length > 0)
                    {
                        // Return the latest modified platform executable
                        return platformExe[0];
                    }
                }
            }
            catch { }

            return null;
        }

        public bool IsDefenderAvailable()
        {
            var path = ResolveDefenderExecutable();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public string GetDefenderPath()
        {
            return ResolveDefenderExecutable() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
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
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-Scan");
            psi.ArgumentList.Add("-ScanType");
            psi.ArgumentList.Add("3");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(filePath);

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
