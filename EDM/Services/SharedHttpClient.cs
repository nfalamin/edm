using System;
using System.Net.Http;
using EDM.Models;

namespace EDM.Services
{
    /// <summary>
    /// Process-wide shared HttpClient. Unlike a plain Lazy singleton, this can be
    /// reconfigured at runtime (e.g. when the user changes proxy settings) without
    /// requiring an application restart - new downloads pick up the new client on
    /// their next call to <see cref="Instance"/>. Existing in-flight requests keep
    /// using the client instance they already captured and finish safely.
    /// </summary>
    public static class SharedHttpClient
    {
        private static readonly object _lock = new object();
        private static HttpClient? _client;
        private static ProxySettings? _appliedProxySettings;
        private static System.Threading.Timer? _graceTimer;

        public static HttpClient Instance
        {
            get
            {
                if (_client != null) return _client;
                lock (_lock)
                {
                    if (_client == null)
                    {
                        _client = CreateClient(null);
                    }
                    return _client;
                }
            }
        }

        /// <summary>
        /// Rebuilds the shared client using the given proxy settings. Safe to call at any time
        /// (e.g. right after the user saves proxy settings in the Settings window). The previous
        /// client is disposed safely with a grace period to allow in-flight requests to complete.
        /// </summary>
        public static void ApplyProxySettings(ProxySettings? proxySettings)
        {
            HttpClient? old = null;
            lock (_lock)
            {
                old = _client;
                _client = CreateClient(proxySettings);
                _appliedProxySettings = proxySettings;
            }

            // Delay disposal of the old HttpClient to reduce the chance of aborting in-flight requests.
            // Use Timer instead of Task.Run to ensure proper cleanup and no task leaks.
            if (old != null)
            {
                DisposeOldClientSafely(old);
            }

            LoggingService.Log($"[SharedHttpClient] Reconfigured shared HttpClient. {(proxySettings?.ToString() ?? "Proxy: disabled")}");
        }

        /// <summary>
        /// Rebuilds the shared HttpClient when the network interface changes (e.g. WiFi → Ethernet).
        /// The new SocketsHttpHandler will open connections over the new OS routing table.
        /// In-flight requests on the old client finish normally; the old client is disposed after a grace period.
        /// </summary>
        public static void RebuildForNetworkChange(NetworkType newNetworkType)
        {
            HttpClient? old = null;
            lock (_lock)
            {
                old = _client;
                _client = CreateClient(_appliedProxySettings, newNetworkType);
            }

            if (old != null)
            {
                DisposeOldClientSafely(old);
            }

            LoggingService.Log($"[SharedHttpClient] Rebuilt HttpClient for network change → {newNetworkType}.");
        }

        /// <summary>
        /// Safely disposes the old client with a grace period, using Timer to track lifetime.
        /// </summary>
        private static void DisposeOldClientSafely(HttpClient? oldClient)
        {
            if (oldClient == null) return;

            // Cancel any existing grace timer to prevent resource accumulation
            lock (_lock)
            {
                try { _graceTimer?.Dispose(); } catch (Exception ex) { LoggingService.LogException("[SharedHttpClient] Grace timer dispose failed", ex); }
                _graceTimer = null;
            }

            const int graceMs = 5_000; // 5 seconds grace period (reduced from 10s for better resource cleanup)
            _graceTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    oldClient.Dispose();
                    LoggingService.Log("[SharedHttpClient] Old HttpClient disposed after grace period");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SharedHttpClient] Failed disposing old client after grace period", ex);
                }
                finally
                {
                    lock (_lock)
                    {
                        try { _graceTimer?.Dispose(); } catch { }
                        _graceTimer = null;
                    }
                }
            }, null, graceMs, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Set bandwidth throttle limit in KB/s. Pass 0 to disable throttling.
        /// </summary>
        public static void SetBandwidthThrottle(int limitKbps)
        {
            lock (_lock)
            {
                _bandwidthThrottleKbps = Math.Max(0, limitKbps);
                try
                {
                    // Update global throttler
                    BandwidthThrottler.Instance.SetLimit(_bandwidthThrottleKbps);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SharedHttpClient] Failed to apply bandwidth throttle to BandwidthThrottler", ex);
                }

                if (_bandwidthThrottleKbps > 0)
                {
                    LoggingService.Log($"[SharedHttpClient] Bandwidth throttle set to {_bandwidthThrottleKbps} KB/s");
                }
                else
                {
                    LoggingService.Log($"[SharedHttpClient] Bandwidth throttle disabled");
                }
            }
        }

        private static int _bandwidthThrottleKbps = 0;

        /// <summary>
        /// Get current bandwidth throttle limit in KB/s. Returns 0 if throttling is disabled.
        /// </summary>
        public static int GetBandwidthThrottle()
        {
            lock (_lock)
            {
                return _bandwidthThrottleKbps;
            }
        }

            public static ProxySettings? CurrentProxySettings
        {
            get { lock (_lock) { return _appliedProxySettings; } }
        }

        private static HttpClient CreateClient(ProxySettings? proxySettings, NetworkType networkType = NetworkType.WiFi)
        {
            // Tune connection pool size based on interface type (allow high concurrency for 32-segment parallel pipelines)
            int maxConnections = networkType switch
            {
                NetworkType.Ethernet => 64,
                NetworkType.WiFi => 32,
                NetworkType.Vpn => 32,
                NetworkType.MobileHotspot => 12,
                NetworkType.Cellular => 8,
                NetworkType.MeteredNetwork => 8,
                _ => 32
            };

            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = Math.Max(64, maxConnections),
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                EnableMultipleHttp2Connections = true,
                InitialHttp2StreamWindowSize = 16 * 1024 * 1024,
                AllowAutoRedirect = true
            };



            var webProxy = ProxyService.BuildWebProxy(proxySettings);
            if (webProxy != null)
            {
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Add default headers once
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EDM/1.0 (+https://example)");
            return client;
        }


        /// <summary>
        /// Send a request with simple retry and exponential backoff. Honors the provided cancellation token.
        /// IMPORTANT: Caller MUST dispose the returned HttpResponseMessage.
        /// </summary>
        public static async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage req, HttpCompletionOption option, CancellationToken ct, int maxRetries = 3)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            int attempt = 0;
            const int baseDelayMs = 250;
            while (true)
            {
                try
                {
                    var client = Instance;
                    var resp = await client.SendAsync(req, option, ct).ConfigureAwait(false);
                    return resp;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    attempt++;
                    LoggingService.LogException($"[SharedHttpClient] SendWithRetry attempt {attempt} failed", ex);
                    int jitter = Random.Shared.Next(0, 100);
                    int delay = Math.Min(10000, baseDelayMs * (1 << attempt) + jitter);
                    try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch { }
                    // Continue to retry with same request message (do NOT create new request to avoid leaks)
                    continue;
                }
            }
        }
    }
}