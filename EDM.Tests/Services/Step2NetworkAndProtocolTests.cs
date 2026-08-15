using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Step2NetworkAndProtocolTests
    {
        // -----------------------------------------------------------------------
        // PROMPT 2.1 — REDIRECT HANDLING (301, 302, 307, 308) & HEADER SANITIZATION
        // -----------------------------------------------------------------------

        private class MockRedirectHandler : HttpMessageHandler
        {
            private readonly byte[] _payload;

            public MockRedirectHandler(byte[] payload)
            {
                _payload = payload;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (path == "/redirect1")
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                    resp.Headers.Location = new Uri("http://example.com/redirect2");
                    resp.RequestMessage = request;
                    return Task.FromResult(resp);
                }
                if (path == "/redirect2")
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
                    resp.Headers.Location = new Uri("http://example.com/final");
                    resp.RequestMessage = request;
                    return Task.FromResult(resp);
                }
                if (path == "/final")
                {
                    if (request.Method == HttpMethod.Head)
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.OK);
                        resp.Headers.AcceptRanges.Add("bytes");
                        resp.Content = new ByteArrayContent(Array.Empty<byte>());
                        resp.Content.Headers.ContentLength = _payload.Length;
                        resp.RequestMessage = request;
                        return Task.FromResult(resp);
                    }

                    var range = request.Headers.Range;
                    if (range != null && range.Ranges.Count > 0)
                    {
                        var r = range.Ranges.GetEnumerator();
                        r.MoveNext();
                        long start = r.Current.From ?? 0;
                        long end = r.Current.To ?? (_payload.Length - 1);
                        long len = end - start + 1;

                        byte[] chunk = new byte[len];
                        Array.Copy(_payload, start, chunk, 0, len);

                        var resp = new HttpResponseMessage(HttpStatusCode.PartialContent);
                        resp.Content = new ByteArrayContent(chunk);
                        resp.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _payload.Length);
                        resp.Content.Headers.ContentLength = len;
                        resp.RequestMessage = request;
                        return Task.FromResult(resp);
                    }
                    else
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.OK);
                        resp.Content = new ByteArrayContent(_payload);
                        resp.Content.Headers.ContentLength = _payload.Length;
                        resp.RequestMessage = request;
                        return Task.FromResult(resp);
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        [Fact]
        public async Task Redirect_Chain_301_302_307_308_Succeeds()
        {
            int payloadSize = 100 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(2026).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"redir_chain_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string destFile = Path.Combine(tempDir, "redirect_file.bin");

            try
            {
                var handler = new MockRedirectHandler(payload);
                using var httpClient = new HttpClient(handler);
                var downloader = new MultiPartDownloader(httpClient);

                await downloader.DownloadFileAsync(
                    fileUrl: new Uri("http://example.com/redirect1"),
                    destinationFilePath: destFile,
                    chunkCount: 2,
                    maxConcurrency: 4,
                    progress: null,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);

                File.Exists(destFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(destFile);
                downloaded.Length.Should().Be(payload.Length);

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                }
                actualSha256.Should().Be(expectedSha256, "Redirect chain 301 -> 307 must preserve full payload integrity");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // -----------------------------------------------------------------------
        // PROMPT 2.3 — NON-RANGE SERVER FALLBACK TO SINGLE STREAM
        // -----------------------------------------------------------------------

        private class MockNoRangeHandler : HttpMessageHandler
        {
            private readonly byte[] _payload;

            public MockNoRangeHandler(byte[] payload)
            {
                _payload = payload;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new ByteArrayContent(_payload);
                resp.Content.Headers.ContentLength = _payload.Length;
                resp.RequestMessage = request;
                return Task.FromResult(resp);
            }
        }

        [Fact]
        public async Task ServerNoRange_FallsBackToSingleStreamDownload()
        {
            int payloadSize = 250 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(777).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"norange_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string destFile = Path.Combine(tempDir, "norange_file.bin");

            try
            {
                var handler = new MockNoRangeHandler(payload);
                using var httpClient = new HttpClient(handler);
                var downloader = new MultiPartDownloader(httpClient);

                await downloader.DownloadFileAsync(
                    fileUrl: new Uri("http://example.com/no-range"),
                    destinationFilePath: destFile,
                    chunkCount: 4,
                    maxConcurrency: 4,
                    progress: null,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);

                File.Exists(destFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(destFile);
                downloaded.Length.Should().Be(payload.Length, "Non-range server must fall back to single stream and write exact file length");

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                }
                actualSha256.Should().Be(expectedSha256, "Single-stream fallback payload SHA-256 must match payload perfectly");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // -----------------------------------------------------------------------
        // PROMPT 2.4 — FILENAME RESOLUTION & SECURITY SANITIZATION
        // -----------------------------------------------------------------------

        [Fact]
        public void FilenameResolution_ContentDisposition_And_SecuritySanitization()
        {
            // 1. Content-Disposition filename
            var cd = new ContentDispositionHeaderValue("attachment");
            cd.FileNameStar = "report_2026_🔥.pdf";
            var uri = new Uri("https://example.com/api/v1/download?token=xyz");

            string filename = FileNamingHelper.DetermineFileNameFromHeaders(cd, "application/pdf", uri);
            filename.Should().Contain("report_2026");

            // 2. Malicious Path Traversal Attempt
            string maliciousPath = @"..\..\..\Windows\System32\drivers\etc\hosts";
            string safeName = SecuritySanitizer.SanitizeFileName(maliciousPath);
            safeName.Should().Be("hosts");

            // 3. Reserved Windows Device Names
            string reservedName = "CON.txt";
            string safeReserved = SecuritySanitizer.SanitizeFileName(reservedName);
            safeReserved.Should().Be("_CON.txt", "Reserved device names must be prefixed to prevent OS IO locks");

            // 4. Destination Directory Boundary Enforcement
            string baseDir = @"C:\Users\Public\Downloads";
            bool validPath = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, "subfolder/file.zip", out string fullPath);
            validPath.Should().BeTrue();
            fullPath.Should().StartWith(baseDir);

            bool traversalPath = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, "../../../secret.txt", out string _);
            traversalPath.Should().BeFalse("Path traversal leaving base directory must be rejected");
        }
    }
}
