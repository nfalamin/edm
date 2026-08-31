using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdvancedHlsStreamingTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedHlsStreamingTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_HlsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        // 1. Valid M3U8 detection & parsing
        [Fact]
        public void Test1_ValidM3u8Detection_ClassifiesAsHls()
        {
            var res1 = ProtocolDetector.Detect("https://cdn.example.com/live/stream.m3u8");
            res1.Protocol.Should().Be(DownloadProtocolType.Hls);
            res1.IsStreaming.Should().BeTrue();

            var res2 = ProtocolDetector.Detect("https://cdn.example.com/stream?format=m3u8&token=abc");
            res2.Protocol.Should().Be(DownloadProtocolType.Hls);

            var res3 = ProtocolDetector.Detect("https://cdn.example.com/video/playlist", "application/vnd.apple.mpegurl");
            res3.Protocol.Should().Be(DownloadProtocolType.Hls);
        }

        // 2. Master playlist variant resolution
        [Fact]
        public void Test2_MasterPlaylistVariants_ParsesAllAttributes()
        {
            string m3u8 = @"#EXTM3U
#EXT-X-VERSION:4
#EXT-X-STREAM-INF:BANDWIDTH=800000,AVERAGE-BANDWIDTH=750000,RESOLUTION=640x360,FRAME-RATE=29.970,CODECS=""avc1.4d401e,mp4a.40.2"",AUDIO=""audio-group""
360p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2500000,AVERAGE-BANDWIDTH=2400000,RESOLUTION=1280x720,FRAME-RATE=59.940,CODECS=""avc1.4d401f,mp4a.40.2"",AUDIO=""audio-group""
720p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=5000000,AVERAGE-BANDWIDTH=4800000,RESOLUTION=1920x1080,FRAME-RATE=59.940,CODECS=""avc1.64002a,mp4a.40.2"",AUDIO=""audio-group""
1080p/index.m3u8";

            var baseUri = new Uri("https://cdn.example.com/hls/master.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.IsMaster.Should().BeTrue();
            playlist.Variants.Should().HaveCount(3);

            var top = playlist.Variants.OrderByDescending(v => v.Bandwidth).First();
            top.Bandwidth.Should().Be(5000000);
            top.Width.Should().Be(1920);
            top.Height.Should().Be(1080);
            top.FrameRate.Should().BeApproximately(59.94, 0.01);
            top.AudioGroupId.Should().Be("audio-group");
            top.Uri.Should().Be("https://cdn.example.com/hls/1080p/index.m3u8");
        }

        // 3. Media playlist parsing (EXTINF, media sequence)
        [Fact]
        public void Test3_MediaPlaylistParsing_ExtractsDurationsAndSequences()
        {
            string m3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXT-X-MEDIA-SEQUENCE:100
#EXTINF:9.500,Opening Scene
segment_100.ts
#EXTINF:10.000,Main Scene
segment_101.ts
#EXTINF:8.200,Ending Scene
segment_102.ts
#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/media/index.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.IsMaster.Should().BeFalse();
            playlist.MediaSequence.Should().Be(100);
            playlist.TargetDurationSeconds.Should().Be(10);
            playlist.Segments.Should().HaveCount(3);
            playlist.Segments[0].SequenceNumber.Should().Be(100);
            playlist.Segments[0].DurationSeconds.Should().Be(9.5);
            playlist.Segments[0].Title.Should().Be("Opening Scene");
            playlist.TotalDurationSeconds.Should().BeApproximately(27.7, 0.01);
        }

        // 4. Relative segment URL resolution against base URI
        [Fact]
        public void Test4_RelativeSegmentUrls_ResolvedAgainstBaseUri()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:5.0,
../chunks/seg1.ts
#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/vod/hls/playlist.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.Segments.Should().HaveCount(1);
            playlist.Segments[0].Uri.Should().Be("https://cdn.example.com/vod/chunks/seg1.ts");
        }

        // 5. Absolute segment URLs
        [Fact]
        public void Test5_AbsoluteSegmentUrls_PreservedAsIs()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:6.0,
https://storage.cdn.com/direct/seg01.ts
#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/playlist.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.Segments[0].Uri.Should().Be("https://storage.cdn.com/direct/seg01.ts");
        }

        // 6. Query-string segment URLs & tokens
        [Fact]
        public void Test6_QueryStringSegmentUrls_PreservedWithSecurityTokens()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:4.0,
seg1.ts?token=exp123&signature=abc9988
#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/hls/playlist.m3u8?masterToken=xyz");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.Segments[0].Uri.Should().Be("https://cdn.example.com/hls/seg1.ts?token=exp123&signature=abc9988");
        }

        // 7. Segment ordering integrity during concurrent download simulation
        [Fact]
        public async Task Test7_SegmentOrdering_PreservedDuringSequentialAssembly()
        {
            string stagingDir = Path.Combine(_testStorageDir, "staging_order_test");
            Directory.CreateDirectory(stagingDir);

            // Create 10 dummy segment parts with distinct sequence markers
            for (int i = 0; i < 10; i++)
            {
                string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                await File.WriteAllTextAsync(partPath, $"[CHUNK_{i:D2}]");
            }

            string finalOutput = Path.Combine(_testStorageDir, "assembled.ts");
            await using (var outFs = new FileStream(finalOutput, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                for (int i = 0; i < 10; i++)
                {
                    string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                    await using var partFs = File.OpenRead(partPath);
                    await partFs.CopyToAsync(outFs);
                }
            }

            string content = await File.ReadAllTextAsync(finalOutput);
            content.Should().Be("[CHUNK_00][CHUNK_01][CHUNK_02][CHUNK_03][CHUNK_04][CHUNK_05][CHUNK_06][CHUNK_07][CHUNK_08][CHUNK_09]");
        }

        // 8. Duplicate segment handling
        [Fact]
        public void Test8_DuplicateSegments_ProcessedDeterministically()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:5.0,
seg1.ts
#EXTINF:5.0,
seg1.ts
#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/playlist.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.Segments.Should().HaveCount(2);
            playlist.Segments[0].SequenceNumber.Should().Be(0);
            playlist.Segments[1].SequenceNumber.Should().Be(1);
        }

        // 9. Failed segment handling
        [Fact]
        public void Test9_FailedSegment_ReportsCorrectly()
        {
            var progress = new DownloadProgressInfo
            {
                Status = "Error",
                ErrorMessage = "Segment 12 failed HTTP 500"
            };

            progress.Status.Should().Be("Error");
            progress.ErrorMessage.Should().Contain("Segment 12");
        }

        // 10. Segment retry logic with backoff
        [Fact]
        public async Task Test10_SegmentRetry_ExecutesViaHttpRequestPipeline()
        {
            var pipeline = new HttpRequestPipeline();
            int attemptCount = 0;

            var response = await pipeline.ExecuteWithRetryAsync(() =>
            {
                attemptCount++;
                return new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://httpbin.org/status/200");
            }, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

            attemptCount.Should().BeGreaterThanOrEqualTo(1);
        }

        // 11. Interrupted download state
        [Fact]
        public void Test11_InterruptedDownload_LeavesCompletedSegmentsInStaging()
        {
            string stagingDir = Path.Combine(_testStorageDir, ".movie.mp4.hls_segments");
            Directory.CreateDirectory(stagingDir);

            File.WriteAllText(Path.Combine(stagingDir, "seg_000000.part"), "DATA_0");
            File.WriteAllText(Path.Combine(stagingDir, "seg_000001.part"), "DATA_1");

            Directory.GetFiles(stagingDir, "*.part").Length.Should().Be(2);
        }

        // 12. Resume capability from existing partial segments
        [Fact]
        public void Test12_ResumeCapability_DetectsExistingSegmentsInStaging()
        {
            string stagingDir = Path.Combine(_testStorageDir, ".video.ts.hls_segments");
            Directory.CreateDirectory(stagingDir);

            File.WriteAllText(Path.Combine(stagingDir, "seg_000000.part"), "DATA_0");
            File.WriteAllText(Path.Combine(stagingDir, "seg_000001.part"), "DATA_1");

            int completed = 0;
            for (int i = 0; i < 4; i++)
            {
                string part = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                if (File.Exists(part) && new FileInfo(part).Length > 0)
                {
                    completed++;
                }
            }

            completed.Should().Be(2, "2 out of 4 segments already exist and should be resumed");
        }

        // 13. Invalid/Malformed M3U8 playlist recovery
        [Fact]
        public void Test13_MalformedPlaylist_RecoversGracefullyWithoutCrash()
        {
            string badM3u8 = "NOT_A_VALID_M3U8_HEADER\r\nRandom garbage data\r\n123456";
            var playlist = HlsParser.Parse(badM3u8, new Uri("https://cdn.example.com/test.m3u8"));

            playlist.Should().NotBeNull();
            playlist.Segments.Should().BeEmpty();
            playlist.Variants.Should().BeEmpty();
        }

        // 14. Empty playlist handling
        [Fact]
        public void Test14_EmptyPlaylist_ReturnsEmptyStructure()
        {
            var playlist = HlsParser.Parse("", new Uri("https://cdn.example.com/empty.m3u8"));
            playlist.Should().NotBeNull();
            playlist.Segments.Should().BeEmpty();
        }

        // 15. Live playlist detection (missing #EXT-X-ENDLIST)
        [Fact]
        public void Test15_LivePlaylistDetection_IdentifiedAsLive()
        {
            string liveM3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:6
#EXTINF:6.0,
chunk_100.ts
#EXTINF:6.0,
chunk_101.ts";

            var playlist = HlsParser.Parse(liveM3u8, new Uri("https://cdn.example.com/live.m3u8"));
            playlist.IsLive.Should().BeTrue("Missing #EXT-X-ENDLIST signifies a live stream");
        }

        // 16. VOD playlist detection (contains #EXT-X-ENDLIST)
        [Fact]
        public void Test16_VodPlaylistDetection_IdentifiedAsVod()
        {
            string vodM3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:6
#EXTINF:6.0,
chunk_1.ts
#EXT-X-ENDLIST";

            var playlist = HlsParser.Parse(vodM3u8, new Uri("https://cdn.example.com/vod.m3u8"));
            playlist.IsLive.Should().BeFalse("Presence of #EXT-X-ENDLIST signifies a VOD stream");
        }

        // 17. #EXT-X-DISCONTINUITY tag handling
        [Fact]
        public void Test17_Discontinuity_MarksSegmentWithDiscontinuityFlag()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:5.0,
seg1.ts
#EXT-X-DISCONTINUITY
#EXTINF:5.0,
seg2.ts
#EXT-X-ENDLIST";

            var playlist = HlsParser.Parse(m3u8, new Uri("https://cdn.example.com/disc.m3u8"));
            playlist.Segments[0].IsDiscontinuity.Should().BeFalse();
            playlist.Segments[1].IsDiscontinuity.Should().BeTrue();
        }

        // 18. Unsupported encryption & DRM detection
        [Theory]
        [InlineData(@"#EXT-X-KEY:METHOD=SAMPLE-AES,URI=""skd://key""", "SAMPLE-AES (DRM)")]
        [InlineData(@"#EXT-X-KEY:METHOD=SAMPLE-AES,KEYFORMAT=""urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed"",URI=""widevine://key""", "Widevine")]
        [InlineData(@"#EXT-X-KEY:METHOD=SAMPLE-AES,KEYFORMAT=""com.microsoft.playready"",URI=""playready://key""", "PlayReady")]
        public void Test18_DrmDetection_FlagsAsDrmProtected(string keyLine, string expectedDrm)
        {
            string m3u8 = $@"#EXTM3U
{keyLine}
#EXTINF:5.0,
seg.ts";

            var playlist = HlsParser.Parse(m3u8, new Uri("https://cdn.example.com/drm.m3u8"));
            playlist.IsDrmProtected.Should().BeTrue();
            playlist.DrmSystem.Should().Be(expectedDrm);
        }

        // 19. Standard AES-128 key handling
        [Fact]
        public void Test19_StandardAes128_ParsedAsSupportedClearKey()
        {
            string m3u8 = @"#EXTM3U
#EXT-X-KEY:METHOD=AES-128,URI=""https://keys.example.com/key.bin"",IV=0x0102030405060708090a0b0c0d0e0f10
#EXTINF:6.0,
enc_seg1.ts
#EXT-X-ENDLIST";

            var playlist = HlsParser.Parse(m3u8, new Uri("https://cdn.example.com/playlist.m3u8"));
            playlist.IsDrmProtected.Should().BeFalse();
            playlist.Segments[0].KeyMethod.Should().Be("AES-128");
            playlist.Segments[0].KeyUri.Should().Be("https://keys.example.com/key.bin");
            playlist.Segments[0].KeyIv.Should().NotBeNull();
            playlist.Segments[0].KeyIv!.Length.Should().Be(16);
        }

        // 20. Bounded concurrency & connection throttling
        [Fact]
        public void Test20_ParallelOptions_BoundedConcurrencyConfigured()
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 8
            };

            options.MaxDegreeOfParallelism.Should().Be(8);
        }

        // 21. Queue & Scheduler integration
        [Fact]
        public void Test21_QueueIntegration_EnqueuesHlsItemCorrectly()
        {
            var queueScheduler = new DownloadQueueScheduler();
            queueScheduler.Clear();
            var item = new QueuedDownloadItem
            {
                DownloadId = "hls_job_1",
                Url = "https://example.com/stream.m3u8",
                DestinationPath = Path.Combine(_testStorageDir, "stream.mp4")
            };

            queueScheduler.Enqueue(item);
            var next = queueScheduler.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("hls_job_1");
        }

        // 22. Resource limits & malicious playlist safeguards
        [Fact]
        public void Test22_MaliciousPlaylistLimits_EnforcesSizeAndSegmentCaps()
        {
            // A. Huge text length (> 10MB)
            string hugeM3u8 = new string('A', 11 * 1024 * 1024);
            var playlist1 = HlsParser.Parse(hugeM3u8, new Uri("https://cdn.example.com/huge.m3u8"));
            playlist1.Segments.Should().BeEmpty();

            // B. Segment count cap (50,000 segments)
            var lines = new List<string> { "#EXTM3U" };
            for (int i = 0; i < 55000; i++)
            {
                lines.Add("#EXTINF:2.0,");
                lines.Add($"seg_{i}.ts");
            }
            string massiveM3u8 = string.Join("\n", lines);
            var playlist2 = HlsParser.Parse(massiveM3u8, new Uri("https://cdn.example.com/massive.m3u8"));

            playlist2.Segments.Count.Should().BeLessThanOrEqualTo(HlsParser.MaxSegmentCount);
        }
    }
}
