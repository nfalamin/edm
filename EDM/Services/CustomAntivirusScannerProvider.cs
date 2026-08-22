using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class AntivirusScanResult
    {
        public bool IsClean { get; set; }
        public int ExitCode { get; set; }
        public string? ThreatName { get; set; }
        public string Output { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string ScannerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Extensible Antivirus Scanner Provider interface.
    /// </summary>
    public interface IAntivirusScannerProvider
    {
        string ProviderName { get; }
        bool IsAvailable { get; }
        Task<AntivirusScanResult> ScanFileAsync(string filePath, CancellationToken ct = default);
    }

    /// <summary>
    /// Custom Configurable Antivirus Scanner Provider with argument placeholders (%FILE%, %DIRECTORY%).
    /// </summary>
    public class CustomAntivirusScannerProvider : IAntivirusScannerProvider
    {
        public string ProviderName { get; set; } = "Custom External Scanner";
        public string ExecutablePath { get; set; } = string.Empty;
        public string ArgumentsTemplate { get; set; } = "\"%FILE%\"";
        public int ExpectedCleanExitCode { get; set; } = 0;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        public bool IsAvailable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);

        public async Task<AntivirusScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
        {
            var result = new AntivirusScanResult
            {
                ScannerName = ProviderName,
                IsClean = false
            };

            if (!File.Exists(filePath))
            {
                result.Output = "File does not exist: " + filePath;
                return result;
            }

            if (!IsAvailable)
            {
                result.Output = "Antivirus executable not found: " + ExecutablePath;
                return result;
            }

            var sw = Stopwatch.StartNew();
            string fileDir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string formattedArgs = ArgumentsTemplate
                .Replace("%FILE%", filePath, StringComparison.OrdinalIgnoreCase)
                .Replace("%DIRECTORY%", fileDir, StringComparison.OrdinalIgnoreCase);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = formattedArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(Timeout);

                var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

                result.Duration = sw.Elapsed;
                result.ExitCode = process.ExitCode;
                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);
                result.Output = $"{stdout}\n{stderr}".Trim();
                result.IsClean = (process.ExitCode == ExpectedCleanExitCode);

                LoggingService.Log($"[AntivirusScanner] Scan completed for {filePath}. Clean: {result.IsClean}, Exit: {result.ExitCode}");
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Output = "Scan timed out after " + Timeout.TotalSeconds + "s";
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[AntivirusScanner] Execution failure for {filePath}", ex);
                result.Output = "Scan execution error: " + ex.Message;
                return result;
            }
        }
    }
}
