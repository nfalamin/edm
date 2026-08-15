using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.E2E
{
    [Trait("Category", "RealE2E")]
    public class MediaVariantE2ETests : IAsyncLifetime
    {
        private LocalHttpTestServer _server = null!;

        public async Task InitializeAsync()
        {
            _server = new LocalHttpTestServer();
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _server.DisposeAsync();
        }

        [Fact]
        public async Task MediaVariantResolver_HlsStream_ParsesResolutionsCorrectly()
        {
            string m3u8Url = $"{_server.BaseUrl}media.m3u8";
            var resolver = new MediaVariantResolver();

            var result = await resolver.ResolveVariantsAsync(m3u8Url, cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue();
            result.Variants.Should().NotBeEmpty();

            // Should have 1080p, 720p, 480p variants
            result.Variants.Any(v => v.Height == 1080 || v.QualityLabel.Contains("1080")).Should().BeTrue();
            result.Variants.Any(v => v.Height == 720 || v.QualityLabel.Contains("720")).Should().BeTrue();
        }

        [Fact]
        public async Task MediaVariantResolver_DirectStream_ReturnsDirectOption()
        {
            string mp4Url = $"{_server.BaseUrl}1mb.bin";
            var resolver = new MediaVariantResolver();

            var result = await resolver.ResolveVariantsAsync(mp4Url, cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue();
            result.Variants.Should().NotBeEmpty();
            result.Variants[0].DirectUrl.Should().Be(mp4Url);
        }
    }
}
