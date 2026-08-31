using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    /// <summary>
    /// Performs final-file verification: size checks, segment consistency checks and SHA-256 verification using streaming.
    /// </summary>
    public class IntegrityVerificationService
    {
        private readonly FileIntegrityService _integrity;
        public IntegrityVerificationService(FileIntegrityService? integrity = null)
        {
            _integrity = integrity ?? new FileIntegrityService();
        }

        public async Task<FileVerificationResult> VerifyFileAsync(string filePath, long expectedSize, string expectedSha256, CancellationToken ct = default)
        {
            var res = await VerifyAsync(filePath, null, expectedSha256, expectedSize, ct).ConfigureAwait(false);
            return new FileVerificationResult
            {
                IsValid = res.State == VerificationState.Verified,
                ActualHash = res.ComputedHashHex ?? string.Empty,
                MismatchReason = res.Message ?? string.Empty
            };
        }

        public async Task<VerificationResult> VerifyAsync(string finalFilePath, DurableDownloadState? metaState = null, string? expectedHashHex = null, long? expectedSize = null, CancellationToken ct = default)
        {
            var result = new VerificationResult
            {
                State = VerificationState.Pending,
                Algorithm = string.IsNullOrEmpty(expectedHashHex) ? null : "SHA-256",
                ExpectedHashHex = expectedHashHex,
                ExpectedSize = expectedSize
            };

            if (!File.Exists(finalFilePath))
            {
                result.State = VerificationState.VerificationFailed;
                result.Message = "Final file does not exist.";
                result.ActualSize = 0;
                return result;
            }

            FileInfo fi = new FileInfo(finalFilePath);
            result.ActualSize = fi.Length;

            // 1) Size checks
            if (expectedSize.HasValue)
            {
                if (result.ActualSize < expectedSize.Value)
                {
                    result.State = VerificationState.VerificationFailed;
                    result.Message = $"File truncated: expected {expectedSize.Value} bytes, actual {result.ActualSize} bytes.";
                    return result;
                }
                if (result.ActualSize > expectedSize.Value)
                {
                    result.State = VerificationState.VerificationFailed;
                    result.Message = $"File has unexpected trailing bytes: expected {expectedSize.Value} bytes, actual {result.ActualSize} bytes.";
                    return result;
                }
            }

            // 2) Segment metadata verification
            if (metaState != null && metaState.Segments != null && metaState.Segments.Count > 0)
            {
                var segResults = new List<SegmentVerificationResult>();
                bool anyFailed = false;
                foreach (var seg in metaState.Segments.OrderBy(s => s.Start))
                {
                    var r = new SegmentVerificationResult
                    {
                        Index = seg.Id,
                        ExpectedStart = seg.Start,
                        ExpectedEnd = seg.End,
                    };

                    try
                    {
                        if (string.IsNullOrEmpty(seg.TempPath) || !File.Exists(seg.TempPath))
                        {
                            r.ActualLength = 0;
                            r.Complete = false;
                            r.Message = "Segment file missing.";
                            anyFailed = true;
                        }
                        else
                        {
                            var pfi = new FileInfo(seg.TempPath);
                            r.ActualLength = pfi.Length;
                            long expectedLen = seg.TotalBytes;
                            r.Complete = r.ActualLength >= expectedLen;

                            if (r.ActualLength < expectedLen)
                            {
                                r.Message = $"Segment truncated: expected {expectedLen}, actual {r.ActualLength}.";
                                anyFailed = true;
                            }
                            else if (seg.Sha256Hash != null)
                            {
                                // per-segment hash available — verify
                                string? actualSegHash = null;
                                try
                                {
                                    using var fs = new FileStream(seg.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    using var sha = SHA256.Create();
                                    var hash = sha.ComputeHash(fs);
                                    actualSegHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                                }
                                catch (Exception ex)
                                {
                                    r.Message = "Failed to compute segment hash: " + ex.Message;
                                    anyFailed = true;
                                }

                                if (actualSegHash != null && !string.Equals(actualSegHash, seg.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                                {
                                    r.Message = $"Segment SHA-256 mismatch. Expected={seg.Sha256Hash}, Actual={actualSegHash}.";
                                    anyFailed = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        r.Message = "Exception while validating segment: " + ex.Message;
                        anyFailed = true;
                    }

                    segResults.Add(r);
                }

                result.SegmentResults = segResults;
                if (anyFailed)
                {
                    result.State = VerificationState.VerificationFailed;
                    result.Message = "One or more segments failed validation.";
                    return result;
                }

                // Verify combined size equals sum of segments if expectedSize not provided
                if (!expectedSize.HasValue)
                {
                    long sum = metaState.Segments.Sum(s => s.BytesDownloaded > 0 ? s.BytesDownloaded : s.TotalBytes);
                    if (sum != result.ActualSize)
                    {
                        result.State = VerificationState.VerificationFailed;
                        result.Message = $"Final file length {result.ActualSize} does not match sum of segment lengths {sum}.";
                        return result;
                    }
                }
            }

            // 3) Trusted checksum verification (SHA-256)
            if (!string.IsNullOrEmpty(expectedHashHex))
            {
                string computed = await _integrity.ComputeSha256Async(finalFilePath, ct).ConfigureAwait(false);
                result.ComputedHashHex = computed;
                if (!string.Equals(computed, expectedHashHex, StringComparison.OrdinalIgnoreCase))
                {
                    result.State = VerificationState.VerificationFailed;
                    result.Message = $"SHA-256 mismatch. Expected={expectedHashHex}, Actual={computed}.";
                    return result;
                }
                result.State = VerificationState.Verified;
                result.Message = "SHA-256 verification passed.";
                return result;
            }

            // 4) No trusted checksum — if expected size verified, mark Verified; otherwise VerificationUnavailable
            try
            {
                string computed = await _integrity.ComputeSha256Async(finalFilePath, ct).ConfigureAwait(false);
                result.ComputedHashHex = computed;
                result.State = expectedSize.HasValue ? VerificationState.Verified : VerificationState.VerificationUnavailable;
                result.Message = expectedSize.HasValue ? "File size verification passed." : "No trusted checksum available; computed SHA-256 stored for reference.";
                return result;
            }
            catch (Exception ex)
            {
                result.State = VerificationState.VerificationUnavailable;
                result.Message = "Hash computation failed: " + ex.Message;
                return result;
            }
        }
    }

    public class FileVerificationResult
    {
        public bool IsValid { get; set; }
        public string ActualHash { get; set; } = string.Empty;
        public string MismatchReason { get; set; } = string.Empty;
    }
}
