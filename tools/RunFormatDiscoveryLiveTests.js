/**
 * EDM Extension - Live Format Discovery & Representation Verification Runner
 * Version: 1.0.0
 * Executes live test cases with strict assertions and static analysis for hardcoded quality arrays.
 */

import { MediaRepresentation, Downloadability, DiscoveryStatus } from '../extension/src/media/representation-model.js';
import { QualityNormalizer } from '../extension/src/media/quality-normalizer.js';
import { AdaptiveManifestParser } from '../extension/src/media/hls-dash-parser.js';
import { FormatRegistry } from '../extension/src/media/format-registry.js';
import { FormatValidator } from '../extension/src/media/format-validator.js';
import fs from 'fs';
import path from 'path';

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;
const testLogs = [];

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
        testLogs.push({ testId, title, status: "PASS", error: null });
        console.log(`[PASS] ${testId}: ${title}`);
    } catch (err) {
        failedTests++;
        testLogs.push({ testId, title, status: "FAIL", error: err.message });
        console.error(`[FAIL] ${testId}: ${title} -> ${err.message}`);
    }
}

console.log("================================================================================");
console.log(" EDM FORMAT DISCOVERY & QUALITY ENUMERATION - LIVE VERIFICATION HARNESS");
console.log("================================================================================\n");

// ─────────────────────────────────────────────────────────────────────────────
// 1. PROOF: CURRENT PLAYBACK QUALITY != MAXIMUM AVAILABLE QUALITY
// ─────────────────────────────────────────────────────────────────────────────
runTest("TC-PLAYBACK-01", "Current Playback 144p while available up to 1440p (Does NOT cap max quality)", () => {
    const registry = new FormatRegistry();
    const mediaKey = "yt_live_stream_case_1";

    // Simulate user watching on 144p
    registry.setCurrentPlayback(mediaKey, 144, 256);

    // Actual discovered formats from manifest / player metadata
    const discoveredVariants = [
        new MediaRepresentation({ width: 256, height: 144, bitrate: 100000, container: 'mp4', url: 'https://cdn.example.com/144p.mp4' }),
        new MediaRepresentation({ width: 640, height: 360, bitrate: 450000, container: 'mp4', url: 'https://cdn.example.com/360p.mp4' }),
        new MediaRepresentation({ width: 1280, height: 720, bitrate: 1500000, container: 'mp4', url: 'https://cdn.example.com/720p.mp4' }),
        new MediaRepresentation({ width: 1920, height: 1080, bitrate: 4000000, container: 'mp4', url: 'https://cdn.example.com/1080p.mp4' }),
        new MediaRepresentation({ width: 2560, height: 1440, bitrate: 8500000, container: 'mp4', url: 'https://cdn.example.com/1440p.mp4' })
    ];

    const record = registry.registerRepresentations(mediaKey, discoveredVariants);

    assert(record.currentPlayback.height === 144, "Current playback must be 144p");
    assert(record.currentPlayback.qualityLabel === "144p", "Current label must be '144p'");
    assert(record.maximumAvailable !== null, "Maximum available must not be null");
    assert(record.maximumAvailable.height === 1440, "Maximum available height must be 1440");
    assert(record.maximumAvailable.qualityLabel === "1440p (2K QHD)", "Maximum available label must be '1440p (2K QHD)'");
    assert(record.videoRepresentations.length === 5, "Must contain all 5 distinct discovered representations");
});

runTest("TC-PLAYBACK-02", "Player set to 1080p while available is only up to 720p (Never invents fake 1080p)", () => {
    const registry = new FormatRegistry();
    const mediaKey = "yt_live_stream_case_2";

    // Simulate player claiming 1080p
    registry.setCurrentPlayback(mediaKey, 1080, 1920);

    // Actual discovered formats only go up to 720p
    const discoveredVariants = [
        new MediaRepresentation({ width: 256, height: 144, bitrate: 100000, container: 'mp4', url: 'https://cdn.example.com/144p.mp4' }),
        new MediaRepresentation({ width: 640, height: 360, bitrate: 450000, container: 'mp4', url: 'https://cdn.example.com/360p.mp4' }),
        new MediaRepresentation({ width: 1280, height: 720, bitrate: 1500000, container: 'mp4', url: 'https://cdn.example.com/720p.mp4' })
    ];

    const record = registry.registerRepresentations(mediaKey, discoveredVariants);

    assert(record.currentPlayback.height === 1080, "Current playback must record 1080");
    assert(record.maximumAvailable !== null, "Maximum available must not be null");
    assert(record.maximumAvailable.height === 720, "Maximum available height must be strictly 720p");
    assert(record.maximumAvailable.qualityLabel === "720p (HD)", "Max available label must be '720p (HD)'");
    assert(!record.videoRepresentations.some(r => r.height === 1080), "Registry MUST NOT contain a 1080p representation");
});

// ─────────────────────────────────────────────────────────────────────────────
// 2. PROOF: NO HARDCODED QUALITY LIST USED AS DISCOVERY EVIDENCE
// ─────────────────────────────────────────────────────────────────────────────
runTest("TC-STATIC-03", "Static Code Analysis: Zero hardcoded quality arrays used for representation discovery", () => {
    const mediaDir = path.resolve("d:/Update EDM/EDM/extension/src/media");
    const files = fs.readdirSync(mediaDir).filter(f => f.endsWith(".js"));

    const forbiddenPatterns = [
        /=\s*\[\s*["']144p["'],\s*["']240p["']/i,
        /=\s*\[\s*["']720p["'],\s*["']1080p["'],\s*["']1440p["']/i,
        /availableQualities\s*=\s*\[/i
    ];

    for (const file of files) {
        const fullPath = path.join(mediaDir, file);
        const code = fs.readFileSync(fullPath, "utf-8");

        for (const pattern of forbiddenPatterns) {
            if (pattern.test(code)) {
                throw new Error(`Forbidden hardcoded quality pattern found in ${file}: ${pattern}`);
            }
        }
    }
});

// ─────────────────────────────────────────────────────────────────────────────
// 3. PROOF: VIDEO-ONLY VS AUDIO-ONLY REPRESENTATION SEPARATION & PAIRING
// ─────────────────────────────────────────────────────────────────────────────
runTest("TC-SEPARATION-04", "Video-Only and Audio-Only representations remain strictly separate and pair cleanly", () => {
    const registry = new FormatRegistry();
    const mediaKey = "dash_stream_separation_test";

    const videoOnly1080p = new MediaRepresentation({
        formatId: "vid_1080_dash",
        mediaType: "VIDEO",
        container: "mp4",
        codec: "avc1.640028",
        width: 1920,
        height: 1080,
        fps: 60,
        bitrate: 4200000,
        isVideoOnly: true,
        isAudioOnly: false,
        isMuxed: false,
        url: "https://cdn.example.com/video_1080p_dash.mp4"
    });

    const videoOnly4K = new MediaRepresentation({
        formatId: "vid_2160_dash",
        mediaType: "VIDEO",
        container: "webm",
        codec: "vp09.00.51.08.01",
        width: 3840,
        height: 2160,
        fps: 60,
        bitrate: 14000000,
        isVideoOnly: true,
        isAudioOnly: false,
        isMuxed: false,
        url: "https://cdn.example.com/video_2160p_dash.webm"
    });

    const audioLow = new MediaRepresentation({
        formatId: "aud_aac_64",
        mediaType: "AUDIO",
        container: "m4a",
        audioCodec: "mp4a.40.2",
        audioBitrate: 64000,
        sampleRate: 44100,
        channels: 2,
        isVideoOnly: false,
        isAudioOnly: true,
        isMuxed: false,
        url: "https://cdn.example.com/audio_64k.m4a"
    });

    const audioHigh = new MediaRepresentation({
        formatId: "aud_aac_160",
        mediaType: "AUDIO",
        container: "m4a",
        audioCodec: "mp4a.40.2",
        audioBitrate: 160000,
        sampleRate: 48000,
        channels: 2,
        isVideoOnly: false,
        isAudioOnly: true,
        isMuxed: false,
        url: "https://cdn.example.com/audio_160k.m4a"
    });

    const record = registry.registerRepresentations(mediaKey, [videoOnly1080p, videoOnly4K, audioLow, audioHigh]);

    // 1. Verify Video Only Separation
    assert(record.videoRepresentations.length === 2, "Must have exactly 2 video representations");
    assert(record.videoRepresentations.every(v => v.isVideoOnly && !v.isAudioOnly), "All video representations must be video-only");

    // 2. Verify Audio Only Separation
    assert(record.audioRepresentations.length === 2, "Must have exactly 2 audio representations");
    assert(record.audioRepresentations.every(a => a.isAudioOnly && !a.isVideoOnly), "All audio representations must be audio-only");

    // 3. Verify Audio Pairing Selection
    const bestAudio = QualityNormalizer.selectBestAudioStream(record.audioRepresentations, "mp4");
    assert(bestAudio.formatId === "aud_aac_160", "Must select highest bitrate audio track (160kbps)");

    // 4. Verify Download Package Construction
    const pkg = QualityNormalizer.createPairedDownloadPackage(videoOnly1080p, bestAudio);
    assert(pkg.requiresFfmpegMerge === true, "Must require ffmpeg merge for DASH video-only");
    assert(pkg.videoUrl === "https://cdn.example.com/video_1080p_dash.mp4", "Video URL match");
    assert(pkg.audioUrl === "https://cdn.example.com/audio_160k.m4a", "Audio URL match");
    assert(pkg.quality === "1080p60 (Full HD)", "Quality label match");
    assert(pkg.downloadability === Downloadability.REQUIRES_MERGE, "Downloadability must be REQUIRES_MERGE");
});

// ─────────────────────────────────────────────────────────────────────────────
// 4. PROOF: EXPIRED, UNSUPPORTED, AND PARTIAL DISCOVERY HANDLING
// ─────────────────────────────────────────────────────────────────────────────
runTest("TC-EXPIRATION-05", "Expired signed URL is detected and rejected", () => {
    const expiredTimestamp = Date.now() - 60000; // 1 minute in the past
    const expiredRep = new MediaRepresentation({
        formatId: "expired_signed_stream",
        url: "https://cdn.example.com/stream.mp4?expire=" + Math.floor(expiredTimestamp / 1000),
        expiresAt: expiredTimestamp,
        width: 1280,
        height: 720
    });

    assert(expiredRep.isExpired() === true, "Representation must report isExpired() = true");
    assert(FormatValidator.validateRepresentation(expiredRep) === false, "Validator must reject expired representation");
});

runTest("TC-UNSUPPORTED-06", "Dangerous or unsupported schemes (javascript:, file:) are rejected", () => {
    const jsRep = new MediaRepresentation({ url: "javascript:window.location='https://evil.com'" });
    const fileRep = new MediaRepresentation({ url: "file:///C:/Windows/System32/cmd.exe" });
    const emptyRep = new MediaRepresentation({ url: "" });

    assert(FormatValidator.validateRepresentation(jsRep) === false, "Must reject javascript: scheme");
    assert(FormatValidator.validateRepresentation(fileRep) === false, "Must reject file: scheme");
    assert(FormatValidator.validateRepresentation(emptyRep) === false, "Must reject empty URL");
});

runTest("TC-PARTIAL-07", "Partial Discovery (Audio-only stream / Radio stream) is handled gracefully", () => {
    const registry = new FormatRegistry();
    const mediaKey = "online_radio_session";

    const radioAudio = new MediaRepresentation({
        formatId: "radio_aac_128",
        mediaType: "AUDIO",
        container: "aac",
        audioCodec: "mp4a.40.2",
        audioBitrate: 128000,
        isAudioOnly: true,
        url: "https://radio.example.com/live.aac"
    });

    const record = registry.registerRepresentations(mediaKey, [radioAudio]);

    assert(record.discoveryStatus === DiscoveryStatus.FAILED || record.audioRepresentations.length === 1, "Audio tracked");
    assert(record.audioRepresentations.length === 1, "Must contain 1 audio representation");
    assert(record.videoRepresentations.length === 0, "Must contain 0 video representations");
    assert(record.maximumAvailable === null, "Maximum available video quality must be null for audio-only stream");
});

// ─────────────────────────────────────────────────────────────────────────────
// 5. PROOF: REAL HLS MASTER PLAYLIST DISCOVERY
// ─────────────────────────────────────────────────────────────────────────────
runTest("TC-HLS-08", "Real HLS master playlist parsing extracts genuine resolutions and bitrates", () => {
    const liveM3u8Content = `#EXTM3U
#EXT-X-VERSION:6
#EXT-X-STREAM-INF:BANDWIDTH=528000,AVERAGE-BANDWIDTH=480000,RESOLUTION=426x240,FRAME-RATE=30.000,CODECS="avc1.4d4015,mp4a.40.2"
v_240p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1128000,AVERAGE-BANDWIDTH=1000000,RESOLUTION=854x480,FRAME-RATE=30.000,CODECS="avc1.4d401f,mp4a.40.2"
v_480p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2800000,AVERAGE-BANDWIDTH=2500000,RESOLUTION=1280x720,FRAME-RATE=60.000,CODECS="avc1.4d4020,mp4a.40.2"
v_720p60/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=6000000,AVERAGE-BANDWIDTH=5500000,RESOLUTION=1920x1080,FRAME-RATE=60.000,CODECS="avc1.64002a,mp4a.40.2"
v_1080p60/index.m3u8`;

    const baseUrl = "https://stream.server.net/hls/live/master.m3u8";
    const variants = AdaptiveManifestParser.parseHlsMasterPlaylist(liveM3u8Content, baseUrl);

    assert(variants.length === 4, `Expected 4 HLS variants, parsed ${variants.length}`);

    const maxVariant = QualityNormalizer.calculateMaximumAvailableQuality(variants);
    assert(maxVariant !== null, "Maximum variant must exist");
    assert(maxVariant.height === 1080, "Max height must be 1080");
    assert(maxVariant.fps === 60, "Max fps must be 60");
    assert(maxVariant.qualityLabel === "1080p60 (Full HD)", "Max label must be '1080p60 (Full HD)'");
    assert(maxVariant.url === "https://stream.server.net/hls/live/v_1080p60/index.m3u8", "Resolved absolute URL match");
});

console.log("\n================================================================================");
console.log(` SUMMARY: Total Tests: ${totalTests} | Passed: ${passedTests} | Failed: ${failedTests}`);
console.log("================================================================================");

if (failedTests > 0) {
    process.exit(1);
} else {
    process.exit(0);
}
