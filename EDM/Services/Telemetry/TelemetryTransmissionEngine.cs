using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services.Telemetry
{
    /// <summary>
    /// Telemetry Transmission Engine.
    /// Manages periodic and threshold-triggered batch flushes over HTTPS with HMAC-SHA256 integrity signatures.
    /// </summary>
    public class TelemetryTransmissionEngine
    {
        private static readonly Lazy<TelemetryTransmissionEngine> _instance = new(() => new TelemetryTransmissionEngine());
        public static TelemetryTransmissionEngine Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly TelemetryQueueService _queue;
        public string IngestionEndpoint { get; set; } = "https://telemetry.edm-downloadmanager.com/v1/events";
        public string HmacSigningKey { get; set; } = "EDM-Telemetry-Client-Sign-2026";
        public bool IsEnabled { get; set; } = true;

        public TelemetryTransmissionEngine(HttpClient? httpClient = null, TelemetryQueueService? queue = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _queue = queue ?? TelemetryQueueService.Instance;
        }

        public async Task<bool> FlushBatchAsync(CancellationToken ct = default)
        {
            if (!IsEnabled) return false;
            if (_queue.GetPendingCount() == 0) return false;

            var batchEvents = _queue.DequeueBatch(25);
            if (batchEvents.Count == 0) return false;

            var envelope = new TelemetryBatchEnvelope
            {
                ClientHeader = new TelemetryClientHeader
                {
                    AppVersion = "6.0.0",
                    BuildNumber = "20260816.1",
                    Channel = "Stable",
                    OsVersion = Environment.OSVersion.VersionString,
                    UiCulture = LocalizationService.Instance.CurrentCulture,
                    TimestampUtc = DateTime.UtcNow
                },
                Events = batchEvents,
                SystemSnapshot = new SystemSnapshotPayload
                {
                    LogicalCpuCores = Environment.ProcessorCount,
                    WorkingSetMemoryMb = Environment.WorkingSet / (1024 * 1024)
                }
            };

            string json = JsonSerializer.Serialize(envelope);
            string signature = ComputeHmacSignature(json, HmacSigningKey);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, IngestionEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Add("X-EDM-Signature", signature);
                request.Headers.Add("X-EDM-App-Version", envelope.ClientHeader.AppVersion);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5)); // Fast timeout to never block user operations

                var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Re-enqueue dropped batch for next attempt
                foreach (var evt in batchEvents)
                {
                    _queue.Enqueue(evt);
                }
                LoggingService.LogException("[TelemetryTransmissionEngine] Flush failed, re-queued", ex);
                return false;
            }
        }

        public static string ComputeHmacSignature(string payload, string secretKey)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
