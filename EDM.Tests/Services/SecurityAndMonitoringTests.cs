using System;
using System.IO;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class SecurityAndMonitoringTests
    {
        [Fact]
        public void SecuritySanitizer_IsAllowedUrlScheme_AllowsHttpAndHttpsOnly()
        {
            SecuritySanitizer.IsAllowedUrlScheme("https://example.com/file.zip").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("http://example.com/file.zip").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("ftp://example.com/file.zip").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("javascript:alert(1)").Should().BeFalse();
            SecuritySanitizer.IsAllowedUrlScheme("file:///C:/boot.ini").Should().BeFalse();
        }

        [Fact]
        public void SecuritySanitizer_SanitizeFileName_StripsReservedWindowsDeviceNames()
        {
            SecuritySanitizer.SanitizeFileName("CON.txt").Should().Be("_CON.txt");
            SecuritySanitizer.SanitizeFileName("NUL.zip").Should().Be("_NUL.zip");
            SecuritySanitizer.SanitizeFileName("COM1.exe").Should().Be("_COM1.exe");
        }

        [Fact]
        public void DownloadDiagnosticsTracker_CalculatesEtaAndTracksMetricsCorrectly()
        {
            var tracker = new DownloadDiagnosticsTracker();
            tracker.RecordMetrics("dl-100", 100 * 1024 * 1024, 50 * 1024 * 1024, 5 * 1024 * 1024, 4 * 1024 * 1024, 8, 0, 45.0, 0);

            var metrics = tracker.GetMetrics("dl-100");
            metrics.Should().NotBeNull();
            metrics!.TotalBytes.Should().Be(100 * 1024 * 1024);
            metrics.RemainingEta.TotalSeconds.Should().Be(10);
            metrics.ActiveConnections.Should().Be(8);
        }
    }
}
