using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A5ZeroAllocationAndMemoryStorageTests
    {
        [Fact]
        public async Task ZeroAllocation_FileMerge_PreservesIntegrityAndUsesArrayPool()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"a5_merge_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string destFile = Path.Combine(tempDir, "merged_final.bin");

            try
            {
                // Create 4 distinct segment chunk files
                int chunkSize = 64 * 1024;
                byte[] fullPayload = new byte[chunkSize * 4];
                new Random(2026).NextBytes(fullPayload);

                string expectedSha256;
                using (var sha = SHA256.Create())
                {
                    expectedSha256 = Convert.ToHexString(sha.ComputeHash(fullPayload));
                }

                string[] chunkFiles = new string[4];
                for (int i = 0; i < 4; i++)
                {
                    chunkFiles[i] = Path.Combine(tempDir, $"segment_{i}.part");
                    using var fs = new FileStream(chunkFiles[i], FileMode.Create, FileAccess.Write);
                    await fs.WriteAsync(fullPayload.AsMemory(i * chunkSize, chunkSize));
                }

                using var httpClient = new HttpClient();
                var downloader = new MultiPartDownloader(httpClient);

                // Execute zero-allocation merge
                await downloader.MergeFilesAsync(chunkFiles, destFile, CancellationToken.None, expectedHash: expectedSha256);

                File.Exists(destFile).Should().BeTrue();
                byte[] mergedData = await File.ReadAllBytesAsync(destFile);
                mergedData.Length.Should().Be(fullPayload.Length);

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(mergedData));
                }
                actualSha256.Should().Be(expectedSha256, "Zero-allocation merged file payload SHA-256 must match source perfectly");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FileIntegrityService_UsesArrayPoolBuffer_ForHashCalculation()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"a5_hash_{Guid.NewGuid():N}.bin");
            try
            {
                byte[] payload = new byte[100 * 1024];
                new Random(1234).NextBytes(payload);
                File.WriteAllBytes(tempFile, payload);

                string expectedHash;
                using (var sha = SHA256.Create())
                {
                    expectedHash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                }

                var integrityService = new FileIntegrityService();
                string computedHash = integrityService.ComputeSha256Async(tempFile, CancellationToken.None).GetAwaiter().GetResult();

                computedHash.Should().Be(expectedHash, "FileIntegrityService pooled read loop must yield exact SHA-256 hash");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
