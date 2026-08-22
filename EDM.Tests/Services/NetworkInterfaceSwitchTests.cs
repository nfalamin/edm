using System;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class NetworkInterfaceSwitchTests
    {
        private class MockNetworkMonitor : INetworkMonitor
        {
            public event EventHandler<NetworkChangedEventArgs>? NetworkChanged;
            public event EventHandler? ConnectivityLost;
            public event EventHandler? ConnectivityRestored;
            public event EventHandler<InterfaceSwitchedEventArgs>? InterfaceSwitched;

            public NetworkType CurrentNetworkType { get; set; } = NetworkType.WiFi;
            public bool IsCurrentNetworkMetered { get; set; } = false;

            public void SimulateInterfaceSwitch(NetworkType oldType, NetworkType newType, bool isMetered)
            {
                CurrentNetworkType = newType;
                IsCurrentNetworkMetered = isMetered;
                InterfaceSwitched?.Invoke(this, new InterfaceSwitchedEventArgs
                {
                    PreviousNetworkType = oldType,
                    NewNetworkType = newType,
                    IsNewNetworkMetered = isMetered,
                    Description = $"Switched from {oldType} to {newType}"
                });
            }

            public void SimulateConnectivityLost() => ConnectivityLost?.Invoke(this, EventArgs.Empty);
            public void SimulateConnectivityRestored() => ConnectivityRestored?.Invoke(this, EventArgs.Empty);
        }

        [Fact]
        public void DownloadNetworkMonitorAdapter_OnInterfaceSwitched_TriggersHttpClientRebuildAndDiagnostic()
        {
            var mockMonitor = new MockNetworkMonitor();
            string? lastDiagnostic = null;

            using var adapter = new DownloadNetworkMonitorAdapter(mockMonitor, null, diag => lastDiagnostic = diag);

            // Capture current HttpClient instance
            var initialClient = SharedHttpClient.Instance;

            // Trigger interface switch: WiFi -> Ethernet
            mockMonitor.SimulateInterfaceSwitch(NetworkType.WiFi, NetworkType.Ethernet, isMetered: false);

            // Diagnostics should report the switch
            lastDiagnostic.Should().NotBeNull();
            lastDiagnostic.Should().Contain("Interface switched: WiFi → Ethernet");

            // SharedHttpClient instance should be replaced with a new instance
            var newClient = SharedHttpClient.Instance;
            newClient.Should().NotBeSameAs(initialClient, "SharedHttpClient should rebuild client on interface switch");
        }

        [Fact]
        public void SharedHttpClient_RebuildForNetworkChange_ReplacesInstanceSafely()
        {
            var firstClient = SharedHttpClient.Instance;

            SharedHttpClient.RebuildForNetworkChange(NetworkType.Ethernet);

            var secondClient = SharedHttpClient.Instance;
            secondClient.Should().NotBeSameAs(firstClient);

            SharedHttpClient.RebuildForNetworkChange(NetworkType.WiFi);

            var thirdClient = SharedHttpClient.Instance;
            thirdClient.Should().NotBeSameAs(secondClient);
        }
    }
}
