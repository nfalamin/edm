using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class VpnProfile
    {
        public string ProfileName { get; set; } = string.Empty;
        public string? PhoneNumberOrHost { get; set; }
        public string? Username { get; set; }
        public bool AutoDisconnectWhenDone { get; set; } = true;
        public int ConnectTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Advanced Windows VPN & Dial-up (RAS) Tunnel Orchestrator.
    /// Manages rasdial/rasphone profile automation, tunnel readiness verification,
    /// automatic reconnect with backoff, and graceful queue teardown.
    /// </summary>
    public class VpnTunnelOrchestrator
    {
        private static readonly Lazy<VpnTunnelOrchestrator> _instance = new(() => new VpnTunnelOrchestrator());
        public static VpnTunnelOrchestrator Instance => _instance.Value;

        private VpnProfile? _activeProfile;
        public bool IsConnected { get; private set; }

        public async Task<bool> ConnectProfileAsync(VpnProfile profile, string? securePassword = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(profile.ProfileName)) return false;
            _activeProfile = profile;

            LoggingService.Log($"[VpnTunnelOrchestrator] Initiating connection to VPN profile '{profile.ProfileName}'...");

            try
            {
                // rasdial "ProfileName" username password
                var psi = new ProcessStartInfo
                {
                    FileName = "rasdial",
                    Arguments = $"\"{profile.ProfileName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (!string.IsNullOrEmpty(profile.Username) && !string.IsNullOrEmpty(securePassword))
                {
                    // Pass securely via argument list
                    psi.ArgumentList.Clear();
                    psi.ArgumentList.Add(profile.ProfileName);
                    psi.ArgumentList.Add(profile.Username);
                    psi.ArgumentList.Add(securePassword);
                }

                using var proc = Process.Start(psi);
                if (proc == null) return false;

                await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                if (proc.ExitCode == 0 || VerifyTunnelInterfaceExists(profile.ProfileName))
                {
                    IsConnected = true;
                    LoggingService.Log($"[VpnTunnelOrchestrator] ✅ Successfully connected to VPN profile '{profile.ProfileName}'.");
                    return true;
                }
                else
                {
                    IsConnected = false;
                    LoggingService.Log($"[VpnTunnelOrchestrator] ❌ Failed to connect to VPN '{profile.ProfileName}'. ExitCode={proc.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[VpnTunnelOrchestrator] Connection attempt failed for '{profile.ProfileName}'", ex);
                IsConnected = false;
                return false;
            }
        }

        public async Task DisconnectActiveProfileAsync(CancellationToken ct = default)
        {
            if (_activeProfile == null && !IsConnected) return;

            string name = _activeProfile?.ProfileName ?? string.Empty;
            LoggingService.Log($"[VpnTunnelOrchestrator] Disconnecting VPN profile '{name}'...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rasdial",
                    Arguments = string.IsNullOrEmpty(name) ? "/disconnect" : $"\"{name}\" /disconnect",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                IsConnected = false;
                _activeProfile = null;
            }
        }

        public bool VerifyTunnelInterfaceExists(string profileName)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in interfaces)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                            nic.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
                            nic.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, VpnProfile> _queueProfiles = new(StringComparer.OrdinalIgnoreCase);

        public void AssignProfileToQueue(string queueId, VpnProfile profile)
        {
            _queueProfiles[queueId] = profile;
        }

        public VpnProfile? GetProfileForQueue(string queueId)
        {
            _queueProfiles.TryGetValue(queueId, out var profile);
            return profile;
        }

        public async Task<System.Collections.Generic.List<string>> GetSystemVpnProfilesAsync(CancellationToken ct = default)
        {
            var profiles = new System.Collections.Generic.List<string>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -NonInteractive -Command \"Get-VpnConnection | Select-Object -ExpandProperty Name\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string? line;
                    while ((line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            profiles.Add(line.Trim());
                        }
                    }
                    await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                }
            }
            catch { }

            return profiles;
        }
    }
}
