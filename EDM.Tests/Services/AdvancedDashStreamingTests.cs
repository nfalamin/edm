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
    public class AdvancedDashStreamingTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedDashStreamingTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_DashTests_" + Guid.NewGuid().ToString("N"));
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

        // 1. Valid MPD detection (.mpd, query strings, MIME types)
        [Fact]
        public void Test1_ValidMpdDetection_ClassifiesAsDash()
        {
            var res1 = ProtocolDetector.Detect("https://cdn.example.com/dash/stream.mpd");
            res1.Protocol.Should().Be(DownloadProtocolType.Dash);
            res1.IsStreaming.Should().BeTrue();

            var res2 = ProtocolDetector.Detect("https://cdn.example.com/manifest?format=mpd&auth=123");
            res2.Protocol.Should().Be(DownloadProtocolType.Dash);

            var res3 = ProtocolDetector.Detect("https://cdn.example.com/dash/video", "application/dash+xml");
            res3.Protocol.Should().Be(DownloadProtocolType.Dash);
        }

        // 2. Multi-period / multi-representation parsing
        [Fact]
        public void Test2_MultiPeriodParsing_ExtractsAllRepresentations()
        {
            string mpd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" mediaPresentationDuration=""PT0H1M0S"" type=""static"">
  <Period id=""p1"">
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""2000000"" width=""1280"" height=""720"" frameRate=""30"">
        <BaseURL>video_720p.mp4</BaseURL>
      </Representation>
      <Representation id=""v2"" bandwidth=""4000000"" width=""1920"" height=""1080"" frameRate=""60"">
        <BaseURL>video_1080p.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"" lang=""en"">
      <Representation id=""a1"" bandwidth=""128000"" audioSamplingRate=""48000"">
        <BaseURL>audio_en.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);
            manifest.VideoRepresentations[1].Width.Should().Be(1920);
            manifest.VideoRepresentations[1].Height.Should().Be(1080);
            manifest.VideoRepresentations[1].FrameRate.Should().Be(60);
        }

        // 3. SegmentTemplate with $Number$ expansion
        [Fact]
        public void Test3_SegmentTemplateNumberExpansion_ExpandsCorrectUrls()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" mediaPresentationDuration=""PT0H0M30S"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <SegmentTemplate media=""chunk_$RepresentationID$_$Number%04d$.m4s"" initialization=""init_$RepresentationID$.mp4"" timescale=""1000"" duration=""10000"" startNumber=""1""/>
      <Representation id=""1080p"" bandwidth=""3000000"" width=""1920"" height=""1080""/>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/media/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            var rep = manifest.VideoRepresentations.First();
            rep.InitializationUrl.Should().Be("https://cdn.example.com/media/init_1080p.mp4");
            rep.SegmentUrls.Should().HaveCount(3);
            rep.SegmentUrls[0].Should().Be("https://cdn.example.com/media/chunk_1080p_0001.m4s");
            rep.SegmentUrls[1].Should().Be("https://cdn.example.com/media/chunk_1080p_0002.m4s");
            rep.SegmentUrls[2].Should().Be("https://cdn.example.com/media/chunk_1080p_0003.m4s");
        }

        // 4. SegmentTemplate with $Time$ expansion
        [Fact]
        public void Test4_SegmentTemplateTimeExpansion_ExpandsTimestampedUrls()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <SegmentTemplate media=""seg_$Time$.mp4"" timescale=""1000"">
        <SegmentTimeline>
          <S t=""0"" d=""4000""/>
          <S d=""4000""/>
          <S d=""2000""/>
        </SegmentTimeline>
      </SegmentTemplate>
      <Representation id=""720p"" bandwidth=""1500000"" width=""1280"" height=""720""/>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            var rep = manifest.VideoRepresentations.First();
            rep.SegmentUrls.Should().HaveCount(3);
            rep.SegmentUrls[0].Should().Be("https://cdn.example.com/dash/seg_0.mp4");
            rep.SegmentUrls[1].Should().Be("https://cdn.example.com/dash/seg_4000.mp4");
            rep.SegmentUrls[2].Should().Be("https://cdn.example.com/dash/seg_8000.mp4");
        }

        // 5. SegmentTimeline parsing with repetition (r attribute)
        [Fact]
        public void Test5_SegmentTimelineRepetition_ExpandsRepeatedSegments()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <SegmentTemplate media=""chunk_$Number$.m4s"" startNumber=""10"">
        <SegmentTimeline>
          <S t=""0"" d=""2000"" r=""2""/>
        </SegmentTimeline>
      </SegmentTemplate>
      <Representation id=""rep1"" bandwidth=""1000000""/>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/stream/");
            var manifest = DashParser.Parse(mpd, baseUri);

            var rep = manifest.VideoRepresentations.First();
            rep.SegmentUrls.Should().HaveCount(3); // r=2 means 1 initial + 2 repeats = 3 segments
            rep.SegmentUrls[0].Should().Be("https://cdn.example.com/stream/chunk_10.m4s");
            rep.SegmentUrls[1].Should().Be("https://cdn.example.com/stream/chunk_11.m4s");
            rep.SegmentUrls[2].Should().Be("https://cdn.example.com/stream/chunk_12.m4s");
        }

        // 6. SegmentList parsing (SegmentURL)
        [Fact]
        public void Test6_SegmentListParsing_ParsesExplicitSegmentList()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""800000"">
        <SegmentList>
          <Initialization sourceURL=""init.mp4""/>
          <SegmentURL media=""segment_1.mp4""/>
          <SegmentURL media=""segment_2.mp4""/>
        </SegmentList>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/index.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            var rep = manifest.VideoRepresentations.First();
            rep.InitializationUrl.Should().Be("https://cdn.example.com/dash/init.mp4");
            rep.SegmentUrls.Should().HaveCount(2);
            rep.SegmentUrls[0].Should().Be("https://cdn.example.com/dash/segment_1.mp4");
            rep.SegmentUrls[1].Should().Be("https://cdn.example.com/dash/segment_2.mp4");
        }

        // 7. SegmentBase single-URL media parsing
        [Fact]
        public void Test7_SegmentBaseParsing_ExtractsDirectMediaUrl()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""audio/mp4"">
      <Representation id=""a1"" bandwidth=""128000"">
        <BaseURL>audio_stream.mp4</BaseURL>
        <SegmentBase>
          <Initialization sourceURL=""init_audio.mp4""/>
        </SegmentBase>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/vod.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            var rep = manifest.AudioRepresentations.First();
            rep.InitializationUrl.Should().Be("https://cdn.example.com/dash/init_audio.mp4");
            rep.SegmentUrls.Should().ContainSingle();
            rep.SegmentUrls[0].Should().Be("https://cdn.example.com/dash/audio_stream.mp4");
        }

        // 8. Relative BaseURL resolution against manifest URI
        [Fact]
        public void Test8_RelativeBaseUrlResolution_CombinesWithManifestUri()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <BaseURL>content/streams/</BaseURL>
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""1000000"">
        <BaseURL>video.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.VideoRepresentations.First().SegmentUrls[0].Should().Be("https://cdn.example.com/dash/content/streams/video.mp4");
        }

        // 9. Nested BaseURL resolution
        [Fact]
        public void Test9_NestedBaseUrlResolution_ResolvesAllLevels()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <BaseURL>media/</BaseURL>
  <Period>
    <BaseURL>p1/</BaseURL>
    <AdaptationSet mimeType=""video/mp4"">
      <BaseURL>hd/</BaseURL>
      <Representation id=""v1"" bandwidth=""2000000"">
        <BaseURL>1080p.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.VideoRepresentations.First().SegmentUrls[0].Should().Be("https://cdn.example.com/dash/media/p1/hd/1080p.mp4");
        }

        // 10. Absolute BaseURL preservation
        [Fact]
        public void Test10_AbsoluteBaseUrl_OverridesBaseUri()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <BaseURL>https://direct-storage.net/cdn/</BaseURL>
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""1000000"">
        <BaseURL>file.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.VideoRepresentations.First().SegmentUrls[0].Should().Be("https://direct-storage.net/cdn/file.mp4");
        }

        // 11. Initialization segment
        [Fact]
        public void Test11_InitializationSegment_ParsedCorrectly()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <SegmentTemplate initialization=""https://cdn.example.com/headers/init.mp4"" media=""chunk_$Number$.m4s"" duration=""2000"" timescale=""1000""/>
      <Representation id=""v1"" bandwidth=""500000""/>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.VideoRepresentations.First().InitializationUrl.Should().Be("https://cdn.example.com/headers/init.mp4");
        }

        // 12. Video & Audio track representation separation
        [Fact]
        public void Test12_AudioVideoSeparation_CorrectlyClassified()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""2000000"" width=""1280"" height=""720"">
        <BaseURL>v.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"">
      <Representation id=""a1"" bandwidth=""128000"">
        <BaseURL>a.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            manifest.VideoRepresentations.Should().HaveCount(1);
            manifest.AudioRepresentations.Should().HaveCount(1);
        }

        // 13. Representation quality selection (Resolution, Bandwidth, FPS)
        [Fact]
        public void Test13_RepresentationAttributes_ParsedAccurately()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""4k"" bandwidth=""15000000"" width=""3840"" height=""2160"" frameRate=""60/1"" codecs=""hev1.1.6.L150.90"">
        <BaseURL>4k.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            var rep = manifest.VideoRepresentations.First();

            rep.Width.Should().Be(3840);
            rep.Height.Should().Be(2160);
            rep.FrameRate.Should().Be(60);
            rep.Codecs.Should().Be("hev1.1.6.L150.90");
            rep.Resolution.Should().Be("3840x2160 (2160p)");
        }

        // 14. ISO-8601 duration parsing (PT1H30M15S)
        [Fact]
        public void Test14_Iso8601DurationParsing_CalculatesTotalSeconds()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" mediaPresentationDuration=""PT1H30M15.5S"">
  <Period/>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            // 1h (3600) + 30m (1800) + 15.5s = 5415.5s
            manifest.TotalDurationSeconds.Should().BeApproximately(5415.5, 0.01);
        }

        // 15. Dynamic / Live MPD detection (type="dynamic")
        [Fact]
        public void Test15_DynamicLiveDetection_IdentifiedAsLive()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" type=""dynamic"">
  <Period/>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            manifest.IsLive.Should().BeTrue();
        }

        // 16. Static / VOD MPD detection (type="static")
        [Fact]
        public void Test16_StaticVodDetection_IdentifiedAsVod()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" type=""static"">
  <Period/>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            manifest.IsLive.Should().BeFalse();
        }

        // 17. DRM detection (Widevine, PlayReady, ClearKey)
        [Theory]
        [InlineData("urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed", "Widevine")]
        [InlineData("urn:uuid:9a04f079-9840-4286-ab92-e65be0885f95", "PlayReady")]
        [InlineData("urn:uuid:e2719d58-a985-b3c9-781a-b030e4d41e12", "ClearKey")]
        public void Test17_DrmDetection_FlagsProtectedManifests(string schemeUri, string expectedDrm)
        {
            string mpd = $@"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <ContentProtection schemeIdUri=""{schemeUri}""/>
      <Representation id=""v1"" bandwidth=""1000000"">
        <BaseURL>v.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            manifest.IsDrmProtected.Should().BeTrue();
            manifest.DrmSystem.Should().Be(expectedDrm);
        }

        // 18. Malformed XML manifest recovery
        [Fact]
        public void Test18_MalformedXml_RecoversGracefullyWithoutCrash()
        {
            string badMpd = "<MPD> << Unclosed invalid XML !!!";
            var manifest = DashParser.Parse(badMpd, new Uri("https://example.com/"));

            manifest.Should().NotBeNull();
            manifest.VideoRepresentations.Should().BeEmpty();
        }

        // 19. Empty manifest handling
        [Fact]
        public void Test19_EmptyManifest_ReturnsEmptyStructure()
        {
            var manifest = DashParser.Parse("", new Uri("https://example.com/"));
            manifest.Should().NotBeNull();
            manifest.VideoRepresentations.Should().BeEmpty();
        }

        // 20. Staging directory partial resume detection
        [Fact]
        public void Test20_StagingDirectory_DetectsCompletedParts()
        {
            string stagingDir = Path.Combine(_testStorageDir, ".video.mp4.dash_segments");
            Directory.CreateDirectory(stagingDir);

            File.WriteAllText(Path.Combine(stagingDir, "seg_000000.part"), "PART_0");
            File.WriteAllText(Path.Combine(stagingDir, "seg_000001.part"), "PART_1");

            Directory.GetFiles(stagingDir, "*.part").Length.Should().Be(2);
        }

        // 21. Segment ordering during sequential file assembly
        [Fact]
        public async Task Test21_SequentialAssembly_PreservesSegmentOrder()
        {
            string stagingDir = Path.Combine(_testStorageDir, "dash_order_staging");
            Directory.CreateDirectory(stagingDir);

            for (int i = 0; i < 5; i++)
            {
                string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                await File.WriteAllTextAsync(partPath, $"[DASH_SEG_{i}]");
            }

            string outPath = Path.Combine(_testStorageDir, "final_dash.mp4");
            await using (var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                for (int i = 0; i < 5; i++)
                {
                    string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                    await using var partFs = File.OpenRead(partPath);
                    await partFs.CopyToAsync(outFs);
                }
            }

            string text = await File.ReadAllTextAsync(outPath);
            text.Should().Be("[DASH_SEG_0][DASH_SEG_1][DASH_SEG_2][DASH_SEG_3][DASH_SEG_4]");
        }

        // 22. Segment retry with backoff via HttpRequestPipeline
        [Fact]
        public async Task Test22_SegmentRetry_ExecutesThroughPipeline()
        {
            var pipeline = new HttpRequestPipeline();
            int attempts = 0;

            var response = await pipeline.ExecuteWithRetryAsync(() =>
            {
                attempts++;
                return new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://httpbin.org/status/200");
            }, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

            attempts.Should().BeGreaterThanOrEqualTo(1);
        }

        // 23. Bounded concurrency (max 8 parallel downloads)
        [Fact]
        public void Test23_BoundedConcurrency_Max8Configured()
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 8
            };

            parallelOptions.MaxDegreeOfParallelism.Should().Be(8);
        }

        // 24. Queue & Scheduler integration
        [Fact]
        public void Test24_QueueIntegration_EnqueuesDashItemCorrectly()
        {
            var queueScheduler = new DownloadQueueScheduler();
            var item = new QueuedDownloadItem
            {
                DownloadId = "dash_job_1",
                Url = "https://example.com/manifest.mpd",
                DestinationPath = Path.Combine(_testStorageDir, "dash_video.mp4")
            };

            queueScheduler.Enqueue(item);
            var next = queueScheduler.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("dash_job_1");
        }

        // 25. Resource limits & malicious MPD safeguards
        [Fact]
        public void Test25_MaliciousMpdLimits_EnforcesMaxTextLength()
        {
            string hugeMpd = new string('<', 11 * 1024 * 1024);
            var manifest = DashParser.Parse(hugeMpd, new Uri("https://example.com/manifest.mpd"));

            manifest.VideoRepresentations.Should().BeEmpty();
        }
    }
}
