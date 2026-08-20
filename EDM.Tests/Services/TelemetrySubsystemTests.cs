using System;
using System.IO;
using EDM.Services.Telemetry;
using Xunit;

namespace EDM.Tests.Services
{
    public class TelemetrySubsystemTests
    {
        [Fact]
        public void TelemetrySanitizer_SanitizesUrlAndStripsQueryParameters()
        {
            string rawUrl = "https://downloads.example-cdn.com/releases/setup.exe?token=secret123&signature=abcde456&session=999";
            string sanitized = TelemetrySanitizer.SanitizeUrl(rawUrl);

            Assert.Equal("https://downloads.example-cdn.com/releases/setup.exe", sanitized);
            Assert.DoesNotContain("secret123", sanitized);
            Assert.DoesNotContain("token", sanitized);
        }

        [Fact]
        public void TelemetrySanitizer_SanitizesLocalWindowsUserPaths()
        {
            string rawPath = @"C:\Users\JohnDoe\Downloads\EDM\MyPrivateReport.pdf";
            string sanitized = TelemetrySanitizer.SanitizePath(rawPath);

            Assert.DoesNotContain("JohnDoe", sanitized);
            Assert.Contains("<USER_HOME>", sanitized);
        }

        [Fact]
        public void TelemetrySanitizer_SanitizesStackTraceTokens()
        {
            string rawTrace = "System.Exception: Request failed at Bearer token: 9948abcde123 in C:\\Users\\Administrator\\source\\Engine.cs:line 42";
            string sanitized = TelemetrySanitizer.SanitizeStackTrace(rawTrace);

            Assert.DoesNotContain("9948abcde123", sanitized);
            Assert.DoesNotContain("Administrator", sanitized);
            Assert.Contains("[REDACTED]", sanitized);
        }

        [Fact]
        public void TelemetryQueueService_EnqueueAndDequeue_MaintainsFIFOOrder()
        {
            var queue = new TelemetryQueueService();
            queue.Clear();

            Assert.Equal(0, queue.GetPendingCount());

            queue.Enqueue(new TelemetryEvent { Type = "EVT_1" });
            queue.Enqueue(new TelemetryEvent { Type = "EVT_2" });
            queue.Enqueue(new TelemetryEvent { Type = "EVT_3" });

            Assert.Equal(3, queue.GetPendingCount());

            var batch = queue.DequeueBatch(2);
            Assert.Equal(2, batch.Count);
            Assert.Equal("EVT_1", batch[0].Type);
            Assert.Equal("EVT_2", batch[1].Type);
            Assert.Equal(1, queue.GetPendingCount());

            queue.Clear();
            Assert.Equal(0, queue.GetPendingCount());
        }

        [Fact]
        public void TelemetryTransmissionEngine_ComputeHmac_IsDeterministic()
        {
            string payload = "{\"test\":\"data\"}";
            string secret = "test-secret-key";

            string sig1 = TelemetryTransmissionEngine.ComputeHmacSignature(payload, secret);
            string sig2 = TelemetryTransmissionEngine.ComputeHmacSignature(payload, secret);

            Assert.False(string.IsNullOrWhiteSpace(sig1));
            Assert.Equal(sig1, sig2);
        }

        [Fact]
        public void TelemetryManager_OptOutEnforcement_DropsEventsAndClearsQueue()
        {
            var queue = new TelemetryQueueService();
            var manager = new TelemetryManager(queue);

            manager.SetTelemetryEnabled(true);
            manager.TrackDownloadCompleted("https://cdn.example.org/archive.zip", 1024 * 1024, 2.5, 100.0, 120.0, 8);

            Assert.True(queue.GetPendingCount() >= 1);

            // Opt-out
            manager.SetTelemetryEnabled(false);
            Assert.Equal(0, queue.GetPendingCount());

            // Attempt to track while disabled
            manager.TrackDownloadCompleted("https://cdn.example.org/archive2.zip", 2048, 1.0, 50.0, 60.0, 4);
            Assert.Equal(0, queue.GetPendingCount());

            // Re-enable
            manager.SetTelemetryEnabled(true);
        }
    }
}
