using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDM.Services.Telemetry
{
    /// <summary>
    /// Top-Level Privacy-Preserving Telemetry & Diagnostic Orchestrator.
    /// Ingests application lifecycle events, sanitizes PII, buffers to offline queue,
    /// and manages transparent user opt-out preferences.
    /// </summary>
    public class TelemetryManager
    {
        private static readonly Lazy<TelemetryManager> _instance = new(() => new TelemetryManager());
        public static TelemetryManager Instance => _instance.Value;

        private readonly TelemetryQueueService _queue;
        private readonly TelemetryTransmissionEngine _transmitter;
        private bool _isEnabled = true;

        public bool IsEnabled => _isEnabled;

        public TelemetryManager(TelemetryQueueService? queue = null, TelemetryTransmissionEngine? transmitter = null)
        {
            _queue = queue ?? TelemetryQueueService.Instance;
            _transmitter = transmitter ?? TelemetryTransmissionEngine.Instance;

            try
            {
                var settings = new SettingsService();
                string? val = settings.GetSetting("DiagnosticTelemetryEnabled");
                if (bool.TryParse(val, out bool isOptIn))
                {
                    _isEnabled = isOptIn;
                }
            }
            catch { }

            _transmitter.IsEnabled = _isEnabled;
        }

        public void SetTelemetryEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _transmitter.IsEnabled = enabled;

            try
            {
                var settings = new SettingsService();
                settings.SetSetting("DiagnosticTelemetryEnabled", enabled.ToString());
            }
            catch { }

            if (!enabled)
            {
                _queue.Clear(); // Drop all buffered telemetry immediately upon opt-out
            }
        }

        public void TrackDownloadCompleted(string rawUrl, long totalBytes, double durationSec, double avgMbps, double peakMbps, int segments, string? cdn = null, double diskMbps = 0.0)
        {
            if (!_isEnabled) return;

            var evt = new TelemetryEvent
            {
                Type = "DOWNLOAD_COMPLETED",
                TimestampUtc = DateTime.UtcNow,
                DownloadMetrics = new DownloadMetricsPayload
                {
                    Protocol = "HTTP_MULTIPART",
                    FileExtension = TelemetrySanitizer.ExtractExtension(rawUrl),
                    TotalBytes = totalBytes,
                    DurationSeconds = Math.Max(0.1, durationSec),
                    AverageSpeedMbps = avgMbps,
                    PeakSpeedMbps = peakMbps,
                    ActiveSegments = segments,
                    CdnDetected = cdn,
                    DiskWriteThroughputMbps = diskMbps
                }
            };

            _queue.Enqueue(evt);
            TriggerFlushIfNeeded();
        }

        public void TrackDownloadFailed(string rawUrl, string errorCode, string errorCategory, string stackTrace, string? recoveryAction = null)
        {
            if (!_isEnabled) return;

            var evt = new TelemetryEvent
            {
                Type = "DOWNLOAD_FAILED",
                TimestampUtc = DateTime.UtcNow,
                Fault = new FaultDiagnosticPayload
                {
                    Category = errorCategory,
                    ErrorCode = errorCode,
                    DomainHost = TelemetrySanitizer.SanitizeHost(rawUrl),
                    SanitizedStackTrace = TelemetrySanitizer.SanitizeStackTrace(stackTrace),
                    RecoveryActionTaken = recoveryAction
                }
            };

            _queue.Enqueue(evt);
            TriggerFlushIfNeeded();
        }

        public void TrackFault(string category, string errorCode, string stackTrace)
        {
            if (!_isEnabled) return;

            var evt = new TelemetryEvent
            {
                Type = "FAULT_DIAGNOSTIC",
                TimestampUtc = DateTime.UtcNow,
                Fault = new FaultDiagnosticPayload
                {
                    Category = category,
                    ErrorCode = errorCode,
                    DomainHost = "internal",
                    SanitizedStackTrace = TelemetrySanitizer.SanitizeStackTrace(stackTrace)
                }
            };

            _queue.Enqueue(evt);
            TriggerFlushIfNeeded();
        }

        public void RecordHeartbeat()
        {
            if (!_isEnabled) return;

            var evt = new TelemetryEvent
            {
                Type = "HEARTBEAT",
                TimestampUtc = DateTime.UtcNow
            };

            _queue.Enqueue(evt);
        }

        public Task<bool> FlushNowAsync()
        {
            return _transmitter.FlushBatchAsync();
        }

        private void TriggerFlushIfNeeded()
        {
            if (_queue.GetPendingCount() >= 25)
            {
                _ = Task.Run(() => _transmitter.FlushBatchAsync());
            }
        }
    }
}
