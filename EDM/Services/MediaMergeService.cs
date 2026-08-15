using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Responsible for downloading separate video/audio streams and merging them using ffmpeg.
    /// </summary>
    public sealed class MediaMergeService
    {
        private readonly HttpClient _httpClient;

        public MediaMergeService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Downloads a video-only stream and audio-only stream and merges them into outputPath using ffmpeg.
        /// Temporary files are cleaned up on completion or failure.
        /// </summary>
        public async Task MergeAudioVideoAsync(string videoUrl, string audioUrl, string outputPath, string? ffmpegPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) throw new ArgumentException("videoUrl is required", nameof(videoUrl));
            if (string.IsNullOrWhiteSpace(audioUrl)) throw new ArgumentException("audioUrl is required", nameof(audioUrl));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("outputPath is required", nameof(outputPath));

            string tempVideo = outputPath + ".video.tmp";
            string tempAudio = outputPath + ".audio.tmp";

            try
            {
                LoggingService.Log($"[MediaMergeService] Starting merge: video={videoUrl}, audio={audioUrl}");

                // download video-only
                using (var respV = await _httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    respV.EnsureSuccessStatusCode();
                    await using (var vs = await respV.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    await using (var vfs = new FileStream(tempVideo, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    {
                        await vs.CopyToAsync(vfs, 81920, cancellationToken).ConfigureAwait(false);
                        await vfs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                // download audio-only
                using (var respA = await _httpClient.GetAsync(audioUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    respA.EnsureSuccessStatusCode();
                    await using (var asr = await respA.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    await using (var afs = new FileStream(tempAudio, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    {
                        await asr.CopyToAsync(afs, 81920, cancellationToken).ConfigureAwait(false);
                        await afs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                // merge using ffmpeg
                string exe = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"-y -i \"{tempVideo}\" -i \"{tempAudio}\" -c copy \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    if (proc.ExitCode != 0)
                    {
                        string err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                        LoggingService.Log($"[MediaMergeService] ffmpeg exit code {proc.ExitCode}: {err}");
                        throw new InvalidOperationException("ffmpeg failed to merge streams. Check ffmpeg path and logs.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to start ffmpeg process. Ensure ffmpeg is installed or ffmpegPath is correct.");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LoggingService.Log($"[MediaMergeService] Adaptive merge failed: {ex.Message}");
                throw;
            }
            finally
            {
                try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch (Exception ex) { LoggingService.Log($"[MediaMergeService] Failed to delete temp video file '{tempVideo}': {ex.Message}"); }
                try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch (Exception ex) { LoggingService.Log($"[MediaMergeService] Failed to delete temp audio file '{tempAudio}': {ex.Message}"); }
            }
        }
    }
}
