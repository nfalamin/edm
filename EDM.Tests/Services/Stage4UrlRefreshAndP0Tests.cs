using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4UrlRefreshAndP0Tests : TestBase
    {
        [Theory]
        [InlineData(HttpStatusCode.Forbidden, "Access Denied")]
        [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
        [InlineData(HttpStatusCode.Gone, "Resource expired")]
        [InlineData(HttpStatusCode.BadRequest, "<Error><Code>Request has expired</Code></Error>")]
        [InlineData(HttpStatusCode.BadRequest, "<Error><Code>SignatureDoesNotMatch</Code></Error>")]
        public void UrlRefreshOrchestrator_DetectsExpiredUrlsCorrectly(HttpStatusCode code, string body)
        {
            var orchestrator = UrlRefreshOrchestrator.Instance;
            bool isExpired = orchestrator.IsUrlExpired(code, body);
            isExpired.Should().BeTrue();
        }

        [Fact]
        public void VpnTunnelOrchestrator_MapsPerQueueProfilesProperly()
        {
            var vpn = VpnTunnelOrchestrator.Instance;
            var profile = new VpnProfile { ProfileName = "Corporate VPN", Username = "worker_1" };

            vpn.AssignProfileToQueue("night_queue", profile);
            var retrieved = vpn.GetProfileForQueue("night_queue");

            retrieved.Should().NotBeNull();
            retrieved!.ProfileName.Should().Be("Corporate VPN");
        }

        [Fact]
        public void PowerActionScheduler_CancelsScheduledCountdownGracefully()
        {
            var scheduler = PowerActionScheduler.Instance;
            scheduler.CancelCountdown();
            scheduler.IsCountdownActive.Should().BeFalse();
        }
    }
}
