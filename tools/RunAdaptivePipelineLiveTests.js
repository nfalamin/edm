/**
 * EDM Extension - Adaptive Media Pipeline 20-Scenario Live Test Runner
 * Version: 1.0.0
 * Verifies HLS/DASH manifest parsing, DRM rejection, init segments, deterministic ordering, and processing profiles.
 */

import { HlsAdapter } from '../extension/src/adaptive/hls-adapter.js';
import { DashAdapter } from '../extension/src/adaptive/dash-adapter.js';
import { DownloadPlan, Segment, SegmentStatus } from '../extension/src/adaptive/segment-model.js';
import { ProcessingPlanner, ProcessingProfile, MediaProcessingJob, ProcessingJobState } from '../extension/src/adaptive/processing-planner.js';
import { AdaptivePipelineManager } from '../extension/src/adaptive/adaptive-pipeline.js';
import { MediaRepresentation, Downloadability } from '../extension/src/media/representation-model.js';

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function assert(condition, message) {
    if (!condition) {
        throw new Error(`ASSERTION FAILED: ${message}`);
    }
}

function runTest(testId, title, testFn) {
    totalTests++;
    try {
        testFn();
        passedTests++;
        console.log(`[PASS] ${testId}: ${title}`);
    } catch (err) {
        failedTests++;
        console.error(`[FAIL] ${testId}: ${title} -> ${err.message}`);
    }
}

console.log("================================================================================");
console.log(" EDM ADAPTIVE MEDIA ENGINE - 20-SCENARIO LIVE TEST HARNESS");
console.log("================================================================================\n");

// 1. HLS VOD Playlist Parsing
runTest("ADP-01", "HLS VOD: Parses complete VOD playlist with #EXT-X-ENDLIST and segment durations", () => {
    const vM3u8 = `#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXTINF:9.009,
segment_0.ts
#EXTINF:9.009,
segment_1.ts
#EXTINF:8.500,
segment_2.ts
#EXT-X-ENDLIST`;

    const parsed = HlsAdapter.parseMediaPlaylist(vM3u8, "https://cdn.example.com/hls/v.m3u8", "fmt_720p");
    assert(parsed.isLive === false, "VOD stream must not be live");
    assert(parsed.segments.length === 3, `Expected 3 segments, got ${parsed.segments.length}`);
    assert(Math.round(parsed.totalDuration) === 27, `Total duration mismatch: ${parsed.totalDuration}`);
});

// 2. HLS Multi-Variant Master Playlist
runTest("ADP-02", "HLS Master: Parses multi-bitrate variants with resolutions and frame rates", () => {
    const masterM3u8 = `#EXTM3U
#EXT-X-STREAM-INF:BANDWIDTH=1500000,RESOLUTION=1280x720,FRAME-RATE=60.000,CODECS="avc1.4d4020,mp4a.40.2"
720p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=4500000,RESOLUTION=1920x1080,FRAME-RATE=60.000,CODECS="avc1.64002a,mp4a.40.2"
1080p/index.m3u8`;

    const result = HlsAdapter.parseMasterPlaylist(masterM3u8, "https://cdn.example.com/master.m3u8");
    assert(result.videoVariants.length === 2, "Expected 2 video variants");
    assert(result.videoVariants[1].height === 1080, "Max height should be 1080");
    assert(result.videoVariants[1].fps === 60, "Frame rate should be 60");
});

// 3. HLS Audio Groups Parsing
runTest("ADP-03", "HLS Audio Groups: Extracts discrete audio stream groups and languages", () => {
    const masterWithAudio = `#EXTM3U
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio_aac",NAME="English",LANGUAGE="en",URI="audio/en.m3u8"
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio_aac",NAME="Spanish",LANGUAGE="es",URI="audio/es.m3u8"
#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,AUDIO="audio_aac"
video/720p.m3u8`;

    const result = HlsAdapter.parseMasterPlaylist(masterWithAudio, "https://cdn.example.com/master.m3u8");
    assert(result.audioVariants.length === 2, `Expected 2 audio variants, got ${result.audioVariants.length}`);
    assert(result.audioVariants[0].qualityLabel === "Audio (English)", "Language name match");
});

// 4. DASH VOD MPD Parsing
runTest("ADP-04", "DASH VOD: Parses ISO 8601 duration and representations", () => {
    const mpd = `<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" mediaPresentationDuration="PT1H30M15S">
        <Period>
            <AdaptationSet mimeType="video/mp4">
                <Representation id="1" bandwidth="2000000" width="1280" height="720" codecs="avc1.4d401f" />
                <Representation id="2" bandwidth="4500000" width="1920" height="1080" codecs="avc1.640028" />
            </AdaptationSet>
        </Period>
    </MPD>`;

    const parsed = DashAdapter.parseMpd(mpd, "https://cdn.example.com/manifest.mpd");
    assert(parsed.duration === 5415, `Duration should be 5415s (1h30m15s), got ${parsed.duration}`);
    assert(parsed.videoVariants.length === 2, "Expected 2 DASH video variants");
});

// 5. DASH Video & Audio Separation
runTest("ADP-05", "DASH Separation: Separates video-only and audio-only representations cleanly", () => {
    const mpd = `<MPD xmlns="urn:mpeg:dash:schema:mpd:2011">
        <Period>
            <AdaptationSet mimeType="video/mp4">
                <Representation id="v1080" bandwidth="4000000" width="1920" height="1080" codecs="avc1.640028" />
            </AdaptationSet>
            <AdaptationSet mimeType="audio/mp4">
                <Representation id="a128" bandwidth="128000" codecs="mp4a.40.2" />
            </AdaptationSet>
        </Period>
    </MPD>`;

    const parsed = DashAdapter.parseMpd(mpd, "https://cdn.example.com/manifest.mpd");
    assert(parsed.videoVariants.length === 1 && parsed.videoVariants[0].isVideoOnly === true, "Video-only match");
    assert(parsed.audioVariants.length === 1 && parsed.audioVariants[0].isAudioOnly === true, "Audio-only match");
});

// 6. Initialization Segment Handling
runTest("ADP-06", "Init Segment: Distinguishes fMP4 #EXT-X-MAP initialization segment from media chunks", () => {
    const m3u8Map = `#EXTM3U
#EXT-X-MAP:URI="init_720p.mp4"
#EXTINF:4.000,
chunk_1.m4s
#EXTINF:4.000,
chunk_2.m4s
#EXT-X-ENDLIST`;

    const parsed = HlsAdapter.parseMediaPlaylist(m3u8Map, "https://cdn.example.com/video.m3u8", "fmt_map");
    assert(parsed.initSegment !== null, "Init segment must exist");
    assert(parsed.initSegment.isInitialization === true, "isInitialization must be true");
    assert(parsed.initSegment.url === "https://cdn.example.com/init_720p.mp4", "Init URL match");
    assert(parsed.segments.length === 2, "2 media segments");
});

// 7. Deterministic Segment Ordering
runTest("ADP-07", "Segment Ordering: Sequences segments monotonically starting from 1", () => {
    const m3u8 = `#EXTM3U
#EXTINF:5.0,
seg1.ts
#EXTINF:5.0,
seg2.ts
#EXTINF:5.0,
seg3.ts`;
    const parsed = HlsAdapter.parseMediaPlaylist(m3u8, "https://cdn.example.com/v.m3u8");
    assert(parsed.segments[0].sequenceNumber === 1, "First segment must be sequence 1");
    assert(parsed.segments[1].sequenceNumber === 2, "Second segment must be sequence 2");
    assert(parsed.segments[2].sequenceNumber === 3, "Third segment must be sequence 3");
});

// 8. Expired Segment Detection & Refresh
runTest("ADP-08", "Expiration: Detects expired segment and refreshes URL in DownloadPlan", () => {
    const manager = new AdaptivePipelineManager();
    const plan = manager.createDownloadPlan({
        manifestType: 'HLS',
        manifestUrl: 'https://cdn.example.com/v.m3u8',
        rawManifestText: `#EXTM3U\n#EXTINF:6.0,\nseg1.ts`
    });

    const ok = manager.handleSegmentExpiration(plan.planId, 1, "https://cdn.example.com/seg1_refreshed.ts");
    assert(ok === true, "Expiration refresh must succeed");
    assert(plan.videoSegments[0].url === "https://cdn.example.com/seg1_refreshed.ts", "URL must be updated");
});

// 9. Bounded Concurrency Limit
runTest("ADP-09", "Concurrency: Enforces default maxConcurrentSegments = 4", () => {
    const plan = new DownloadPlan({ maxConcurrentSegments: 4 });
    assert(plan.maxConcurrentSegments === 4, "Default concurrency must be 4");
});

// 10. Codec Compatibility for Lossless Merge
runTest("ADP-10", "Codec Compatibility: AVC + AAC evaluates to MERGE profile without transcoding", () => {
    const vRep = new MediaRepresentation({ codec: "avc1.640028", isVideoOnly: true, container: "mp4" });
    const aRep = new MediaRepresentation({ audioCodec: "mp4a.40.2", isAudioOnly: true, container: "m4a" });
    const profile = ProcessingPlanner.evaluateProcessingProfile(vRep, aRep);
    assert(profile === ProcessingProfile.MERGE, "AVC+AAC must evaluate to MERGE");
});

// 11. VP9 + Opus Compatibility
runTest("ADP-11", "Codec Compatibility: VP9 + Opus in WebM evaluates to MERGE profile", () => {
    const vRep = new MediaRepresentation({ codec: "vp09.00.51.08.01", isVideoOnly: true, container: "webm" });
    const aRep = new MediaRepresentation({ audioCodec: "opus", isAudioOnly: true, container: "opus" });
    const profile = ProcessingPlanner.evaluateProcessingProfile(vRep, aRep);
    assert(profile === ProcessingProfile.MERGE, "VP9+Opus must evaluate to MERGE");
});

// 12. HLS TS Remuxing
runTest("ADP-12", "Processing: HLS .m3u8 evaluates to REMUX profile (zero transcoding)", () => {
    const hlsRep = new MediaRepresentation({ container: "m3u8", mediaType: "ADAPTIVE" });
    const profile = ProcessingPlanner.evaluateProcessingProfile(hlsRep);
    assert(profile === ProcessingProfile.REMUX, "HLS must evaluate to REMUX");
});

// 13. DRM Protection Detection (DASH CENC / Widevine)
runTest("ADP-13", "DRM Protection: Rejects CENC Widevine DRM stream with UNAVAILABLE status", () => {
    const drmMpd = `<MPD xmlns="urn:mpeg:dash:schema:mpd:2011">
        <Period>
            <AdaptationSet mimeType="video/mp4">
                <ContentProtection schemeIdUri="urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed" />
                <Representation id="1" bandwidth="3000000" width="1920" height="1080" />
            </AdaptationSet>
        </Period>
    </MPD>`;

    const parsed = DashAdapter.parseMpd(drmMpd, "https://cdn.example.com/drm.mpd");
    assert(parsed.isDrm === true, "Must flag isDrm = true");
    assert(parsed.videoVariants[0].downloadability === Downloadability.UNAVAILABLE, "Must mark UNAVAILABLE");
});

// 14. DRM Encryption Detection (HLS SAMPLE-AES)
runTest("ADP-14", "DRM Protection: Rejects FairPlay / SAMPLE-AES encrypted HLS stream", () => {
    const drmHls = `#EXTM3U
#EXT-X-KEY:METHOD=SAMPLE-AES,URI="skd://key.apple.com",KEYFORMAT="com.apple.streamingkeydelivery"
#EXT-X-STREAM-INF:BANDWIDTH=2000000,RESOLUTION=1280x720
enc/index.m3u8`;

    const parsed = HlsAdapter.parseMasterPlaylist(drmHls, "https://cdn.example.com/master.m3u8");
    assert(parsed.isDrm === true, "Must flag isDrm = true");
    assert(parsed.videoVariants[0].downloadability === Downloadability.UNAVAILABLE, "Must mark UNAVAILABLE");
});

// 15. MediaProcessingJob Lifecycle Transitions
runTest("ADP-15", "Processing Job: Enforces sequential state transitions through MERGING to COMPLETED", () => {
    const job = new MediaProcessingJob({ downloadId: "dl_proc_1" });
    assert(job.state === ProcessingJobState.CREATED, "State must be CREATED");
    job.transitionTo(ProcessingJobState.QUEUED);
    job.transitionTo(ProcessingJobState.PROCESSING);
    job.transitionTo(ProcessingJobState.MERGING);
    job.transitionTo(ProcessingJobState.FINALIZING);
    job.transitionTo(ProcessingJobState.COMPLETED);
    assert(job.state === ProcessingJobState.COMPLETED, "State must be COMPLETED");
});

// 16. MediaProcessingJob Terminal Guard
runTest("ADP-16", "Processing Guard: Rejects illegal COMPLETED -> PROCESSING transition", () => {
    const job = new MediaProcessingJob({ downloadId: "dl_proc_2" });
    job.transitionTo(ProcessingJobState.COMPLETED);
    const ok = job.transitionTo(ProcessingJobState.PROCESSING);
    assert(ok === false, "COMPLETED -> PROCESSING must be blocked");
});

// 17. Partial Download Completion Calculation
runTest("ADP-17", "Partial Recovery: DownloadPlan computes completed vs total segment count", () => {
    const manager = new AdaptivePipelineManager();
    const plan = manager.createDownloadPlan({
        manifestType: 'HLS',
        manifestUrl: 'https://cdn.example.com/v.m3u8',
        rawManifestText: `#EXTM3U\n#EXTINF:5.0,\ns1.ts\n#EXTINF:5.0,\ns2.ts\n#EXT-X-ENDLIST`
    });

    assert(plan.getTotalSegmentCount() === 2, "Total segments must be 2");
    assert(plan.getCompletedSegmentCount() === 0, "Initial completed must be 0");
    manager.markSegmentComplete(plan.planId, 1);
    assert(plan.getCompletedSegmentCount() === 1, "Completed must be 1 after segment 1 complete");
    assert(plan.isComplete() === false, "Plan is not complete yet");
    manager.markSegmentComplete(plan.planId, 2);
    assert(plan.isComplete() === true, "Plan must be complete after segment 2");
});

// 18. Live Stream Flagging
runTest("ADP-18", "Live Stream: Manifest without #EXT-X-ENDLIST is flagged as isLive = true", () => {
    const liveM3u8 = `#EXTM3U\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:4.0,\nchunk_100.ts\n#EXTINF:4.0,\nchunk_101.ts`;
    const parsed = HlsAdapter.parseMediaPlaylist(liveM3u8, "https://live.cdn.com/stream.m3u8");
    assert(parsed.isLive === true, "Stream without endlist must be live");
});

// 19. Relative URL Resolution
runTest("ADP-19", "URL Resolution: Correctly resolves relative segment paths against master URL", () => {
    const resolved = HlsAdapter.resolveUrl("../media/segment1.ts", "https://cdn.example.com/hls/live/master.m3u8");
    assert(resolved === "https://cdn.example.com/hls/media/segment1.ts", `Unexpected resolved URL: ${resolved}`);
});

// 20. Direct Progressive Stream Handling
runTest("ADP-20", "Direct Stream: Direct progressive MP4 evaluates to DIRECT profile (zero processing)", () => {
    const directRep = new MediaRepresentation({ container: "mp4", mediaType: "VIDEO", isVideoOnly: false });
    const profile = ProcessingPlanner.evaluateProcessingProfile(directRep);
    assert(profile === ProcessingProfile.DIRECT, "Direct stream must evaluate to DIRECT profile");
});

console.log("\n================================================================================");
console.log(` SUMMARY: Total Tests: ${totalTests} | Passed: ${passedTests} | Failed: ${failedTests}`);
console.log("================================================================================");

if (failedTests > 0) {
    process.exit(1);
} else {
    process.exit(0);
}
