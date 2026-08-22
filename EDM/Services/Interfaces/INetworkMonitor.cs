using System;

namespace EDM.Services
{
    /// <summary>
    /// Provides network monitoring events and properties for runtime components to subscribe to.
    /// Separated from INetworkService to keep detection/monitoring concerns distinct from pure lookup APIs.
    /// </summary>
    public interface INetworkMonitor
    {
        /// <summary>
        /// Raised when the network type or status changes (including metered state).
        /// </summary>
        event EventHandler<NetworkChangedEventArgs>? NetworkChanged;

        /// <summary>
        /// Raised when internet connectivity is lost.
        /// </summary>
        event EventHandler? ConnectivityLost;

        /// <summary>
        /// Raised when internet connectivity is restored after loss.
        /// </summary>
        event EventHandler? ConnectivityRestored;

        /// <summary>
        /// Raised when the dominant network interface switches type (e.g. WiFi → Ethernet)
        /// while connectivity remains uninterrupted. Allows in-flight downloads to rebind
        /// HttpClient connections to the new interface.
        /// </summary>
        event EventHandler<InterfaceSwitchedEventArgs>? InterfaceSwitched;

        /// <summary>
        /// Returns the last known network type (cached) to avoid repeated blocking detection calls.
        /// </summary>
        NetworkType CurrentNetworkType { get; }

        /// <summary>
        /// Returns whether the last known network was metered.
        /// </summary>
        bool IsCurrentNetworkMetered { get; }
    }

    public class NetworkChangedEventArgs : EventArgs
    {
        public NetworkType NetworkType { get; set; }
        public bool IsMetered { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Event args for a network interface type switch (e.g. WiFi → Ethernet).
    /// Fired when connectivity remains available but the dominant interface changed type.
    /// </summary>
    public class InterfaceSwitchedEventArgs : EventArgs
    {
        public NetworkType PreviousNetworkType { get; set; }
        public NetworkType NewNetworkType { get; set; }
        public bool IsNewNetworkMetered { get; set; }
        public string? Description { get; set; }
    }
}
