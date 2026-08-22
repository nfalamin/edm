using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum NetworkConnectivityState
    {
        Online,
        Offline,
        Reconnecting
    }

    /// <summary>
    /// NetworkTransitionManager — Monitors system network topology changes (Wi-Fi <-> Ethernet,
    /// VPN disconnects, network dropouts) and orchestrates graceful offline mode pausing and auto-resumption.
    /// </summary>
    public sealed class NetworkTransitionManager : IDisposable
    {
        private static readonly Lazy<NetworkTransitionManager> _lazy = new(() => new NetworkTransitionManager());
        public static NetworkTransitionManager Instance => _lazy.Value;

        private NetworkConnectivityState _state = NetworkConnectivityState.Online;
        private readonly object _lock = new();

        public event Action<NetworkConnectivityState>? ConnectivityChanged;
        public event Action? NetworkRestored;

        public NetworkConnectivityState State
        {
            get { lock (_lock) return _state; }
        }

        public bool IsNetworkAvailable
        {
            get
            {
                try { return NetworkInterface.GetIsNetworkAvailable(); }
                catch { return true; }
            }
        }

        public NetworkTransitionManager()
        {
            try
            {
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                _state = IsNetworkAvailable ? NetworkConnectivityState.Online : NetworkConnectivityState.Offline;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[NetworkTransitionManager] Failed to bind network change listeners: {ex.Message}");
            }
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            lock (_lock)
            {
                var previous = _state;
                _state = e.IsAvailable ? NetworkConnectivityState.Online : NetworkConnectivityState.Offline;
                LoggingService.Log($"[NetworkTransitionManager] Network availability changed: {_state}");
                ConnectivityChanged?.Invoke(_state);

                if (previous == NetworkConnectivityState.Offline && _state == NetworkConnectivityState.Online)
                {
                    NetworkRestored?.Invoke();
                }
            }
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                LoggingService.Log($"[NetworkTransitionManager] Network address/adapter changed. Revalidating connectivity...");
                bool available = IsNetworkAvailable;
                var previous = _state;
                _state = available ? NetworkConnectivityState.Online : NetworkConnectivityState.Offline;
                ConnectivityChanged?.Invoke(_state);

                if (previous == NetworkConnectivityState.Offline && _state == NetworkConnectivityState.Online)
                {
                    NetworkRestored?.Invoke();
                }
            }
        }

        /// <summary>
        /// Asynchronously waits until network connectivity is restored if currently offline.
        /// </summary>
        public async Task WaitForConnectivityAsync(CancellationToken ct)
        {
            if (IsNetworkAvailable) return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = ct.Register(() => tcs.TrySetCanceled());

            Action<NetworkConnectivityState>? handler = null;
            handler = (state) =>
            {
                if (state == NetworkConnectivityState.Online)
                {
                    tcs.TrySetResult();
                }
            };

            ConnectivityChanged += handler;
            try
            {
                if (IsNetworkAvailable) return;
                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                ConnectivityChanged -= handler;
            }
        }

        public void Dispose()
        {
            try
            {
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            }
            catch { }
        }
    }
}
