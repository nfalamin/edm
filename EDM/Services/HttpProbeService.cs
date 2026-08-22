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

        public Task<HttpProbeResult> ProbeUrlAsync(
            string url,
            string savePath,
            DownloadCredentials? credentials,
            string? cookies,
            CancellationToken cancellationToken)
        {
            return ProbeUrlAsync(url, savePath, credentials, cookies, null, null, null, cancellationToken);
        }

        public async Task<HttpProbeResult> ProbeUrlAsync(
            string url,
            string savePath,
            DownloadCredentials? credentials = null,
            string? cookies = null,
            string? userAgent = null,
            string? referer = null,
            string? authHeader = null,
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
            try
            {
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[HttpProbeService] Directory creation check failed: {ex.Message}");
            }

            long? totalBytes = null;
            ContentDispositionHeaderValue? capturedCd = null;
            string? capturedMime = null;
            string? capturedETag = null;
            DateTimeOffset? capturedLastModified = null;
            Uri? finalUri = null;

            void ApplyProbeHeaders(HttpRequestMessage req)
            {
                if (!string.IsNullOrWhiteSpace(userAgent))
                {
                    string cleanUa = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(userAgent);
                    req.Headers.Remove("User-Agent");
                    req.Headers.TryAddWithoutValidation("User-Agent", cleanUa);
                }
                if (!string.IsNullOrWhiteSpace(referer))
                {
                    string cleanRef = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(referer);
                    if (Uri.TryCreate(cleanRef, UriKind.Absolute, out var refUri))
                    {
                        req.Headers.Referrer = refUri;
                    }
                }
                if (credentials != null && !credentials.IsEmpty)
                {
                    req.Headers.Authorization = credentials.ToBasicAuthHeader();
                }
                else if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    string cleanAuth = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(authHeader);
                    var parts = cleanAuth.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        req.Headers.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
                    }
                    else
                    {
                        req.Headers.TryAddWithoutValidation("Authorization", cleanAuth);
                    }
                }
                if (!string.IsNullOrWhiteSpace(cookies))
                {
                    string cleanCookies = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(cookies);
                    if (cleanCookies.Length > 16384) cleanCookies = cleanCookies.Substring(0, 16384);
                    req.Headers.TryAddWithoutValidation("Cookie", cleanCookies);
                }
            }

            // Step 1: High-Speed Unified Range Probe (Range: bytes=0-1 in 1 single roundtrip)
            HttpResponseMessage? response = null;
            bool serverSupportsResume = false;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var probeReq = new HttpRequestMessage(HttpMethod.Get, requestUri);
                probeReq.Headers.Range = new RangeHeaderValue(0, 1);
                ApplyProbeHeaders(probeReq);

                try
                {
                    response = await _httpClient.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        response.Dispose();
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.AuthenticationRequired,
                            $"Authentication required (401) during probe for '{requestUri.Host}'.",
                            HttpStatusCode.Unauthorized,
                            requestUri);
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        response.Dispose();
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.Forbidden,
                            $"Access forbidden (403) during probe for '{requestUri.Host}'.",
                            HttpStatusCode.Forbidden,
                            requestUri);
                    }

                    // Server responded with 206 Partial Content (Supports Multi-Segment Download)
                    if (response.StatusCode == HttpStatusCode.PartialContent)
                    {
                        serverSupportsResume = true;
                        break;
                    }

                    // Server responded with 200 OK (Doesn't support Range, but valid response)
                    if (response.IsSuccessStatusCode)
                    {
                        serverSupportsResume = false;
                        break;
                    }

                    int code = (int)response.StatusCode;
                    if (code == 500 || code == 502 || code == 503 || code == 504 || code == 408 || code == 429)
                    {
                        if (attempt < 2)
                        {
                            response.Dispose();
                            response = null;
                            await Task.Delay(30 * (1 << attempt), cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                    }

                    response.EnsureSuccessStatusCode();
                    break;
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    response?.Dispose();
                    response = null;
                    await Task.Delay(30 * (1 << attempt), cancellationToken).ConfigureAwait(false);
                }
            }

            if (response == null)
            {
                throw new HttpRequestException("Failed to probe URL after fast retry attempts.");
            }

            using (response)
            {
                finalUri = response.RequestMessage?.RequestUri ?? requestUri;
                
                if (serverSupportsResume && response.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    totalBytes = response.Content.Headers.ContentRange.Length.Value;
                }
                else
                {
                    totalBytes = response.Content.Headers.ContentLength;
                }

                try { capturedCd = response.Content.Headers.ContentDisposition; } catch { }
                try { capturedMime = response.Content.Headers.ContentType?.MediaType; } catch { }
                try { capturedETag = response.Headers.ETag?.Tag; } catch { }
                try { capturedLastModified = response.Content.Headers.LastModified; } catch { }

                LoggingService.Log($"[HttpProbeService] Fast Unified Probe => StatusCode: {response.StatusCode}, RangeResume: {serverSupportsResume}, TotalBytes: {totalBytes}");
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
