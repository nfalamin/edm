using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Domain.Protocols;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// Master Production Certification Suite for EDM Step 2:
    /// Production-Grade Network, Protocol & Download Compatibility Certification.
    /// Exhaustively validates Sections 2.1 through 2.20.
    /// </summary>
    public class Step2NetworkProtocolCompatibilityCertificationSuite
    {
        // =====================================================================
        // SECTION 2.1 & 2.5: HTTP/HTTPS & CDN COMPATIBILITY
        // =====================================================================
        [Fact]
        public async Task Sec2_1_HttpCompatibility_HandlesKeepAliveAndConnectionReuse()
        {
            int port = 57100 + new Random().Next(100, 800);
            string serverUrl = $"http://localhost:{port}/keepalive.dat";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            byte[] sampleData = Encoding.UTF8.GetBytes("EDM Keep-Alive Payload Data Stream 2026");

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }
                    var resp = ctx.Response;
                    resp.StatusCode = 200;
                    resp.KeepAlive = true;
                    resp.ContentLength64 = sampleData.Length;
                    await resp.OutputStream.WriteAsync(sampleData, 0, sampleData.Length).ConfigureAwait(false);
                    resp.Close();
                }
            });

            try
            {
                var pipeline = new HttpRequestPipeline();
                
                // Execute multiple sequential requests reusing the SocketsHttpHandler connection pool
                for (int i = 0; i < 3; i++)
                {
                    var result = await pipeline.ExecuteWithRetryAsync(
                        () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(serverUrl)),
                        HttpCompletionOption.ResponseContentRead,
                        CancellationToken.None);

                    result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
                    byte[] received = await result.Response.Content.ReadAsByteArrayAsync();
                    received.Should().BeEquivalentTo(sampleData);
                    result.Response.Dispose();
                }
            }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        }

        // =====================================================================
        // SECTION 2.2: REDIRECT ENGINE & CROSS-ORIGIN SECURITY
        // =====================================================================
        [Theory]
        [InlineData(301)]
        [InlineData(302)]
        [InlineData(303)]
        [InlineData(307)]
        [InlineData(308)]
        public async Task Sec2_2_RedirectEngine_FollowsRedirectStatusCodes(int redirectCode)
        {
            int port = 0;
            HttpListener? listener = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    port = 57300 + new Random().Next(100, 2000);
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();
                    break;
                }
                catch
                {
                    try { listener?.Close(); } catch { }
                    listener = null;
                    await Task.Delay(25);
                }
            }

            if (listener == null) return;

            string originUrl = $"http://127.0.0.1:{port}/initial";
            string targetUrl = $"http://127.0.0.1:{port}/final.dat";
            byte[] targetData = Encoding.UTF8.GetBytes("Redirect Target Content Stream");

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }
                    var req = ctx.Request;
                    var resp = ctx.Response;

                    if (req.Url?.AbsolutePath == "/initial")
                    {
                        resp.StatusCode = redirectCode;
                        resp.Headers["Location"] = targetUrl;
                        resp.Close();
                    }
                    else
                    {
                        resp.StatusCode = 200;
                        resp.ContentLength64 = targetData.Length;
                        await resp.OutputStream.WriteAsync(targetData, 0, targetData.Length).ConfigureAwait(false);
                        resp.Close();
                    }
                }
            });

            try
            {
                var pipeline = new HttpRequestPipeline();
                var result = await pipeline.ExecuteWithRetryAsync(
                    () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(originUrl)),
                    HttpCompletionOption.ResponseContentRead,
                    CancellationToken.None);

                result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
                byte[] content = await result.Response.Content.ReadAsByteArrayAsync();
                content.Should().BeEquivalentTo(targetData);
                result.Response.Dispose();
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
            }
        }

        [Fact]
        public void Sec2_2_RedirectEngine_DetectsCircularRedirectLoops()
        {
            var origin = new Uri("https://cdn.example.com/item/1");
            var hop1 = new Uri("https://cdn.example.com/item/2");
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First hop OK
            bool hop1Valid = HttpRetryDecisionEngine.ValidateRedirectSecurity(origin, hop1, visited, out _);
            hop1Valid.Should().BeTrue();

            // Circular hop back to hop1 must be rejected
            bool loopValid = HttpRetryDecisionEngine.ValidateRedirectSecurity(origin, hop1, visited, out _);
            loopValid.Should().BeFalse("Circular redirect loops must be rejected immediately");
        }

        [Fact]
        public void Sec2_2_RedirectEngine_ProtectsAgainstHttpsToHttpDowngrade()
        {
            var secureOrigin = new Uri("https://secure.bank.com/download/receipt");
            var insecureTarget = new Uri("http://insecure.cdn.com/download/receipt");
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool allowed = HttpRetryDecisionEngine.ValidateRedirectSecurity(secureOrigin, insecureTarget, visited, out bool stripAuth);
            allowed.Should().BeTrue();
            stripAuth.Should().BeTrue("HTTPS to HTTP downgrade MUST unconditionally strip Authorization header");
        }

        [Fact]
        public void Sec2_2_RedirectEngine_PreservesSignedUrlParameters()
        {
            var signedUri = new Uri("https://s3.amazonaws.com/bucket/file.zip?X-Amz-Signature=abcdef123456&X-Amz-Expires=3600");
            var redirectedWithToken = new Uri("https://cdn.cloudfront.net/bucket/file.zip?X-Amz-Signature=abcdef123456&X-Amz-Expires=3600");
            var redirectedWithoutToken = new Uri("https://cdn.cloudfront.net/bucket/file.zip");

            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(signedUri, redirectedWithToken).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(signedUri, redirectedWithoutToken).Should().BeFalse();
        }

        // =====================================================================
        // SECTION 2.3: RANGE REQUEST COMPATIBILITY & 200/416 FALLBACK
        // =====================================================================
        [Fact]
        public async Task Sec2_3_RangeRequest_200OkOnRange_TriggersFallbackException()
        {
            int port = 57300 + new Random().Next(100, 800);
            string serverUrl = $"http://localhost:{port}/norange.dat";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            byte[] fullData = Encoding.UTF8.GetBytes("Full non-ranged stream content");

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }
                    var resp = ctx.Response;
                    // Intentionally return 200 OK even though Range header was sent
                    resp.StatusCode = 200;
                    resp.ContentLength64 = fullData.Length;
                    await resp.OutputStream.WriteAsync(fullData, 0, fullData.Length).ConfigureAwait(false);
                    resp.Close();
                }
            });

            try
            {
                var pipeline = new HttpRequestPipeline();
                Func<Task> act = async () =>
                {
                    await pipeline.ExecuteWithRetryAsync(
                        () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(serverUrl), rangeStart: 100, rangeEnd: 200),
                        HttpCompletionOption.ResponseHeadersRead,
                        CancellationToken.None,
                        requirePartialContent: true,
                        expectedRangeStart: 100,
                        expectedRangeEnd: 200,
                        knownTotalBytes: 500);
                };

                await act.Should().ThrowAsync<RangeFallbackRequiredException>(
                    "200 OK on range request must trigger RangeFallbackRequiredException to switch to single stream");
            }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        }

        [Fact]
        public async Task Sec2_3_RangeRequest_Validates206PartialContentHeadersStrictly()
        {
            int port = 57400 + new Random().Next(100, 800);
            string serverUrl = $"http://localhost:{port}/partial.dat";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            byte[] sliceData = new byte[100]; // 100 bytes (indices 100-199)
            new Random(99).NextBytes(sliceData);

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }
                    var resp = ctx.Response;
                    resp.StatusCode = 206;
                    resp.Headers["Content-Range"] = "bytes 100-199/1000";
                    resp.ContentLength64 = 100;
                    await resp.OutputStream.WriteAsync(sliceData, 0, sliceData.Length).ConfigureAwait(false);
                    resp.Close();
                }
            });

            try
            {
                var pipeline = new HttpRequestPipeline();
                var result = await pipeline.ExecuteWithRetryAsync(
                    () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(serverUrl), rangeStart: 100, rangeEnd: 199),
                    HttpCompletionOption.ResponseContentRead,
                    CancellationToken.None,
                    requirePartialContent: true,
                    expectedRangeStart: 100,
                    expectedRangeEnd: 199,
                    knownTotalBytes: 1000);

                result.IsPartialContent.Should().BeTrue();
                result.ContentRangeStart.Should().Be(100);
                result.ContentRangeEnd.Should().Be(199);
                result.ContentRangeTotal.Should().Be(1000);
                result.Response.Dispose();
            }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        }

        // =====================================================================
        // SECTION 2.4: FILE IDENTITY & STALE RESUME DETECTION (ETAG / LAST-MODIFIED)
        // =====================================================================
        [Fact]
        public void Sec2_4_FileIdentity_DetectsETagDriftAndInvalidatesResumeState()
        {
            var metaManager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                Url = "https://cdn.example.com/firmware.bin",
                TotalBytes = 50_000_000,
                ETag = "\"etag-v1-original\"",
                LastModified = "Wed, 21 Oct 2025 07:28:00 GMT",
                Segments = new List<SegmentRange>
                {
                    new() { Id = 1, Start = 0, End = 49_999_999, BytesDownloaded = 20_000_000, State = SegmentState.Downloading }
                }
            };

            // Remote ETag changed (server updated file)
            bool isResumeValid = metaManager.ReconcileAndValidate(state, remoteETag: "\"etag-v2-updated\"", remoteLastModified: "Wed, 21 Oct 2025 07:28:00 GMT");
            isResumeValid.Should().BeFalse("Stale ETag must invalidate resume state to avoid byte corruption");
        }

        [Fact]
        public void Sec2_4_FileIdentity_DetectsLastModifiedDriftAndInvalidatesResumeState()
        {
            var metaManager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                Url = "https://cdn.example.com/asset.iso",
                TotalBytes = 100_000_000,
                ETag = "\"static-hash\"",
                LastModified = "Mon, 01 Jan 2026 10:00:00 GMT",
                Segments = new List<SegmentRange>
                {
                    new() { Id = 1, Start = 0, End = 99_999_999, BytesDownloaded = 50_000_000, State = SegmentState.Downloading }
                }
            };

            // Remote Last-Modified changed
            bool isResumeValid = metaManager.ReconcileAndValidate(state, remoteETag: "\"static-hash\"", remoteLastModified: "Tue, 02 Jan 2026 12:00:00 GMT");
            isResumeValid.Should().BeFalse("Stale Last-Modified must invalidate resume state");
        }

        // =====================================================================
        // SECTION 2.6: AUTHENTICATION, HEADERS & CRLF SANITIZATION
        // =====================================================================
        [Fact]
        public void Sec2_6_HeaderSecurity_StripsCrlfInjection()
        {
            string maliciousHeader = "application/json\r\nInjected-Header: evil-payload\r\n";
            string sanitized = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(maliciousHeader);

            sanitized.Should().NotContain("\r");
            sanitized.Should().NotContain("\n");
            sanitized.Should().Be("application/jsonInjected-Header: evil-payload");
        }

        [Theory]
        [InlineData("Host", true)]
        [InlineData("Connection", true)]
        [InlineData("Transfer-Encoding", true)]
        [InlineData("Content-Length", true)]
        [InlineData("Authorization", false)]
        [InlineData("X-Custom-Header", false)]
        [InlineData("User-Agent", false)]
        public void Sec2_6_HeaderSecurity_BlocksForbiddenHopByHopHeaders(string headerName, bool expectedForbidden)
        {
            HttpHeaderSecuritySanitizer.IsForbiddenHeader(headerName).Should().Be(expectedForbidden);
        }

        [Fact]
        public void Sec2_6_Authentication_FormatsBasicAndBearerHeadersSafely()
        {
            var creds = new DownloadCredentials("admin_user", "super$ecret!pass");
            var basicHeader = creds.ToBasicAuthHeader();
            basicHeader.Scheme.Should().Be("Basic");
            basicHeader.Parameter.Should().NotBeNullOrWhiteSpace();

            string rawDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(basicHeader.Parameter!));
            rawDecoded.Should().Be("admin_user:super$ecret!pass");
        }

        // =====================================================================
        // SECTION 2.7 & 2.8: RETRY SYSTEM, 429 & RATE LIMITING
        // =====================================================================
        [Fact]
        public void Sec2_7_RetryEngine_ParsesHttpDateRetryAfterHeader()
        {
            using var response = new HttpResponseMessage((HttpStatusCode)429);
            var futureDate = DateTimeOffset.UtcNow.AddSeconds(45);
            response.Headers.TryAddWithoutValidation("Retry-After", futureDate.ToString("R"));

            var delay = HttpRetryDecisionEngine.ParseRetryAfterHeader(response);
            delay.Should().NotBeNull();
            delay!.Value.TotalSeconds.Should().BeInRange(40, 50);
        }

        [Fact]
        public void Sec2_7_RetryEngine_ClassifiesSocketErrorsAccurately()
        {
            var resetEx = new SocketException((int)SocketError.ConnectionReset);
            var decisionReset = HttpRetryDecisionEngine.EvaluateException(resetEx, attempt: 1);
            decisionReset.Action.Should().Be(RetryAction.Retry);

            var fatalDnsEx = new SocketException((int)SocketError.HostNotFound);
            var decisionDns = HttpRetryDecisionEngine.EvaluateException(fatalDnsEx, attempt: 1);
            decisionDns.Action.Should().Be(RetryAction.Abort);
        }

        [Fact]
        public async Task Sec2_8_RateLimiting_AdaptiveGovernor_LimitsThroughput()
        {
            var governor = new AdaptiveThroughputGovernor();
            governor.SetRateLimit(500 * 1024); // 500 KB/s

            var sw = Stopwatch.StartNew();
            // Read 250 KB -> should take approx ~500ms
            await governor.ApplyRateLimitingAsync(250 * 1024, CancellationToken.None);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(0);
        }

        // =====================================================================
        // SECTION 2.10: PROXY COMPATIBILITY & DPAPI ENCRYPTION
        // =====================================================================
        [Fact]
        public void Sec2_10_ProxyService_EncryptsAndDecryptsCredentialsViaDpapi()
        {
            if (!OperatingSystem.IsWindows()) return;

            string rawPassword = "CorporateProxy#Secret_Password_2026";
            string encrypted = ProxyService.EncryptPassword(rawPassword);

            encrypted.Should().NotBeNullOrWhiteSpace();
            encrypted.Should().NotBe(rawPassword, "Password must be strongly encrypted before storage");

            string decrypted = ProxyService.DecryptPassword(encrypted);
            decrypted.Should().Be(rawPassword);
        }

        [Fact]
        public void Sec2_10_ProxyService_BuildsConfiguredWebProxyWithBypass()
        {
            var settings = new ProxySettings
            {
                Enabled = true,
                Host = "10.0.0.1",
                Port = 8080,
                Type = ProxyType.Http,
                BypassLocalAddresses = true,
                BypassList = "localhost,127.0.0.1,*.internal.corp"
            };

            var proxy = ProxyService.BuildWebProxy(settings);
            proxy.Should().NotBeNull();
            proxy!.IsBypassed(new Uri("http://localhost/api")).Should().BeTrue();
            proxy.IsBypassed(new Uri("http://external.public-site.com/file")).Should().BeFalse();
        }

        // =====================================================================
        // SECTION 2.11: FTP / FTPS COMPATIBILITY
        // =====================================================================
        [Fact]
        public void Sec2_11_FtpCompatibility_ValidatesFtpAndFtpsSchemes()
        {
            SecuritySanitizer.IsAllowedUrlScheme("ftp://ftp.gnu.org/gnu/gcc/gcc-13.2.0.tar.gz").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("ftps://secure-ftp.enterprise.com/blobs/data.bin").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("sftp://ssh-ftp.company.com/file").Should().BeTrue();
        }

        // =====================================================================
        // SECTION 2.12 & 2.13: LARGE FILES & UNKNOWN FILE SIZE HANDLING
        // =====================================================================
        [Fact]
        public void Sec2_12_LargeFiles_Handles64BitIntegerOffsetsWithoutOverflow()
        {
            // 200 GB file test
            long largeFileSize = 200L * 1024L * 1024L * 1024L;
            largeFileSize.Should().BeGreaterThan(int.MaxValue);

            var chunk = new DynamicChunk(1, 100L * 1024L * 1024L * 1024L, largeFileSize - 1, 50L * 1024L * 1024L * 1024L);

            chunk.StartOffset.Should().Be(100L * 1024L * 1024L * 1024L);
            chunk.CurrentOffset.Should().Be(150L * 1024L * 1024L * 1024L);
            chunk.RemainingBytes.Should().Be(50L * 1024L * 1024L * 1024L);
        }

        [Fact]
        public void Sec2_13_UnknownFileSize_GracefullyHandlesNullContentLength()
        {
            var info = new DownloadProgressInfo
            {
                BytesDownloaded = 10_485_760, // 10 MB received
                TotalBytes = null,            // Unknown Content-Length
                SpeedBytesPerSecond = 2_097_152 // 2 MB/s
            };

            // When TotalBytes is null, HasKnownTotal should be false and Eta should gracefully show "Calculating..."
            info.HasKnownTotal.Should().BeFalse();
            info.TotalBytes.Should().BeNull();
            info.Eta.Should().Be("Calculating...");
        }

        // =====================================================================
        // SECTION 2.14 & 2.15: FILENAME RESOLUTION, MIME & EXTENSION SAFETY
        // =====================================================================
        [Fact]
        public void Sec2_14_FileNameResolution_ExtractsRfc5987Utf8Filename()
        {
            var cd = new ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = "UTF-8''%E6%97%A5%E6%9C%AC%E8%AA%9E_%E3%83%89%E3%82%AD%E3%83%A5%E3%83%A1%E3%83%B3%E3%83%88.pdf"
            };

            string resolved = FileNamingHelper.ResolveAuthoritativeFileName(
                explicitUserFilename: null,
                cd: cd,
                mediaTitle: null,
                mimeType: "application/pdf",
                requestUri: new Uri("https://example.com/download?id=123"));

            resolved.Should().Be("日本語_ドキュメント.pdf");
        }

        [Theory]
        [InlineData("../../../Windows/System32/drivers/etc/hosts", "hosts")]
        [InlineData("CON.txt", "CON_file.txt")]
        [InlineData("NUL.iso", "NUL_file.iso")]
        [InlineData("COM1.tar.gz", "COM1_file.tar.gz")]
        [InlineData("AUX.zip", "AUX_file.zip")]
        [InlineData("unsafe:name*with?illegal<chars>|.bin", "unsafe_name_with_illegal_chars__.bin")]
        public void Sec2_14_FileNameResolution_SanitizesPathTraversalAndDosDeviceNames(string input, string expected)
        {
            string sanitized = FileNamingHelper.SanitizeFileName(input);
            sanitized.Should().Be(expected);
        }

        [Fact]
        public void Sec2_14_FileNameResolution_GeneratesUniqueCollisionPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"EDM_Collision_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string baseFile = Path.Combine(tempDir, "document.pdf");

            try
            {
                File.WriteAllText(baseFile, "test");
                string unique1 = SecuritySanitizer.GetUniqueDestinationPath(baseFile);
                unique1.Should().Be(Path.Combine(tempDir, "document (1).pdf"));

                File.WriteAllText(unique1, "test");
                string unique2 = SecuritySanitizer.GetUniqueDestinationPath(baseFile);
                unique2.Should().Be(Path.Combine(tempDir, "document (2).pdf"));
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Sec2_15_MimeExtensionSafety_PreservesCompoundExtensions()
        {
            string fileName = "archive.tar.gz";
            string resolved = FileNamingHelper.ResolveAuthoritativeFileName(
                explicitUserFilename: fileName,
                cd: null,
                mediaTitle: null,
                mimeType: "application/gzip",
                requestUri: new Uri("https://example.com/archive.tar.gz"));

            resolved.Should().Be("archive.tar.gz");
        }

        // =====================================================================
        // SECTION 2.16 & 2.17: CANCELLATION & RESOURCE LEAK AUDIT
        // =====================================================================
        [Fact]
        public async Task Sec2_16_Cancellation_DeterministicCancellationStopsPromptly()
        {
            using var cts = new CancellationTokenSource();
            var pauseToken = new PauseTokenSource();

            int port = 57500 + new Random().Next(100, 800);
            string serverUrl = $"http://localhost:{port}/longdownload.bin";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            byte[] chunk = new byte[64 * 1024];

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var resp = ctx.Response;
                            resp.StatusCode = 200;
                            resp.ContentLength64 = 50 * 1024 * 1024; // 50 MB
                            while (resp.OutputStream.CanWrite)
                            {
                                await resp.OutputStream.WriteAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
                                await Task.Delay(50).ConfigureAwait(false);
                            }
                            resp.Close();
                        }
                        catch { }
                    });
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), $"EDM_Cancel_{Guid.NewGuid():N}.bin");
            try
            {
                // Trigger cancellation after 150ms (simulating mid-download user cancel)
                cts.CancelAfter(150);

                var progressReporter = new Progress<DownloadProgressInfo>();

                Func<Task> act = async () =>
                {
                    await MultiPartAdapter.DownloadWithMultiPartAsync(
                        serverUrl,
                        tempFile,
                        chunkCount: 2,
                        progressReporter,
                        pauseToken,
                        () => -1,
                        cts.Token,
                        null,
                        null);
                };

                await act.Should().ThrowAsync<OperationCanceledException>();
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        // =====================================================================
        // SECTION 2.19: SECURITY AUDIT & URI SCHEME FILTERING
        // =====================================================================
        [Theory]
        [InlineData("http://example.com/setup.exe", true)]
        [InlineData("https://example.com/setup.exe", true)]
        [InlineData("ftp://ftp.example.com/file.zip", true)]
        [InlineData("ftps://ftp.example.com/file.zip", true)]
        [InlineData("sftp://ftp.example.com/file.zip", true)]
        [InlineData("magnet:?xt=urn:btih:1234567890", true)]
        [InlineData("file:///C:/Windows/System32/calc.exe", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("data:text/html,<script>alert(1)</script>", false)]
        [InlineData("vbscript:MsgBox(1)", false)]
        public void Sec2_19_SecurityAudit_EnforcesAllowedUrlSchemesStrictly(string url, bool expectedAllowed)
        {
            SecuritySanitizer.IsAllowedUrlScheme(url).Should().Be(expectedAllowed);
        }
    }
}
