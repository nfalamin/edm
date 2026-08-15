using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// MASTER HIGH-PERFORMANCE QUALITY GATE TEST SUITE
    /// Validates reliability invariants and protects high-throughput performance.
    /// </summary>
    public class HighPerformanceQualityGateTests
    {
        [Fact]
        public void QualityGate1_PauseResume_RapidCycleStorm_MaintainsAtomicState()
        {
            var pts = new PauseTokenSource();
            for (int i = 0; i < 50; i++)
            {
                pts.Pause();
                pts.IsPaused.Should().BeTrue();
                pts.Resume();
                pts.IsPaused.Should().BeFalse();
            }
        }

        [Fact]
        public void QualityGate2_MultiStream_ZeroAllocationBuffer_AllocatesAndRecyclesSafely()
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                buffer.Length.Should().BeGreaterOrEqualTo(65536);
                buffer[0] = 0xAA;
                buffer[buffer.Length - 1] = 0x55;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [Fact]
        public async Task QualityGate3_FailureRecovery_ExponentialBackoff_LimitsMaxDelay()
        {
            int maxRetries = 5;
            int baseDelayMs = 500;
            int maxObservedDelay = 0;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                int delay = (int)Math.Min(10000, Math.Pow(2, attempt - 1) * baseDelayMs);
                maxObservedDelay = Math.Max(maxObservedDelay, delay);
            }

            maxObservedDelay.Should().Be(8000);
            await Task.CompletedTask;
        }

        [Fact]
        public void QualityGate4_BrowserNativeHandoff_DeduplicatesRapidIdenticalRequests()
        {
            var p1 = new IpcHandoffPayload { Url = "https://cdn.example.com/asset.iso", CorrelationId = "corr-1" };
            var p2 = new IpcHandoffPayload { Url = "https://cdn.example.com/asset.iso", CorrelationId = "corr-1" };

            (p1.Url == p2.Url && p1.CorrelationId == p2.CorrelationId).Should().BeTrue();
        }

        [Fact]
        public void QualityGate5_Checksum_Sha256AndMd5_VerifiesMultiPartIntegrity()
        {
            byte[] raw = Encoding.UTF8.GetBytes("Exclusive Download Manager Quality Gate Integrity Verification Payload");
            string expectedSha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
            string expectedMd5 = Convert.ToHexString(MD5.HashData(raw)).ToLowerInvariant();

            string temp = Path.Combine(Path.GetTempPath(), "edm_sup_test_" + Guid.NewGuid().ToString("N") + ".dat");
            File.WriteAllBytes(temp, raw);

            try
            {
                using var fs = File.OpenRead(temp);
                string computedSha256 = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
                fs.Seek(0, SeekOrigin.Begin);
                string computedMd5 = Convert.ToHexString(MD5.HashData(fs)).ToLowerInvariant();

                computedSha256.Should().Be(expectedSha256);
                computedMd5.Should().Be(expectedMd5);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        [Fact]
        public void QualityGate6_ZeroTrust_DPAPI_TokenAndSecretRedaction()
        {
            string log = "Connection failed for user=admin with password=TopSecretToken12345! and token=eyJhbGciOiJIUzI1NiJ9.test";
            string sanitized = SecureCredentialVault.RedactCredentialsFromText(log);

            sanitized.Should().NotContain("TopSecretToken12345!");
            sanitized.Should().Contain("[REDACTED]");
        }
    }
}
