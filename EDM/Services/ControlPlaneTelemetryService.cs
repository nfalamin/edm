using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class ControlPlaneTelemetryService : IDisposable
    {
        private readonly ControlPlaneClient _client;
        private readonly Channel<(string EventName, object Payload)> _queue;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _workerTask;

        public ControlPlaneTelemetryService(ControlPlaneClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            
            // Bounded queue of up to 1,000 events to prevent memory leaks on server disconnect
            _queue = Channel.CreateBounded<(string, object)>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _workerTask = Task.Run(ProcessQueueAsync);
        }

        public void EnqueueEvent(string eventName, object payload)
        {
            if (string.IsNullOrWhiteSpace(eventName) || payload == null) return;

            // Non-blocking try-write to guarantee 0ms latency impact on download worker threads
            _queue.Writer.TryWrite((eventName, payload));
        }

        public void TrackAppStarted(string version, string osVersion)
        {
            EnqueueEvent("app_started", new { version, osVersion, timestamp = DateTime.UtcNow });
        }

        public void TrackDownloadStarted(string url, long? sizeBytes, int segmentCount)
        {
            EnqueueEvent("download_started", new
            {
                url = SanitizeUrl(url),
                sizeBytes = sizeBytes ?? 0,
                segmentCount,
                timestamp = DateTime.UtcNow
            });
        }

        public void TrackDownloadCompleted(string url, long totalBytes, double durationSeconds, double avgSpeedBps)
        {
            EnqueueEvent("download_completed", new
            {
                url = SanitizeUrl(url),
                totalBytes,
                durationSeconds = Math.Round(durationSeconds, 2),
                avgSpeedBps = Math.Round(avgSpeedBps, 0),
                timestamp = DateTime.UtcNow
            });
        }

        public void TrackDownloadFailed(string url, string errorMessage, bool isRetriable)
        {
            EnqueueEvent("download_failed", new
            {
                url = SanitizeUrl(url),
                errorMessage = errorMessage?.Length > 150 ? errorMessage.Substring(0, 147) + "..." : errorMessage,
                isRetriable,
                timestamp = DateTime.UtcNow
            });
        }

        public void TrackDownloadPaused(string url)
        {
            EnqueueEvent("download_paused", new { url = SanitizeUrl(url), timestamp = DateTime.UtcNow });
        }

        public void TrackDownloadResumed(string url)
        {
            EnqueueEvent("download_resumed", new { url = SanitizeUrl(url), timestamp = DateTime.UtcNow });
        }

        public void TrackVideoDetected(string pageUrl, string title, int variantCount)
        {
            EnqueueEvent("video_detected", new
            {
                pageUrl = SanitizeUrl(pageUrl),
                title = title?.Length > 80 ? title.Substring(0, 77) + "..." : title,
                variantCount,
                timestamp = DateTime.UtcNow
            });
        }

        public void TrackUpdateChecked(string currentVersion, string latestVersion, bool isAvailable)
        {
            EnqueueEvent("update_checked", new
            {
                currentVersion,
                latestVersion,
                isAvailable,
                timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessQueueAsync()
        {
            var reader = _queue.Reader;
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        while (reader.TryRead(out var item))
                        {
                            try
                            {
                                await _client.SendTelemetryEventAsync(item.EventName, item.Payload, token).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Offline or transient failure: silently swallow to avoid crashing background loop
                            }

                            // Small 50ms pause between batches to prevent network burst
                            await Task.Delay(50, token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[ControlPlaneTelemetryService] Queue processing error: {ex.Message}");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        private static string SanitizeUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;
            try
            {
                var uri = new Uri(rawUrl);
                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            }
            catch
            {
                return "https://redacted-url";
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _queue.Writer.TryComplete();
            try { _workerTask.Wait(500); } catch { }
            _cts.Dispose();
        }
    }
}
