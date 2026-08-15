using System;

namespace EDM.Services
{
    /// <summary>
    /// Adapter that wires an INetworkMonitor to a PauseTokenSource and provides diagnostics.
    /// Subscribes to network events and pauses/resumes the provided pause token accordingly.
    /// Also handles interface-switch events (e.g. WiFi → Ethernet) by rebuilding the shared
    /// HttpClient so new connections use the updated OS routing table.
    /// </summary>
    public sealed class DownloadNetworkMonitorAdapter : IDisposable
    {
        private readonly INetworkMonitor _monitor;
        private readonly PauseTokenSource? _pauseToken;
        private readonly Action<string>? _diagnostic;
        private bool _disposed;

        private readonly EventHandler? _onLost;
        private readonly EventHandler? _onRestored;
        private readonly EventHandler<NetworkChangedEventArgs>? _onChanged;
        private readonly EventHandler<InterfaceSwitchedEventArgs>? _onSwitched;

        public DownloadNetworkMonitorAdapter(INetworkMonitor monitor, PauseTokenSource? pauseToken, Action<string>? diagnostic = null)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _pauseToken = pauseToken;
            _diagnostic = diagnostic;

            _onLost = (s, e) =>
            {
                try
                {
                    _pauseToken?.Pause();
                    _diagnostic?.Invoke("Network lost - pausing download");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadNetworkMonitorAdapter.onLost]", ex);
                }
            };

            _onRestored = (s, e) =>
            {
                try
                {
                    _pauseToken?.Resume();
                    _diagnostic?.Invoke("Network restored - resuming download");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadNetworkMonitorAdapter.onRestored]", ex);
                }
            };

            _onChanged = (s, e) =>
            {
                try
                {
                    _diagnostic?.Invoke($"Network changed: {e.NetworkType} metered={e.IsMetered}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadNetworkMonitorAdapter.onChanged]", ex);
                }
            };

            _onSwitched = (s, e) =>
            {
                try
                {
                    // Rebuild HttpClient so new TCP connections use the updated OS routing table.
                    // In-flight requests on the old client continue until they complete or retry.
                    SharedHttpClient.RebuildForNetworkChange(e.NewNetworkType);

                    _diagnostic?.Invoke(
                        $"Interface switched: {e.PreviousNetworkType} → {e.NewNetworkType} " +
                        $"(metered={e.IsNewNetworkMetered}). HttpClient rebuilt.");

                    LoggingService.Log(
                        $"[DownloadNetworkMonitorAdapter] Interface switch detected: " +
                        $"{e.PreviousNetworkType} → {e.NewNetworkType}. " +
                        $"HttpClient pool recycled for new routing table.");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadNetworkMonitorAdapter.onSwitched]", ex);
                }
            };

            // Subscribe
            try
            {
                _monitor.ConnectivityLost += _onLost;
                _monitor.ConnectivityRestored += _onRestored;
                _monitor.NetworkChanged += _onChanged;
                _monitor.InterfaceSwitched += _onSwitched;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadNetworkMonitorAdapter] Subscribe failed", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_onLost != null) _monitor.ConnectivityLost -= _onLost;
            }
            catch (Exception ex) { LoggingService.LogException("[DownloadNetworkMonitorAdapter] Unsubscribe lost failed", ex); }
            try
            {
                if (_onRestored != null) _monitor.ConnectivityRestored -= _onRestored;
            }
            catch (Exception ex) { LoggingService.LogException("[DownloadNetworkMonitorAdapter] Unsubscribe restored failed", ex); }
            try
            {
                if (_onChanged != null) _monitor.NetworkChanged -= _onChanged;
            }
            catch (Exception ex) { LoggingService.LogException("[DownloadNetworkMonitorAdapter] Unsubscribe changed failed", ex); }
            try
            {
                if (_onSwitched != null) _monitor.InterfaceSwitched -= _onSwitched;
            }
            catch (Exception ex) { LoggingService.LogException("[DownloadNetworkMonitorAdapter] Unsubscribe switched failed", ex); }
        }
    }
}
