using System;

namespace EDM.Models
{
    /// <summary>
    /// The kind of proxy protocol to use for outgoing download connections.
    /// </summary>
    public enum ProxyType
    {
        None = 0,
        Http = 1,
        Https = 2,
        Socks5 = 3
    }

    /// <summary>
    /// User-configurable proxy settings, persisted as part of AppSettings.
    /// Passwords are stored encrypted at rest (Windows DPAPI) via ProxyService,
    /// never written to disk in plain text.
    /// </summary>
    public class ProxySettings
    {
        public bool Enabled { get; set; } = false;
        public ProxyType Type { get; set; } = ProxyType.Http;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 8080;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// DPAPI-encrypted (base64) password. Use ProxyService.Encrypt/Decrypt to read or write this.
        /// Never set this to a plain-text password directly.
        /// </summary>
        public string EncryptedPassword { get; set; } = string.Empty;

        /// <summary>When true, requests to localhost/127.0.0.1/private LAN ranges bypass the proxy.</summary>
        public bool BypassLocalAddresses { get; set; } = true;

        /// <summary>Comma-separated list of additional host patterns that should bypass the proxy (e.g. "*.internal.com").</summary>
        public string BypassList { get; set; } = string.Empty;

        public bool HasCredentials => !string.IsNullOrWhiteSpace(Username);

        public override string ToString()
        {
            if (!Enabled) return "Proxy: disabled";
            return $"Proxy: {Type} {Host}:{Port}{(HasCredentials ? " (authenticated)" : string.Empty)}";
        }
    }
}
