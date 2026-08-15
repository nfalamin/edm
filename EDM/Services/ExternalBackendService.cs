using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class ExternalBackendService
    {
        public async Task<bool> ValidateExecutableAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                if (!File.Exists(path)) return false;
                var psi = new ProcessStartInfo(path, "--version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                await p.WaitForExitAsync().ConfigureAwait(false);
                return p.ExitCode == 0 || p.ExitCode == 1; // some tools return 1 for warnings
            }
            catch
            {
                return false;
            }
        }

        public Task StartAria2cAsync(string aria2cPath, string uri, string outputPath, string extraArgs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(aria2cPath)) throw new ArgumentNullException(nameof(aria2cPath));
            if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentNullException(nameof(uri));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var args = new StringBuilder();
            args.Append("--allow-overwrite=true ");
            args.Append("--max-connection-per-server=16 ");
            args.Append($"--dir=\"{Path.GetDirectoryName(outputPath)}\" ");
            args.Append($"--out=\"{Path.GetFileName(outputPath)}\" ");
            if (!string.IsNullOrWhiteSpace(extraArgs)) args.Append(extraArgs).Append(' ');
            args.Append("--");
            args.Append(' ');
            args.Append('"').Append(uri).Append('"');

            return StartProcessAsync(aria2cPath, args.ToString(), progress, cancellationToken);
        }

        public Task StartYtDlpWithAria2Async(string ytDlpPath, string aria2cPath, string url, string outputDir, string formatArgs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ytDlpPath)) throw new ArgumentNullException(nameof(ytDlpPath));
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));

            var args = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(formatArgs)) args.Append(formatArgs).Append(' ');
            if (!string.IsNullOrWhiteSpace(aria2cPath))
            {
                args.Append($"--external-downloader \"{aria2cPath}\" --external-downloader-args \"-x 16 -s 16 -k 1M\" ");
            }
            if (!string.IsNullOrWhiteSpace(outputDir)) args.Append($"-o \"{Path.Combine(outputDir, "%(title)s.%(ext)s")}\" ");
            args.Append('"').Append(url).Append('"');

            return StartProcessAsync(ytDlpPath, args.ToString(), progress, cancellationToken);
        }

        private async Task StartProcessAsync(string exePath, string arguments, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo(exePath, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) progress?.Report(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) progress?.Report(e.Data); };
            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using (cancellationToken.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(true); } catch (Exception ex) { LoggingService.LogException("[ExternalBackendService] Kill on cancel failed", ex); }
            }))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
