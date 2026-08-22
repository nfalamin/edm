using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Windows-based implementation of network type detection.
    /// Uses System.Net.NetworkInformation to detect network characteristics.
    /// </summary>
    public class NetworkService : INetworkService
    {
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// Initializes a new instance of the NetworkService.
        /// </summary>
        /// <param name="settingsService">Settings service for persisting network preferences.</param>
        public NetworkService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Detects the current network type by examining active network interfaces.
        /// </summary>
        public NetworkType GetCurrentNetworkType()
        {
            try
            {
                // Check internet connectivity first
                if (!NetworkInterface.GetIsNetworkAvailable())
                    return NetworkType.Offline;

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                if (interfaces.Count == 0)
                    return NetworkType.Offline;

                // Check for VPN first (takes precedence)
                var vpnInterface = interfaces.FirstOrDefault(ni => IsVpnInterface(ni));
                if (vpnInterface != null)
                    return NetworkType.Vpn;

                // Check for metered/mobile hotspot
                var meteredInterface = interfaces.FirstOrDefault(ni => IsMeteredInterface(ni));
                if (meteredInterface != null)
                {
                    // Distinguish between cellular and personal hotspot
                    if (IsMobileHotspotInterface(meteredInterface))
                        return NetworkType.MobileHotspot;
                    if (IsCellularInterface(meteredInterface))
                        return NetworkType.Cellular;
                    return NetworkType.MeteredNetwork;
                }

                // Check for Ethernet (wired)
                var ethernetInterface = interfaces.FirstOrDefault(ni => 
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
                if (ethernetInterface != null)
                    return NetworkType.Ethernet;

                // Default to WiFi for any other active interface
                return NetworkType.WiFi;
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[NetworkService] GetCurrentNetworkType failed", ex);
                // If detection fails, assume WiFi (optimistic fallback)
                return NetworkType.WiFi;
            }
        }

        /// <summary>
        /// Determines if the current network is metered (data-limited).
        /// </summary>
        public bool IsMeteredNetwork()
        {
            try
            {
                var currentType = GetCurrentNetworkType();
                return currentType == NetworkType.Cellular ||
                       currentType == NetworkType.MobileHotspot ||
                       currentType == NetworkType.MeteredNetwork;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects if the system is connected via VPN.
        /// </summary>
        public bool IsVpnActive()
        {
            try
            {
                return GetCurrentNetworkType() == NetworkType.Vpn;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the recommended connection count based on network type and user preferences.
        /// </summary>
        public int GetRecommendedConnectionCount(int userOverride = 0)
        {
            // Use user override if specified (non-zero)
            if (userOverride > 0)
                return Math.Min(userOverride, 16); // Cap at reasonable limit

            var networkType = GetCurrentNetworkType();
            return networkType switch
            {
                NetworkType.Ethernet => 8,
                NetworkType.WiFi => 8,
                NetworkType.Vpn => 8,
                NetworkType.MobileHotspot => 3,
                NetworkType.Cellular => 2,
                NetworkType.MeteredNetwork => 3,
                NetworkType.Offline => 1,
                _ => 4 // Unknown: conservative default
            };
        }

        /// <summary>
        /// Gets a human-readable network description.
        /// </summary>
        public string GetNetworkDescription()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                if (interfaces.Count == 0)
                    return "No Network Connection";

                var currentType = GetCurrentNetworkType();
                var activeInterface = interfaces.FirstOrDefault(ni => 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                var interfaceName = activeInterface?.Description ?? "Unknown Network";

                return currentType switch
                {
                    NetworkType.Ethernet => $"Ethernet ({interfaceName})",
                    NetworkType.WiFi => $"WiFi ({interfaceName})",
                    NetworkType.Vpn => $"VPN ({interfaceName})",
                    NetworkType.MobileHotspot => "Mobile Hotspot (Metered)",
                    NetworkType.Cellular => "Cellular Connection (Metered)",
                    NetworkType.MeteredNetwork => $"Metered Network ({interfaceName})",
                    NetworkType.Offline => "No Internet Connection",
                    _ => $"Unknown ({interfaceName})"
                };
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[NetworkService] GetNetworkDescription failed", ex);
                return "Network Type Unknown";
            }
        }

        /// <summary>
        /// Checks if the system has active internet connectivity via a simple DNS query.
        /// </summary>
        public async Task<bool> HasInternetConnectivityAsync()
        {
            try
            {
                // Try to resolve a reliable DNS name
                var result = await System.Net.Dns.GetHostEntryAsync("8.8.8.8");
                return result?.AddressList?.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // Helper decision methods
        private bool IsVpnInterface(NetworkInterface ni)
        {
            try
            {
                var name = ni.Name ?? string.Empty;
                var desc = ni.Description ?? string.Empty;
                if (name.IndexOf("tap", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (desc.IndexOf("vpn", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (desc.IndexOf("tun", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("tun", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp) return true;
                return false;
            }
            catch (Exception ex) { EDM.Services.LoggingService.LogException("[NetworkService] IsVpnInterface failed", ex); return false; }
        }

        private bool IsMeteredInterface(NetworkInterface ni)
        {
            try
            {
                if (IsCellularInterface(ni) || IsMobileHotspotInterface(ni)) return true;
                var desc = (ni.Description ?? string.Empty).ToLowerInvariant();
                if (desc.Contains("tether") || desc.Contains("mobile") || desc.Contains("remote nds") || desc.Contains("remote ndis")) return true;
                return false;
            }
            catch (Exception ex) { EDM.Services.LoggingService.LogException("[NetworkService] IsMeteredInterface failed", ex); return false; }
        }

        private bool IsMobileHotspotInterface(NetworkInterface ni)
        {
            try
            {
                var desc = (ni.Description ?? string.Empty).ToLowerInvariant();
                if (desc.Contains("host") || desc.Contains("hotspot")) return true;
                return false;
            }
            catch (Exception ex) { EDM.Services.LoggingService.LogException("[NetworkService] IsMobileHotspotInterface failed", ex); return false; }
        }

        private bool IsCellularInterface(NetworkInterface ni)
        {
            try
            {
                var t = ni.NetworkInterfaceType;
                if (t == NetworkInterfaceType.Wwanpp || t == NetworkInterfaceType.Wwanpp2) return true;
                var desc = (ni.Description ?? string.Empty).ToLowerInvariant();
                if (desc.Contains("cellular") || desc.Contains("wwan") || desc.Contains("mobile")) return true;
                return false;
            }
            catch (Exception ex) { EDM.Services.LoggingService.LogException("[NetworkService] IsCellularInterface failed", ex); return false; }
        }
    }
}
