using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace EDM.Services
{
    /// <summary>
    /// Windows implementation of INetworkMonitor.
    /// Subscribes to System.Net.NetworkInformation.NetworkChange events and fires:
    ///   - ConnectivityLost / ConnectivityRestored when internet availability changes
    ///   - NetworkChanged whenever network type or metered status changes
    ///   - InterfaceSwitched when the dominant interface type changes (e.g. WiFi → Ethernet)
    ///     while connectivity remains uninterrupted — allowing in-flight downloads to rebind.
    /// </summary>
    public sealed class WindowsNetworkMonitor : INetworkMonitor, IDisposable
    {
        private NetworkType _currentNetworkType;
        private bool _isCurrentMetered;
        private bool _wasConnected;
        private bool _disposed;

        // Debounce: OS fires NetworkAddressChanged multiple times per physical event
        private System.Threading.Timer? _debounceTimer;
        private readonly object _debounceLock = new();
        private const int DebounceMs = 500;


        public event EventHandler<NetworkChangedEventArgs>? NetworkChanged;
        public event EventHandler? ConnectivityLost;
        public event EventHandler? ConnectivityRestored;
        public event EventHandler<InterfaceSwitchedEventArgs>? InterfaceSwitched;

        public NetworkType CurrentNetworkType => _currentNetworkType;
        public bool IsCurrentNetworkMetered => _isCurrentMetered;

        public WindowsNetworkMonitor()
        {
            // Capture initial state
            _currentNetworkType = DetectNetworkType();
            _isCurrentMetered = IsMetered(_currentNetworkType);
            _wasConnected = _currentNetworkType != NetworkType.Offline;

            // Subscribe to OS network change events
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            // Immediate connectivity lost/restored — no debounce needed
            try
            {
                if (!e.IsAvailable && _wasConnected)
                {
                    _wasConnected = false;
                    _currentNetworkType = NetworkType.Offline;
                    _isCurrentMetered = false;
                    LoggingService.Log("[WindowsNetworkMonitor] Connectivity lost.");
                    ConnectivityLost?.Invoke(this, EventArgs.Empty);
                    NetworkChanged?.Invoke(this, new NetworkChangedEventArgs
                    {
                        NetworkType = NetworkType.Offline,
                        IsMetered = false,
                        Description = "No internet connectivity"
                    });
                }
                else if (e.IsAvailable && !_wasConnected)
                {
                    // Don't set restored here — let the debounced address-change handler
                    // pick the correct new type. Just mark wasConnected to suppress duplicate lost events.
                    ScheduleDebounce();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[WindowsNetworkMonitor] OnNetworkAvailabilityChanged failed", ex);
            }
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            ScheduleDebounce();
        }

        private void ScheduleDebounce()
        {
            lock (_debounceLock)
            {
                if (_disposed) return;
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ => EvaluateNetworkChange(), null, DebounceMs, Timeout.Infinite);
            }
        }

        private void EvaluateNetworkChange()
        {
            try
            {
                NetworkType newType = DetectNetworkType();
                bool newMetered = IsMetered(newType);
                bool nowConnected = newType != NetworkType.Offline;

                NetworkType previousType = _currentNetworkType;
                bool wasConnectedBefore = _wasConnected;

                _currentNetworkType = newType;
                _isCurrentMetered = newMetered;
                _wasConnected = nowConnected;

                // Fire ConnectivityRestored if we regained internet
                if (nowConnected && !wasConnectedBefore)
                {
                    LoggingService.Log($"[WindowsNetworkMonitor] Connectivity restored. New type: {newType}");
                    ConnectivityRestored?.Invoke(this, EventArgs.Empty);
                }

                // Always fire NetworkChanged so subscribers can update their state
                NetworkChanged?.Invoke(this, new NetworkChangedEventArgs
                {
                    NetworkType = newType,
                    IsMetered = newMetered,
                    Description = DescribeType(newType)
                });

                // Fire InterfaceSwitched if the type changed while staying connected
                // (not an offline→online transition, which is handled by ConnectivityRestored)
                if (nowConnected && wasConnectedBefore && newType != previousType)
                {
                    LoggingService.Log($"[WindowsNetworkMonitor] Interface switched: {previousType} → {newType} (metered={newMetered})");
                    InterfaceSwitched?.Invoke(this, new InterfaceSwitchedEventArgs
                    {
                        PreviousNetworkType = previousType,
                        NewNetworkType = newType,
                        IsNewNetworkMetered = newMetered,
                        Description = $"Interface changed from {previousType} to {newType}"
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[WindowsNetworkMonitor] EvaluateNetworkChange failed", ex);
            }
        }

        private static NetworkType DetectNetworkType()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                    return NetworkType.Offline;

                var interfaces = new System.Collections.Generic.List<NetworkInterface>();
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        interfaces.Add(ni);
                    }
                }

                if (interfaces.Count == 0) return NetworkType.Offline;

                // VPN takes precedence
                foreach (var ni in interfaces)
                {
                    var desc = (ni.Description ?? "").ToLowerInvariant();
                    var name = (ni.Name ?? "").ToLowerInvariant();
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                        desc.Contains("vpn") || desc.Contains("tun") ||
                        name.Contains("tap") || name.Contains("tun"))
                        return NetworkType.Vpn;
                }

                // Cellular / WWAN
                foreach (var ni in interfaces)
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wwanpp ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wwanpp2)
                        return NetworkType.Cellular;
                }

                // Metered / mobile hotspot (Remote NDIS or similar)
                foreach (var ni in interfaces)
                {
                    var desc = (ni.Description ?? "").ToLowerInvariant();
                    if (desc.Contains("remote ndis") || desc.Contains("tether") ||
                        desc.Contains("hotspot") || desc.Contains("mobile"))
                        return NetworkType.MobileHotspot;
                }

                // Ethernet (wired)
                foreach (var ni in interfaces)
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT)
                        return NetworkType.Ethernet;
                }

                return NetworkType.WiFi;
            }
            catch
            {
                return NetworkType.WiFi; // optimistic fallback
            }
        }

        private static bool IsMetered(NetworkType t) =>
            t == NetworkType.Cellular || t == NetworkType.MobileHotspot || t == NetworkType.MeteredNetwork;

        private static string DescribeType(NetworkType t) => t switch
        {
            NetworkType.Ethernet => "Wired Ethernet",
            NetworkType.WiFi => "WiFi",
            NetworkType.Vpn => "VPN",
            NetworkType.Cellular => "Cellular (metered)",
            NetworkType.MobileHotspot => "Mobile Hotspot (metered)",
            NetworkType.MeteredNetwork => "Metered Network",
            NetworkType.Offline => "Offline",
            _ => "Unknown"
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }
    }
}
