using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Helpers;

namespace EDM.Services
{
    public class HttpProbeResult
    {
        public Uri RequestUri { get; set; } = null!;
        public Uri? FinalUri { get; set; }
        public string SavePath { get; set; } = string.Empty;
        public long? TotalBytes { get; set; }
        public bool ServerSupportsResume { get; set; }
        public ContentDispositionHeaderValue? ContentDisposition { get; set; }
        public string? ContentType { get; set; }
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string InferredFileName => System.IO.Path.GetFileName(SavePath);
    }

    /// <summary>
    /// Handles HTTP URI validation, header probing, filename resolution,
    /// disk space validation, and robust 206 Partial Content range probing.
    /// </summary>
    public class HttpProbeService
    {
        private readonly HttpClient _httpClient;

        public HttpProbeService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public async Task<HttpProbeResult> ProbeUrlAsync(
            string url,
            string savePath,
            DownloadCredentials? credentials = null,
            string? cookies = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Download URL is empty.", nameof(url));

            if (DownloadService.IsVideoStreamingUrl(url))
            {
                throw new InvalidOperationException($"'{url}' is a streaming media page and cannot be probed as a direct file. Use media resolution engine.");
            }

            if (!FileNamingHelper.TryCreateHttpUri(url.Trim(), out var requestUri) || requestUri == null)
            {
                LoggingService.Log($"[HttpProbeService] Invalid request URI: '{url}'");
                throw new ArgumentException($"Invalid request URI: '{url}'", nameof(url));
            }

            url = requestUri.ToString();

            if (string.IsNullOrWhiteSpace(savePath))
            {
                savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", Path.GetFileName(requestUri.LocalPath));
            }

            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            long? totalBytes = null;
            ContentDispositionHeaderValue? capturedCd = null;
            string? capturedMime = null;
            string? capturedETag = null;
            DateTimeOffset? capturedLastModified = null;
            Uri? finalUri = null;

            // Step 1: Initial GET/HEAD probe with retry for transient server failures
            HttpResponseMessage? response = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var probeReq = new HttpRequestMessage(HttpMethod.Get, requestUri);
                if (credentials != null && !credentials.IsEmpty)
                {
                    probeReq.Headers.Authorization = credentials.ToBasicAuthHeader();
                }
                if (!string.IsNullOrWhiteSpace(cookies))
                {
                    probeReq.Headers.TryAddWithoutValidation("Cookie", cookies);
                }

                try
                {
                    response = await _httpClient.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    int code = (int)response.StatusCode;
                    if (code == 500 || code == 502 || code == 503 || code == 504 || code == 408 || code == 429)
                    {
                        if (attempt < 3)
                        {
                            response.Dispose();
                            response = null;
                            await Task.Delay(50 * (1 << attempt), cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                    }

                    response.EnsureSuccessStatusCode();
                    break;
                }
                catch (HttpRequestException) when (attempt < 3)
                {
                    response?.Dispose();
                    response = null;
                    await Task.Delay(50 * (1 << attempt), cancellationToken).ConfigureAwait(false);
                }
            }

            if (response == null)
            {
                throw new HttpRequestException("Failed to probe URL after retry attempts.");
            }

            using (response)
            {
                finalUri = response.RequestMessage?.RequestUri ?? requestUri;
                totalBytes = response.Content.Headers.ContentLength;
                try { capturedCd = response.Content.Headers.ContentDisposition; } catch { }
                try { capturedMime = response.Content.Headers.ContentType?.MediaType; } catch { }
                try { capturedETag = response.Headers.ETag?.Tag; } catch { }
                try { capturedLastModified = response.Content.Headers.LastModified; } catch { }
            }

            // Step 2: Robust 206 Partial Content Range Confirmation Probe
            // Send explicit Range: bytes=0-1 request and confirm HttpStatusCode.PartialContent (206)
            bool serverSupportsResume = false;
            try
            {
                using var rangeReq = new HttpRequestMessage(HttpMethod.Get, requestUri);
                rangeReq.Headers.Range = new RangeHeaderValue(0, 1);
                if (credentials != null && !credentials.IsEmpty)
                {
                    rangeReq.Headers.Authorization = credentials.ToBasicAuthHeader();
                }
                if (!string.IsNullOrWhiteSpace(cookies))
                {
                    rangeReq.Headers.TryAddWithoutValidation("Cookie", cookies);
                }

                using var rangeResp = await _httpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                serverSupportsResume = (rangeResp.StatusCode == HttpStatusCode.PartialContent);
                LoggingService.Log($"[HttpProbeService] Robust Range probe (Range: bytes=0-1) => StatusCode: {rangeResp.StatusCode}, Confirmed 206 Resume: {serverSupportsResume}");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[HttpProbeService] Range probe failed: {ex.Message}");
                serverSupportsResume = false;
            }

            // Step 3: Disk space validation
            try
            {
                string rootPath = Path.GetPathRoot(Path.GetFullPath(savePath)) ?? "";
                if (!string.IsNullOrEmpty(rootPath))
                {
                    var drive = new DriveInfo(rootPath);
                    if (totalBytes.HasValue && totalBytes.Value > 0 && drive.AvailableFreeSpace < totalBytes.Value)
                    {
                        throw new IOException($"Insufficient disk space on {rootPath}. Required: {totalBytes.Value} bytes, Available: {drive.AvailableFreeSpace} bytes.");
                    }
                }
            }
            catch (IOException) { throw; }
            catch (Exception ex)
            {
                LoggingService.Log($"[HttpProbeService] Disk space check warning: {ex.Message}");
            }

            // Step 4: Filename resolution from Content-Disposition or Content-Type headers
            try
            {
                var dir = directory;
                var providedName = Path.GetFileName(savePath ?? string.Empty);
                bool savePathLooksLikeDirectory = string.IsNullOrEmpty(providedName) || savePath!.EndsWith(Path.DirectorySeparatorChar) || savePath.EndsWith(Path.AltDirectorySeparatorChar);
                bool hasExt = Path.HasExtension(providedName);

                if (savePathLooksLikeDirectory || !hasExt)
                {
                    var inferred = FileNamingHelper.DetermineFileNameFromHeaders(capturedCd, capturedMime, finalUri ?? requestUri);

                    if (savePathLooksLikeDirectory)
                    {
                        savePath = Path.Combine(dir ?? ".", inferred);
                    }
                    else if (!hasExt)
                    {
                        var baseName = Path.GetFileNameWithoutExtension(providedName);
                        savePath = Path.Combine(dir ?? ".", baseName + Path.GetExtension(inferred));
                    }

                    var newDir = Path.GetDirectoryName(savePath) ?? dir;
                    if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir)) Directory.CreateDirectory(newDir);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[HttpProbeService] Filename resolution warning: {ex.Message}");
            }

            return new HttpProbeResult
            {
                RequestUri = requestUri,
                FinalUri = finalUri,
                SavePath = savePath ?? string.Empty,
                TotalBytes = totalBytes,
                ServerSupportsResume = serverSupportsResume,
                ContentDisposition = capturedCd,
                ContentType = capturedMime,
                ETag = capturedETag,
                LastModified = capturedLastModified
            };
        }
    }
}
