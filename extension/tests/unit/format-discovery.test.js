/**
 * EDM Extension - Format Discovery & Quality Representation Unit Tests
 * Version: 1.0.0
 * Verifies representation parsing, quality normalization, and proves current playback != available quality.
 */

import { MediaRepresentation, Downloadability } from '../../src/media/representation-model.js';
import { QualityNormalizer } from '../../src/media/quality-normalizer.js';
import { AdaptiveManifestParser } from '../../src/media/hls-dash-parser.js';
import { FormatRegistry } from '../../src/media/format-registry.js';
import { FormatValidator } from '../../src/media/format-validator.js';

export function runFormatDiscoveryTests() {
    const results = [];

    function test(id, name, fn) {
        try {
            fn();
            results.push({ id, name, status: "PASS" });
        } catch (err) {
            results.push({ id, name, status: "FAIL", error: err.message });
        }
    }

    // ── 1. Quality Normalization from Dimensions ──
    test("FD-01", "QualityNormalizer: Derives accurate labels from actual dimensions", () => {
        if (QualityNormalizer.normalizeQualityLabel(3840, 2160, 60) !== "2160p60 (4K UHD)") {
            throw new Error("Failed 4K 60fps normalization");
        }
        if (QualityNormalizer.normalizeQualityLabel(1920, 1080, 30) !== "1080p (Full HD)") {
            throw new Error("Failed 1080p normalization");
        }
        if (QualityNormalizer.normalizeQualityLabel(1280, 720, 0) !== "720p (HD)") {
            throw new Error("Failed 720p normalization");
        }
        if (QualityNormalizer.normalizeQualityLabel(854, 480, 0) !== "480p") {
            throw new Error("Failed 480p normalization");
        }
    });

    // ── 2. HLS Master Playlist Parsing ──
    test("FD-02", "AdaptiveManifestParser: Parses multi-bitrate HLS master playlist", () => {
        const sampleM3u8 = `#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS="avc1.4d401e,mp4a.40.2"
gear1/prog_index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1400000,RESOLUTION=854x480,CODECS="avc1.4d401f,mp4a.40.2"
gear2/prog_index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2800000,RESOLUTION=1280x720,FRAME-RATE=60.0,CODECS="avc1.4d4020,mp4a.40.2"
gear3/prog_index.m3u8`;

        const variants = AdaptiveManifestParser.parseHlsMasterPlaylist(sampleM3u8, "https://cdn.example.com/live/master.m3u8");
        if (variants.length !== 3) throw new Error(`Expected 3 variants, got ${variants.length}`);

        const top = variants[2];
        if (top.height !== 720 || top.fps !== 60 || top.bitrate !== 2800000) {
            throw new Error("Top variant parsed incorrectly");
        }
        if (top.url !== "https://cdn.example.com/live/gear3/prog_index.m3u8") {
            throw new Error(`Unexpected resolved URL: ${top.url}`);
        }
    });

    // ── 3. Video + Audio Pairing for DASH/Adaptive ──
    test("FD-03", "QualityNormalizer: Pairs video-only stream with highest bitrate audio", () => {
        const video1080p = new MediaRepresentation({
            formatId: "vid_1080",
            mediaType: "VIDEO",
            container: "mp4",
            width: 1920,
            height: 1080,
            isVideoOnly: true,
            url: "https://example.com/vid1080.mp4"
        });
        const audioLow = new MediaRepresentation({
            formatId: "aud_64",
            mediaType: "AUDIO",
            container: "m4a",
            audioBitrate: 64000,
            isAudioOnly: true,
            url: "https://example.com/aud64.m4a"
        });
        const audioHigh = new MediaRepresentation({
            formatId: "aud_160",
            mediaType: "AUDIO",
            container: "m4a",
            audioBitrate: 160000,
            isAudioOnly: true,
            url: "https://example.com/aud160.m4a"
        });

        const bestAudio = QualityNormalizer.selectBestAudioStream([audioLow, audioHigh], "mp4");
        if (!bestAudio || bestAudio.formatId !== "aud_160") {
            throw new Error("Failed to select highest bitrate audio stream");
        }

        const paired = QualityNormalizer.createPairedDownloadPackage(video1080p, bestAudio);
        if (!paired.requiresFfmpegMerge) throw new Error("RequiresFfmpegMerge should be true");
        if (paired.videoUrl !== "https://example.com/vid1080.mp4" || paired.audioUrl !== "https://example.com/aud160.m4a") {
            throw new Error("Paired URLs mismatch");
        }
    });

    // ── 4. Format Deduplication ──
    test("FD-04", "FormatRegistry: Deduplicates identical representations", () => {
        const registry = new FormatRegistry();
        const repA = new MediaRepresentation({
            container: "mp4",
            width: 1920,
            height: 1080,
            bitrate: 4500000,
            codec: "avc1",
            url: "https://example.com/stream1.mp4"
        });
        const repB = new MediaRepresentation({
            container: "mp4",
            width: 1920,
            height: 1080,
            bitrate: 4500000,
            codec: "avc1",
            url: "https://example.com/stream1.mp4"
        });

        const record = registry.registerRepresentations("media_session_1", [repA, repB]);
        if (record.videoRepresentations.length !== 1) {
            throw new Error(`Expected 1 deduplicated variant, got ${record.videoRepresentations.length}`);
        }
    });

    // ── 5. Rejection of Expired and Malformed Formats ──
    test("FD-05", "FormatValidator: Rejects expired and malformed representations", () => {
        const expired = new MediaRepresentation({
            url: "https://example.com/expired.mp4",
            expiresAt: Date.now() - 5000
        });
        if (FormatValidator.validateRepresentation(expired) !== false) {
            throw new Error("Failed to reject expired stream");
        }

        const invalidUrl = new MediaRepresentation({
            url: "javascript:alert(1)"
        });
        if (FormatValidator.validateRepresentation(invalidUrl) !== false) {
            throw new Error("Failed to reject dangerous URL scheme");
        }
    });

    // ── 6. CRITICAL QUALITY TEST 1: Current Playback 144p, Available up to 1440p ──
    test("FD-06", "Critical Quality Test 1: Current 144p playback does NOT cap available 1440p", () => {
        const registry = new FormatRegistry();
        const mediaKey = "test_video_yt_1";

        // Current playing resolution is 144p
        registry.setCurrentPlayback(mediaKey, 144, 256);

        // Available representations expose 144p, 360p, 720p, 1080p, 1440p
        const reps = [
            new MediaRepresentation({ width: 256, height: 144, url: "https://example.com/144.mp4" }),
            new MediaRepresentation({ width: 640, height: 360, url: "https://example.com/360.mp4" }),
            new MediaRepresentation({ width: 1280, height: 720, url: "https://example.com/720.mp4" }),
            new MediaRepresentation({ width: 1920, height: 1080, url: "https://example.com/1080.mp4" }),
            new MediaRepresentation({ width: 2560, height: 1440, url: "https://example.com/1440.mp4" })
        ];

        const record = registry.registerRepresentations(mediaKey, reps);

        // Assert maximum available quality is 1440p
        if (!record.maximumAvailable || record.maximumAvailable.height !== 1440) {
            throw new Error(`Expected maximumAvailable to be 1440p, got ${record.maximumAvailable?.height}p`);
        }

        // Assert current playback remains 144p
        if (record.currentPlayback.height !== 144) {
            throw new Error(`Expected currentPlayback to be 144p, got ${record.currentPlayback?.height}p`);
        }
    });

    // ── 7. CRITICAL QUALITY TEST 2: Current Playback 1080p, Available up to 720p ──
    test("FD-07", "Critical Quality Test 2: Current 1080p playback does NOT invent unavailable 1080p format", () => {
        const registry = new FormatRegistry();
        const mediaKey = "test_video_yt_2";

        // Player UI says 1080p
        registry.setCurrentPlayback(mediaKey, 1080, 1920);

        // But actual stream representations only exist up to 720p
        const reps = [
            new MediaRepresentation({ width: 256, height: 144, url: "https://example.com/144.mp4" }),
            new MediaRepresentation({ width: 640, height: 360, url: "https://example.com/360.mp4" }),
            new MediaRepresentation({ width: 1280, height: 720, url: "https://example.com/720.mp4" })
        ];

        const record = registry.registerRepresentations(mediaKey, reps);

        // Maximum available MUST be 720p (truthful discovery, no invention of 1080p)
        if (!record.maximumAvailable || record.maximumAvailable.height !== 720) {
            throw new Error(`Expected maximumAvailable to be 720p, got ${record.maximumAvailable?.height}p`);
        }
    });

    return results;
}
