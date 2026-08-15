using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EDM.Services
{
    /// <summary>
    /// MultiNicManager - Multi-Interface Network IP Enumeration & Binding Controller.
    /// Enumerates active physical network adapters (Ethernet, Wi-Fi, Cellular) and provides
    /// round-robin local IP address binding to aggregate bandwidth across multiple NICs.
    /// </summary>
    public class MultiNicManager
    {
        private static readonly Lazy<MultiNicManager> _instance = new(() => new MultiNicManager());
        public static MultiNicManager Instance => _instance.Value;

        private readonly object _lock = new();
        private List<IPAddress> _activeLocalIPs = new();
        private int _currentIndex = 0;

        public MultiNicManager()
        {
            RefreshActiveInterfaces();
        }

        /// <summary>
        /// Scans system network adapters and extracts valid operational IPv4 addresses.
        /// Ignores loopback (127.0.0.1) and APIPA (169.254.*) self-assigned addresses.
        /// </summary>
        public List<IPAddress> RefreshActiveInterfaces()
        {
            lock (_lock)
            {
                var ips = new List<IPAddress>();
                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                     ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                     ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                    foreach (var ni in interfaces)
                    {
                        var ipProps = ni.GetIPProperties();
                        foreach (var unicast in ipProps.UnicastAddresses)
                        {
                            if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string ipStr = unicast.Address.ToString();
                                // Exclude loopback & link-local APIPA
                                if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                                {
                                    ips.Add(unicast.Address);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[MultiNicManager] Interface enumeration failed: {ex.Message}");
                }

                _activeLocalIPs = ips;
                _currentIndex = 0;
                return _activeLocalIPs.ToList();
            }
        }

        /// <summary>
        /// Returns all currently detected operational IPv4 local addresses across all network cards.
        /// </summary>
        public List<IPAddress> GetActiveLocalIPs()
        {
            lock (_lock)
            {
                return _activeLocalIPs.ToList();
            }
        }

        /// <summary>
        /// Selects the next local IP address in round-robin sequence to bind outgoing segment socket connections.
        /// Returns null if no active NIC or only 1 default route IP is available.
        /// </summary>
        public IPAddress? GetNextLocalIPAddress()
        {
            lock (_lock)
            {
                if (_activeLocalIPs.Count <= 1)
                {
                    return null; // Standard OS routing table handling
                }

                var selected = _activeLocalIPs[_currentIndex % _activeLocalIPs.Count];
                _currentIndex++;
                return selected;
            }
        }
    }
}
