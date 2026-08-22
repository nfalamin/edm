using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Domain.Protocols;
using Xunit;

namespace EDM.Tests.Services
{
    public sealed class TestPauseToken : IPauseToken
    {
        private readonly ManualResetEventSlim _event = new(true);
        public bool IsPaused => !_event.IsSet;

        public void Pause() => _event.Reset();
        public void Resume() => _event.Set();

        public Task WaitWhilePausedAsync(CancellationToken cancellationToken)
        {
            _event.Wait(cancellationToken);
            return Task.CompletedTask;
        }
    }

    public sealed class DomainProtocolsEngineTests
    {
        [Fact]
        public void AdaptiveThroughputGovernor_CalculatesMetricsAndSmoothsSpeed()
        {
            // Arrange
            var governor = new AdaptiveThroughputGovernor(initialConnections: 8, minConnections: 2, maxConnections: 32);

            // Act
            governor.RecordBytes(10 * 1024 * 1024); // 10 MB
            Thread.Sleep(60); // Allow timestamp tick delta

            var report = governor.SampleMetrics(totalContentLength: 50 * 1024 * 1024, currentActiveConnections: 8, canResume: true, status: "Active");

            // Assert
            Assert.Equal(10 * 1024 * 1024, report.BytesReceived);
            Assert.Equal(50 * 1024 * 1024, report.TotalBytes);
            Assert.True(report.CurrentSpeedBytesPerSec > 0, "Current speed should be positive");
            Assert.True(report.AverageSpeedBytesPerSec > 0, "Average EMA speed should be positive");
            Assert.Equal(20.0, report.ProgressPercentage, 1);
            Assert.True(report.CanResume);
        }

        [Fact]
        public void HttpMultiPartEngine_CanHandle_ValidatesHttpAndHttpsSchemes()
        {
            // Arrange
            var engine = new HttpMultiPartEngine();

            // Act & Assert
            Assert.True(engine.CanHandle("http://example.com/file.zip"));
            Assert.True(engine.CanHandle("https://releases.ubuntu.com/jammy.iso"));
            Assert.False(engine.CanHandle("ftp://speedtest.tele2.net/file.bin"));
            Assert.False(engine.CanHandle("magnet:?xt=urn:btih:abcdef"));
            Assert.False(engine.CanHandle(""));
        }

        [Fact]
        public void TestPauseToken_BlocksAndResumesCorrectly()
        {
            // Arrange
            var token = new TestPauseToken();
            Assert.False(token.IsPaused);

            // Act
            token.Pause();
            Assert.True(token.IsPaused);

            token.Resume();
            Assert.False(token.IsPaused);
        }
    }
}
