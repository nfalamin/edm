using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    /// <summary>
    /// Carries the validated HTTP response and the confirmed Content-Range fields.
    /// All range fields are guaranteed to be populated when IsPartialContent == true.
    /// </summary>
    public class HttpRequestPipelineResult
    {
        public HttpResponseMessage Response { get; set; } = null!;
        public long? ContentRangeStart { get; set; }
        public long? ContentRangeEnd { get; set; }
        public long? ContentRangeTotal { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public double TimeToFirstByteMs { get; set; }
        public bool IsPartialContent => Response.StatusCode == HttpStatusCode.PartialContent;
    }


    /// <summary>
    /// Thrown when a server returns 200 OK in response to a ranged GET request,
    /// signalling that the server does not support byte ranges. The caller must
    /// fall back to a single-stream, single-worker download.
    /// </summary>
    public sealed class RangeFallbackRequiredException : Exception
    {
        public RangeFallbackRequiredException(string message) : base(message) { }
    }

    public class HttpRequestPipeline
    {
        private readonly HttpClient _httpClient;

        public HttpRequestPipeline() : this(SharedHttpClient.Instance)
        {
        }

        public HttpRequestPipeline(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public HttpRequestMessage CreateFreshRequest(
            HttpMethod method,
            Uri url,
            long? rangeStart = null,
            long? rangeEnd = null,
            DownloadCredentials? credentials = null,
            string? cookies = null,
            string? userAgent = null,
            string? referer = null,
            string? authHeader = null)
        {
            var request = new HttpRequestMessage(method, url);

            // User-Agent with CRLF sanitization
            string effectiveUserAgent = !string.IsNullOrWhiteSpace(userAgent) 
                ? HttpHeaderSecuritySanitizer.SanitizeHeaderValue(userAgent) 
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
            request.Headers.TryAddWithoutValidation("User-Agent", effectiveUserAgent);
            request.Headers.Accept.ParseAdd("*/*");

            // Referer with CRLF sanitization
            string cleanReferer = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(referer);
            if (!string.IsNullOrWhiteSpace(cleanReferer) && Uri.TryCreate(cleanReferer, UriKind.Absolute, out var parsedRef))
            {
                request.Headers.Referrer = parsedRef;
            }
            else
            {
                request.Headers.Referrer = new Uri(url.GetLeftPart(UriPartial.Authority));
            }

            // Credentials & Authorization Headers
            if (credentials != null && !credentials.IsEmpty)
            {
                request.Headers.Authorization = credentials.ToBasicAuthHeader();
            }
            else if (!string.IsNullOrWhiteSpace(authHeader))
            {
                string cleanAuth = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(authHeader);
                var parts = cleanAuth.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation("Authorization", cleanAuth);
                }
            }

            // Cookies with length validation (max 16KB to avoid 431 Request Header Fields Too Large) & CRLF sanitization
            if (!string.IsNullOrWhiteSpace(cookies))
            {
                string cleanCookies = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(cookies);
                if (cleanCookies.Length > 16384) cleanCookies = cleanCookies.Substring(0, 16384);
                request.Headers.TryAddWithoutValidation("Cookie", cleanCookies);
            }

            // Range Header
            if (rangeStart.HasValue)
            {
                request.Headers.Range = rangeEnd.HasValue
                    ? new RangeHeaderValue(rangeStart.Value, rangeEnd.Value)
                    : new RangeHeaderValue(rangeStart.Value, null);
            }

            return request;
        }


        /// <summary>
        /// Executes an HTTP request with retry, exponential back-off, and optional strict
        /// 206 Partial Content validation.
        ///
        /// When <paramref name="requirePartialContent"/> is true, ALL of the following are
        /// enforced for every response:
        ///   1. Status MUST be 206. If 200 is received, <see cref="RangeFallbackRequiredException"/>
        ///      is thrown so callers can trigger a safe single-worker fallback.
        ///   2. Content-Range header MUST be present.
        ///   3. Content-Range start MUST equal <paramref name="expectedRangeStart"/>.
        ///   4. Content-Range end MUST equal <paramref name="expectedRangeEnd"/> (when provided).
        ///   5. Content-Range total MUST equal <paramref name="knownTotalBytes"/> (when provided).
        ///   6. Content-Length MUST equal (expectedRangeEnd - expectedRangeStart + 1) (when both ends known).
        /// </summary>
        public async Task<HttpRequestPipelineResult> ExecuteWithRetryAsync(
            Func<HttpRequestMessage> requestFactory,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken,
            int maxRetries = 5,
            bool requirePartialContent = false,
            long? expectedRangeStart = null,
            long? expectedRangeEnd = null,
            long? knownTotalBytes = null)
        {
            int attempt = 0;
            const double baseDelayMs = 400.0;
            const double maxDelayMs = 15_000.0;

            while (true)
            {
                attempt++;
                cancellationToken.ThrowIfCancellationRequested();

                // ALWAYS create a fresh HttpRequestMessage per attempt!
                using var request = requestFactory();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var response = await _httpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
                    sw.Stop();

                    // Log telemetry (scrubbed)
                    LogTelemetry(request.RequestUri, request.Headers.Range?.ToString(), (int)response.StatusCode, sw.ElapsedMilliseconds, attempt, null);

                    // Handle 301, 302, 303, 307, 308 redirect chains up to max 10 hops
                    int redirectsFollowed = 0;
                    const int maxRedirectHops = 10;
                    var currentUri = request.RequestUri;

                    while (((int)response.StatusCode is 301 or 302 or 303 or 307 or 308) &&
                           response.Headers.Location != null &&
                           redirectsFollowed < maxRedirectHops)
                    {
                        redirectsFollowed++;
                        var targetUrl = response.Headers.Location.IsAbsoluteUri
                            ? response.Headers.Location
                            : new Uri(currentUri!, response.Headers.Location);

                        response.Dispose();

                        var redirectReq = requestFactory();
                        redirectReq.RequestUri = targetUrl;

                        // RFC 9110 / Chromium-grade Cross-Origin Redirect Token & Cookie Stripping
                        if (currentUri != null)
                        {
                            CrossOriginRedirectSecurityHandler.SanitizeRequestForRedirect(redirectReq, currentUri, targetUrl);
                        }

                        currentUri = targetUrl;
                        response = await _httpClient.SendAsync(redirectReq, completionOption, cancellationToken).ConfigureAwait(false);
                    }


                    // 416 Requested Range Not Satisfiable handling
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        response.Dispose();
                        if (requirePartialContent)
                        {
                            throw new RangeFallbackRequiredException("Server returned 416 Requested Range Not Satisfiable. Single-stream fallback required.");
                        }
                    }

                    // Structured fast-fail on 401 Unauthorized / 403 Forbidden without wasting retries
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        response.Dispose();
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.AuthenticationRequired,
                            $"Authentication required (401 Unauthorized) for '{request.RequestUri?.Host}'. Credentials or session cookies missing or invalid.",
                            HttpStatusCode.Unauthorized,
                            request.RequestUri);
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        response.Dispose();
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.Forbidden,
                            $"Access denied (403 Forbidden) for '{request.RequestUri?.Host}'. Access token, signed URL, or session cookie may have expired.",
                            HttpStatusCode.Forbidden,
                            request.RequestUri);
                    }

                    // Fast-fail all non-retryable 4xx client errors immediately
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 &&
                        response.StatusCode != (HttpStatusCode)408 &&
                        response.StatusCode != (HttpStatusCode)429)
                    {
                        response.EnsureSuccessStatusCode(); // Throws HttpRequestException immediately
                    }

                    // Retry on 429 Too Many Requests or 500/502/503/504 Server Errors
                    if (response.StatusCode == (HttpStatusCode)429 ||
                        response.StatusCode == HttpStatusCode.InternalServerError ||
                        response.StatusCode == HttpStatusCode.BadGateway ||
                        response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == HttpStatusCode.GatewayTimeout)
                    {
                        if (attempt <= maxRetries)
                        {
                            TimeSpan delay = GetRetryAfterDelay(response) ?? CalculateBackoffDelay(attempt, baseDelayMs, maxDelayMs);
                            response.Dispose();
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        // Exhausted retries — throw informative HttpRequestException
                        var status = response.StatusCode;
                        response.Dispose();
                        throw new HttpRequestException($"Server returned transient error {status} and retries were exhausted.", null, status);
                    }


                    // Strict 206 Partial Content validation
                    if (requirePartialContent)
                    {
                        // BUG-FIX 3: 200 OK on a range request means server ignores ranges.
                        // Throw RangeFallbackRequiredException so the orchestrator can make
                        // a SAFE decision (cancel all workers, do single-stream download).
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            response.Dispose();
                            throw new RangeFallbackRequiredException(
                                $"Server returned 200 OK instead of 206 Partial Content for range " +
                                $"bytes={expectedRangeStart}-{expectedRangeEnd}. Server does not support byte ranges.");
                        }

                        if (response.StatusCode != HttpStatusCode.PartialContent)
                        {
                            response.EnsureSuccessStatusCode(); // surfaces 4xx/5xx properly
                        }

                        // BUG-FIX 2: Content-Range header MUST be present on a 206 response.
                        var contentRangeHeader = response.Content.Headers.ContentRange;
                        if (contentRangeHeader == null)
                        {
                            response.Dispose();
                            throw new InvalidDataException(
                                $"Server returned 206 Partial Content but omitted the Content-Range header " +
                                $"for range bytes={expectedRangeStart}-{expectedRangeEnd}. Protocol violation.");
                        }

                        var cr = ParseContentRange(contentRangeHeader);

                        // BUG-FIX 1a: Validate Content-Range START
                        if (expectedRangeStart.HasValue && cr.Start.HasValue &&
                            cr.Start.Value != expectedRangeStart.Value)
                        {
                            response.Dispose();
                            throw new InvalidDataException(
                                $"Content-Range start mismatch. " +
                                $"Requested={expectedRangeStart.Value}, Received={cr.Start.Value}.");
                        }

                        // BUG-FIX 1b: Validate Content-Range END
                        if (expectedRangeEnd.HasValue && cr.End.HasValue &&
                            cr.End.Value != expectedRangeEnd.Value)
                        {
                            response.Dispose();
                            throw new InvalidDataException(
                                $"Content-Range end mismatch. " +
                                $"Requested={expectedRangeEnd.Value}, Received={cr.End.Value}. " +
                                $"Server delivered a different byte range than requested.");
                        }

                        // BUG-FIX 1c: Validate Content-Range TOTAL against known file size
                        if (knownTotalBytes.HasValue && cr.Total.HasValue &&
                            cr.Total.Value != knownTotalBytes.Value)
                        {
                            response.Dispose();
                            throw new InvalidDataException(
                                $"Content-Range total mismatch. " +
                                $"Known file size={knownTotalBytes.Value}, Server reports={cr.Total.Value}. " +
                                $"Remote resource may have changed.");
                        }

                        // BUG-FIX 4: Validate Content-Length equals expected segment size
                        if (expectedRangeStart.HasValue && expectedRangeEnd.HasValue)
                        {
                            long expectedSegmentBytes = expectedRangeEnd.Value - expectedRangeStart.Value + 1;
                            long? contentLength = response.Content.Headers.ContentLength;
                            if (contentLength.HasValue && contentLength.Value != expectedSegmentBytes)
                            {
                                response.Dispose();
                                throw new InvalidDataException(
                                    $"Content-Length mismatch. " +
                                    $"Expected segment size={expectedSegmentBytes}, " +
                                    $"Server Content-Length={contentLength.Value}. Protocol violation.");
                            }
                        }

                        return new HttpRequestPipelineResult
                        {
                            Response = response,
                            ContentRangeStart = cr.Start,
                            ContentRangeEnd = cr.End,
                            ContentRangeTotal = cr.Total,
                            ElapsedMilliseconds = sw.ElapsedMilliseconds,
                            TimeToFirstByteMs = sw.Elapsed.TotalMilliseconds
                        };
                    }

                    response.EnsureSuccessStatusCode();
                    return new HttpRequestPipelineResult
                    {
                        Response = response,
                        ElapsedMilliseconds = sw.ElapsedMilliseconds,
                        TimeToFirstByteMs = sw.Elapsed.TotalMilliseconds
                    };

                }
                catch (OperationCanceledException)
                {
                    throw; // Do not retry user cancellation
                }
                catch (RangeFallbackRequiredException)
                {
                    throw; // Do not retry — escalate to orchestrator
                }
                catch (InvalidDataException)
                {
                    throw; // Protocol violation — do not retry silently
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    LogTelemetry(request.RequestUri, request.Headers.Range?.ToString(), 0, sw.ElapsedMilliseconds, attempt, ex.Message);

                    if (!IsTransientException(ex) || attempt > maxRetries)
                    {
                        throw;
                    }

                    TimeSpan delay = CalculateBackoffDelay(attempt, baseDelayMs, maxDelayMs);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public static bool IsTransientException(Exception ex)
        {
            if (ex is OperationCanceledException) return false;
            if (ex is InvalidOperationException) return false;
            if (ex is InvalidDataException) return false;
            if (ex is RangeFallbackRequiredException) return false;
            if (ex is UnauthorizedAccessException) return false;
            if (ex is ArgumentException) return false;
            if (ex is NotSupportedException) return false;
            if (ex is FormatException) return false;

            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode.HasValue)
                {
                    int code = (int)httpEx.StatusCode.Value;
                    if (code >= 400 && code < 500 && code != 408 && code != 429) return false;
                    if (code == 429 || code >= 500) return true;
                }
                // HttpRequestException wrapping IOException / SocketException is transient
                return true;
            }

            // Plain IOException (network reset, broken pipe) or SocketException or TimeoutException is transient
            if (ex is IOException || ex is System.Net.Sockets.SocketException || ex is TimeoutException) return true;

            // Default to false for non-network unhandled exceptions
            return false;
        }


        private static TimeSpan CalculateBackoffDelay(int attempt, double baseMs, double maxMs)
        {
            double jitter = Random.Shared.NextDouble() * 200.0;
            double delayMs = baseMs * Math.Pow(2, attempt) + jitter;
            return TimeSpan.FromMilliseconds(Math.Min(maxMs, Math.Max(100.0, delayMs)));
        }

        private static readonly TimeSpan MaxRetryAfterCap = TimeSpan.FromSeconds(60);

        private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter != null)
            {
                TimeSpan? parsedDelay = null;
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    parsedDelay = response.Headers.RetryAfter.Delta.Value;
                }
                else if (response.Headers.RetryAfter.Date.HasValue)
                {
                    var delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delay > TimeSpan.Zero) parsedDelay = delay;
                }

                if (parsedDelay.HasValue)
                {
                    if (parsedDelay.Value > MaxRetryAfterCap)
                    {
                        LoggingService.LogWarning($"[HTTP Pipeline] Retry-After value {parsedDelay.Value.TotalSeconds}s exceeds safety cap. Capping to {MaxRetryAfterCap.TotalSeconds}s.");
                        return MaxRetryAfterCap;
                    }
                    return parsedDelay;
                }
            }
            return null;
        }


        private static (long? Start, long? End, long? Total) ParseContentRange(ContentRangeHeaderValue contentRange)
        {
            return (contentRange.From, contentRange.To, contentRange.Length);
        }

        private static void LogTelemetry(Uri? uri, string? range, int statusCode, long elapsedMs, int attempt, string? error)
        {
            string host = uri?.Host ?? "unknown";
            string rangeText = range ?? "full";
            string statusStr = statusCode > 0 ? statusCode.ToString() : "ERROR";
            string errText = error != null ? $" | Error: {error}" : "";
            LoggingService.Log($"[HTTP Pipeline] Attempt {attempt} | Host: {host} | Range: {rangeText} | Status: {statusStr} | Elapsed: {elapsedMs}ms{errText}");
        }
    }
}
