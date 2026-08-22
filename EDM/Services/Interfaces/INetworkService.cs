using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Provides network type detection and configuration capabilities.
    /// Detects WiFi, Ethernet, VPN, Mobile Hotspot, and Metered networks.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Detects the current network type.
        /// </summary>
        /// <returns>NetworkType enumeration value.</returns>
        NetworkType GetCurrentNetworkType();

        /// <summary>
        /// Determines if the network connection is metered (data-limited).
        /// Includes Mobile Hotspot and carriers' metered connections.
        /// </summary>
        /// <returns>True if metered, false otherwise.</returns>
        bool IsMeteredNetwork();

        /// <summary>
        /// Detects if the system is connected via VPN.
        /// Looks for TAP adapter or common VPN interface patterns.
        /// </summary>
        /// <returns>True if VPN is active, false otherwise.</returns>
        bool IsVpnActive();

        /// <summary>
        /// Gets the recommended maximum connection count based on network type.
        /// Respects user override if set in preferences.
        /// </summary>
        /// <param name="userOverride">User-specified connection limit override (0 = use default).</param>
        /// <returns>Recommended connection count (e.g., 2-8).</returns>
        int GetRecommendedConnectionCount(int userOverride = 0);

        /// <summary>
        /// Gets a human-readable description of the current network.
        /// </summary>
        /// <returns>Network name/description (e.g., "WiFi (Home Network)" or "Mobile Hotspot").</returns>
        string GetNetworkDescription();

        /// <summary>
        /// Checks if the system has active internet connectivity.
        /// </summary>
        /// <returns>True if internet is available, false otherwise.</returns>
        Task<bool> HasInternetConnectivityAsync();
    }

    /// <summary>
    /// Enumeration of network types that affect download behavior.
    /// </summary>
    public enum NetworkType
    {
        /// <summary>Unknown or unable to determine network type.</summary>
        Unknown = 0,

        /// <summary>Wired Ethernet connection (unlimited bandwidth).</summary>
        Ethernet = 1,

        /// <summary>WiFi connection (typically unlimited, unless tethered).</summary>
        WiFi = 2,

        /// <summary>Mobile hotspot or personal hotspot (metered, limited bandwidth).</summary>
        MobileHotspot = 3,

        /// <summary>Cellular/Mobile data connection (metered, limited bandwidth).</summary>
        Cellular = 4,

        /// <summary>Network with data limits enforced by carrier/ISP (metered).</summary>
        MeteredNetwork = 5,

        /// <summary>VPN connection (bandwidth depends on VPN provider and underlying network).</summary>
        Vpn = 6,

        /// <summary>No internet connection.</summary>
        Offline = 7
    }
}
