using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace EDM.ControlPlane.Api.Services
{
    public interface IPrivacySafeDeviceService
    {
        Guid GenerateInstallationId();
        string AnonymizeIpAddress(string? ipAddress);
    }

    /// <summary>
    /// Privacy-Safe Device Identity Service.
    /// Ensures zero collection of hardware MAC addresses or invasive fingerprinting.
    /// Provides coarse IP truncation (e.g. /24 subnet for IPv4 and /48 prefix for IPv6).
    /// </summary>
    public class PrivacySafeDeviceService : IPrivacySafeDeviceService
    {
        public Guid GenerateInstallationId()
        {
            byte[] randomBytes = new byte[16];
            RandomNumberGenerator.Fill(randomBytes);
            return new Guid(randomBytes);
        }

        public string AnonymizeIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return "0.0.0.0";

            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Mask last octet for IPv4 (e.g. 192.168.1.100 -> 192.168.1.0)
                    byte[] bytes = ip.GetAddressBytes();
                    bytes[3] = 0;
                    return new IPAddress(bytes).ToString();
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // Mask last 80 bits for IPv6 (retain /48 prefix)
                    byte[] bytes = ip.GetAddressBytes();
                    for (int i = 6; i < 16; i++) bytes[i] = 0;
                    return new IPAddress(bytes).ToString();
                }
            }

            return "0.0.0.0";
        }
    }
}
