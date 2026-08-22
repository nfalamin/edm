using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum HashAlgorithmType
    {
        Sha256,
        Sha1,
        Md5,
        Crc32
    }

    public class IntegrityVerificationResult
    {
        public bool IsValid { get; set; }
        public DownloadIntegrityStatus Status { get; set; }
        public string Algorithm { get; set; } = string.Empty;
        public string ExpectedHash { get; set; } = string.Empty;
        public string ActualHash { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// FileIntegrityService — Comprehensive file & segment integrity verification system.
    /// Computes high-performance streaming checksums (SHA-256, SHA-1, MD5) and isolates segment corruptions.
    /// </summary>
    public class FileIntegrityService
    {
        private static readonly Lazy<FileIntegrityService> _instance = new(() => new FileIntegrityService());
        public static FileIntegrityService Instance => _instance.Value;

        /// <summary>
        /// Computes streaming hash of a file with pooled memory buffers.
        /// </summary>
        public async Task<string> ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm, CancellationToken ct = default)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found for hash calculation", filePath);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using HashAlgorithm hasher = algorithm switch
            {
                HashAlgorithmType.Sha256 => SHA256.Create(),
                HashAlgorithmType.Sha1 => SHA1.Create(),
                HashAlgorithmType.Md5 => MD5.Create(),
                _ => SHA256.Create()
            };

            byte[] buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
            try
            {
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var hashBytes = hasher.Hash ?? Array.Empty<byte>();
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Computes SHA-256 hash of a file with streaming pooled memory buffers.
        /// </summary>
        public Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
        {
            return ComputeFileHashAsync(filePath, HashAlgorithmType.Sha256, ct);
        }

        /// <summary>
        /// Verifies a file against an expected checksum.
        /// </summary>
        public async Task<IntegrityVerificationResult> VerifyFileIntegrityAsync(string filePath, string expectedChecksum, HashAlgorithmType algorithm = HashAlgorithmType.Sha256, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return new IntegrityVerificationResult
                {
                    IsValid = true,
                    Status = DownloadIntegrityStatus.VerificationUnavailable,
                    ErrorMessage = "No authoritative checksum was provided for this download."
                };
            }

            try
            {
                string cleanExpected = expectedChecksum.Trim().ToLowerInvariant();
                string actualHash = await ComputeFileHashAsync(filePath, algorithm, ct).ConfigureAwait(false);

                bool isMatch = string.Equals(cleanExpected, actualHash, StringComparison.OrdinalIgnoreCase);

                return new IntegrityVerificationResult
                {
                    IsValid = isMatch,
                    Status = isMatch ? DownloadIntegrityStatus.Verified : DownloadIntegrityStatus.VerificationFailed,
                    Algorithm = algorithm.ToString().ToUpperInvariant(),
                    ExpectedHash = cleanExpected,
                    ActualHash = actualHash,
                    ErrorMessage = isMatch ? string.Empty : $"Hash mismatch: expected '{cleanExpected}', calculated '{actualHash}'."
                };
            }
            catch (Exception ex)
            {
                return new IntegrityVerificationResult
                {
                    IsValid = false,
                    Status = DownloadIntegrityStatus.VerificationFailed,
                    Algorithm = algorithm.ToString().ToUpperInvariant(),
                    ExpectedHash = expectedChecksum,
                    ErrorMessage = $"Verification error: {ex.Message}"
                };
            }
        }
    }
}
