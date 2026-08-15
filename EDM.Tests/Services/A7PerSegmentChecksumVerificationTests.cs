using System;
using System.Security.Cryptography;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A7PerSegmentChecksumVerificationTests
    {
        [Fact]
        public void SegmentRange_Sha256Hash_StoresAndClonesCorrectly()
        {
            var segment = new SegmentRange
            {
                Id = 1,
                Start = 0,
                End = 1048575,
                BytesDownloaded = 1048576,
                State = SegmentState.Completed,
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
            };

            var cloned = segment.Clone();
            cloned.Id.Should().Be(segment.Id);
            cloned.Sha256Hash.Should().Be(segment.Sha256Hash, "Segment SHA-256 hash must be preserved during deep cloning");
        }

        [Fact]
        public void IncrementalHash_ComputesIdenticalDigestAsFullHash()
        {
            byte[] data = new byte[256 * 1024];
            new Random(777).NextBytes(data);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            }

            string incHashResult;
            using (var incHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                // Feed data in 64KB blocks to simulate live stream reads
                int offset = 0;
                int blockSize = 64 * 1024;
                while (offset < data.Length)
                {
                    int len = Math.Min(blockSize, data.Length - offset);
                    incHasher.AppendData(data, offset, len);
                    offset += len;
                }
                var bytes = incHasher.GetHashAndReset();
                incHashResult = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }

            incHashResult.Should().Be(expectedHash, "Live incremental SHA-256 hash must match full stream SHA-256 digest 100%");
        }
    }
}
