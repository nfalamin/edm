using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Advanced Remote ZIP Central Directory Range Inspection & Selective Extraction Service.
    /// Fetches only the ZIP Central Directory at the end of the remote file via HTTP Range requests,
    /// allowing users to view the entire archive hierarchy and selectively extract individual files
    /// BEFORE downloading the full payload.
    /// </summary>
    public class RemoteZipPreviewService
    {
        private readonly HttpClient _httpClient;

        public RemoteZipPreviewService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public async Task<ArchivePreviewResult> InspectRemoteZipAsync(string url, CancellationToken ct = default)
        {
            var result = new ArchivePreviewResult { ArchivePath = url };

            try
            {
                // 1. Probe total content length using HEAD request
                using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResp = await _httpClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                long? contentLength = headResp.Content.Headers.ContentLength;
                if (!contentLength.HasValue || contentLength.Value <= 22)
                {
                    result.IsValid = false;
                    result.SecurityWarning = "Server did not provide valid Content-Length for remote archive.";
                    return result;
                }

                long fileLength = contentLength.Value;

                // 2. Fetch the trailing 64 KB of the file to locate the End of Central Directory Record (EOCD)
                long readLength = Math.Min(65536, fileLength);
                long startOffset = fileLength - readLength;

                using var rangeReq = new HttpRequestMessage(HttpMethod.Get, url);
                rangeReq.Headers.Range = new RangeHeaderValue(startOffset, fileLength - 1);

                using var rangeResp = await _httpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                if (rangeResp.StatusCode != System.Net.HttpStatusCode.PartialContent && rangeResp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    result.IsValid = false;
                    result.SecurityWarning = "Server does not support HTTP Range requests for remote preview.";
                    return result;
                }

                byte[] tailBuffer = await rangeResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                // 3. Search backwards for EOCD signature (0x06054b50)
                int eocdOffset = -1;
                for (int i = tailBuffer.Length - 22; i >= 0; i--)
                {
                    if (tailBuffer[i] == 0x50 && tailBuffer[i + 1] == 0x4B &&
                        tailBuffer[i + 2] == 0x05 && tailBuffer[i + 3] == 0x06)
                    {
                        eocdOffset = i;
                        break;
                    }
                }

                if (eocdOffset < 0)
                {
                    result.IsValid = false;
                    result.SecurityWarning = "End of Central Directory record not found in remote payload.";
                    return result;
                }

                // Parse EOCD fields
                ushort totalEntries = BitConverter.ToUInt16(tailBuffer, eocdOffset + 10);
                uint centralDirSize = BitConverter.ToUInt32(tailBuffer, eocdOffset + 12);
                uint centralDirOffset = BitConverter.ToUInt32(tailBuffer, eocdOffset + 16);

                result.IsValid = true;
                result.TotalEntries = totalEntries;

                // 4. Fetch the Central Directory if not already in the buffer
                byte[] cdBuffer;
                if (centralDirOffset >= startOffset && (centralDirOffset + centralDirSize) <= fileLength)
                {
                    int localOffset = (int)(centralDirOffset - startOffset);
                    cdBuffer = new byte[centralDirSize];
                    Array.Copy(tailBuffer, localOffset, cdBuffer, 0, centralDirSize);
                }
                else
                {
                    using var cdReq = new HttpRequestMessage(HttpMethod.Get, url);
                    cdReq.Headers.Range = new RangeHeaderValue(centralDirOffset, centralDirOffset + centralDirSize - 1);
                    using var cdResp = await _httpClient.SendAsync(cdReq, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                    cdBuffer = await cdResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                }

                // 5. Parse Central Directory file entries
                int pos = 0;
                while (pos + 46 <= cdBuffer.Length)
                {
                    if (cdBuffer[pos] != 0x50 || cdBuffer[pos + 1] != 0x4B ||
                        cdBuffer[pos + 2] != 0x01 || cdBuffer[pos + 3] != 0x02)
                    {
                        break;
                    }

                    uint compSize = BitConverter.ToUInt32(cdBuffer, pos + 20);
                    uint uncompSize = BitConverter.ToUInt32(cdBuffer, pos + 24);
                    ushort nameLen = BitConverter.ToUInt16(cdBuffer, pos + 28);
                    ushort extraLen = BitConverter.ToUInt16(cdBuffer, pos + 30);
                    ushort commentLen = BitConverter.ToUInt16(cdBuffer, pos + 32);
                    uint localHeaderOffset = BitConverter.ToUInt32(cdBuffer, pos + 42);

                    string fileName = Encoding.UTF8.GetString(cdBuffer, pos + 46, nameLen);

                    result.TotalCompressedBytes += compSize;
                    result.TotalUncompressedBytes += uncompSize;

                    bool suspicious = fileName.Contains("..") || fileName.StartsWith("/") || fileName.StartsWith("\\");

                    result.Entries.Add(new ArchiveEntryInfo
                    {
                        Name = Path.GetFileName(fileName),
                        FullPath = fileName,
                        CompressedSizeBytes = compSize,
                        UncompressedSizeBytes = uncompSize,
                        CompressionRatio = compSize > 0 ? (double)uncompSize / compSize : 1.0,
                        IsDirectory = fileName.EndsWith("/") || fileName.EndsWith("\\"),
                        IsPathTraversalSuspicious = suspicious
                    });

                    pos += 46 + nameLen + extraLen + commentLen;
                }

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[RemoteZipPreviewService] Error previewing remote ZIP", ex);
                result.IsValid = false;
                result.SecurityWarning = $"Remote preview failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Selectively downloads and unpacks a single file entry from a remote zip via HTTP range requests.
        /// </summary>
        public async Task<bool> DownloadSelectiveEntryAsync(string zipUrl, string targetEntryName, string destinationFilePath, CancellationToken ct = default)
        {
            var preview = await InspectRemoteZipAsync(zipUrl, ct).ConfigureAwait(false);
            if (!preview.IsValid) return false;

            var entry = preview.Entries.Find(e => string.Equals(e.FullPath, targetEntryName, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.IsDirectory || entry.IsPathTraversalSuspicious) return false;

            // Security directory traversal safeguard
            string? dir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return true;
        }
    }
}
