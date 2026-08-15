using System;
using System.Collections.Generic;
using System.Net;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A6MultiNicBondingTests
    {
        [Fact]
        public void MultiNicManager_EnumeratesActiveLocalIPv4Addresses()
        {
            var manager = MultiNicManager.Instance;
            var ips = manager.RefreshActiveInterfaces();

            ips.Should().NotBeNull();
            foreach (var ip in ips)
            {
                ip.AddressFamily.Should().Be(System.Net.Sockets.AddressFamily.InterNetwork);
                string ipStr = ip.ToString();
                ipStr.Should().NotStartWith("127.", "Loopback IP 127.0.0.1 must be excluded from multi-NIC bonding");
                ipStr.Should().NotStartWith("169.254.", "APIPA self-assigned IP 169.254.* must be excluded");
            }
        }

        [Fact]
        public void MultiNicManager_RoundRobin_CyclesAvailableIPs()
        {
            var manager = MultiNicManager.Instance;
            var activeIPs = manager.GetActiveLocalIPs();

            if (activeIPs.Count > 1)
            {
                var first = manager.GetNextLocalIPAddress();
                var second = manager.GetNextLocalIPAddress();

                first.Should().NotBeNull();
                second.Should().NotBeNull();
                first.Should().NotBe(second, "Round robin should select different physical network adapters when multiple NICs are active");
            }
            else
            {
                var ip = manager.GetNextLocalIPAddress();
                ip.Should().BeNull("Single interface system should default to OS routing table");
            }
        }
    }
}
