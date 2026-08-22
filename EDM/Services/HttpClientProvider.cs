using System;
using System.Net.Http;

namespace EDM.Services
{
    public class HttpClientSettings
    {
        public int MaxConnectionsPerServer { get; set; } = 100; // higher default
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class HttpClientProvider : IDisposable
    {
        private readonly object _lock = new object();
        private HttpClient? _client;
        private HttpClientSettings _settings;

        public HttpClientProvider(HttpClientSettings? settings = null)
        {
            _settings = settings ?? new HttpClientSettings();
        }

        public HttpClient GetClient()
        {
            if (_client != null)
                return _client;

            lock (_lock)
            {
                if (_client == null)
                    _client = CreateClient(_settings);
            }

            return _client;
        }

        public void UpdateSettings(HttpClientSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (_lock)
            {
                if (AreSettingsEqual(_settings, settings))
                    return;

                var old = _client;
                _settings = settings;
                _client = CreateClient(_settings);
                old?.Dispose();
            }
        }

        private static bool AreSettingsEqual(HttpClientSettings a, HttpClientSettings b)
        {
            if (a == null || b == null) return false;
            return a.MaxConnectionsPerServer == b.MaxConnectionsPerServer
                   && a.Timeout == b.Timeout
                   && a.PooledConnectionLifetime == b.PooledConnectionLifetime
                   && a.ConnectTimeout == b.ConnectTimeout;
        }

        private static HttpClient CreateClient(HttpClientSettings settings)
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = Math.Max(64, settings.MaxConnectionsPerServer),
                PooledConnectionLifetime = settings.PooledConnectionLifetime,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                ConnectTimeout = settings.ConnectTimeout,
            };

            var client = new HttpClient(handler)
            {
                Timeout = settings.Timeout
            };

            return client;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _client?.Dispose();
                _client = null;
            }
        }
    }
}
