/**
 * EDM Extension - Media Extractor Pipeline Live Test Runner
 * Version: 1.0.0
 * Verifies YouTube adaptive extraction, cipher solving, Vimeo parsing, and generic HTML5 extractors.
 */

import { YouTubeExtractor } from '../extension/src/extractors/youtube-extractor.js';
import { YouTubeCipher } from '../extension/src/extractors/youtube-cipher.js';
import { VimeoExtractor } from '../extension/src/extractors/vimeo-extractor.js';
import { GenericExtractor } from '../extension/src/extractors/generic-extractor.js';
import { ExtractorRegistry } from '../extension/src/extractors/extractor-registry.js';

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function assert(condition, message) {
    if (!condition) {
        throw new Error(`ASSERTION FAILED: ${message}`);
    }
}

async function runTest(testId, title, testFn) {
    totalTests++;
    try {
        await testFn();
        passedTests++;
        console.log(`[PASS] ${testId}: ${title}`);
    } catch (err) {
        failedTests++;
        console.error(`[FAIL] ${testId}: ${title} -> ${err.message}`);
    }
}

console.log("================================================================================");
console.log(" EDM MEDIA EXTRACTORS & CIPHER PIPELINE - LIVE TEST HARNESS");
console.log("================================================================================\n");

async function executeAllTests() {
    // 1. YouTube Extractor Routing
    await runTest("EXT-01", "YouTube Routing: Accurately identifies YouTube watch, shorts, and embed URLs", () => {
        const yt = new YouTubeExtractor();
        assert(yt.canHandle("https://www.youtube.com/watch?v=dQw4w9WgXcQ") === true, "Watch URL must match");
        assert(yt.canHandle("https://www.youtube.com/shorts/abcdef12345") === true, "Shorts URL must match");
        assert(yt.canHandle("https://www.youtube.com/embed/dQw4w9WgXcQ") === true, "Embed URL must match");
        assert(yt.canHandle("https://vimeo.com/123456") === false, "Vimeo URL must not match YouTube");
    });

    // 2. YouTube Cipher Unscrambler
    await runTest("EXT-02", "YouTube Cipher: Successfully applies swap, reverse, and splice transformations", () => {
        const cipherQuery = "s=abcdefghij&sp=sig&url=https%3A%2F%2Frr1---sn-abc.googlevideo.com%2Fvideoplayback%3Fexpire%3D12345";
        const operations = [
            { op: 'reverse' },
            { op: 'swap', arg: 2 },
            { op: 'splice', arg: 1 }
        ];

        const resolvedUrl = YouTubeCipher.decodeCipher(cipherQuery, operations);
        assert(resolvedUrl.startsWith("https://rr1---sn-abc.googlevideo.com/videoplayback"), "URL base must match");
        assert(resolvedUrl.includes("sig="), "Signature param 'sig' must be appended");
    });

    // 3. YouTube Adaptive Formats Extraction (144p to 4K 60fps + Opus/AAC)
    await runTest("EXT-03", "YouTube Adaptive: Discovers 4K/1080p60 video-only streams and audio tracks", async () => {
        const yt = new YouTubeExtractor();
        const fakePlayerResponse = {
            videoDetails: { title: "4K Nature Documentary 60fps", lengthSeconds: "600", author: "NatureChannel" },
            streamingData: {
                formats: [
                    { itag: 18, mimeType: "video/mp4; codecs=\"avc1.42001E, mp4a.40.2\"", width: 640, height: 360, fps: 30, url: "https://googlevideo.com/p360.mp4" },
                    { itag: 22, mimeType: "video/mp4; codecs=\"avc1.64001F, mp4a.40.2\"", width: 1280, height: 720, fps: 30, url: "https://googlevideo.com/p720.mp4" }
                ],
                adaptiveFormats: [
                    { itag: 137, mimeType: "video/mp4; codecs=\"avc1.640028\"", width: 1920, height: 1080, fps: 60, url: "https://googlevideo.com/v1080.mp4" },
                    { itag: 313, mimeType: "video/webm; codecs=\"vp9\"", width: 3840, height: 2160, fps: 60, url: "https://googlevideo.com/v4k.webm" },
                    { itag: 251, mimeType: "audio/webm; codecs=\"opus\"", bitrate: 160000, url: "https://googlevideo.com/a_opus.webm" },
                    { itag: 140, mimeType: "audio/mp4; codecs=\"mp4a.40.2\"", bitrate: 128000, url: "https://googlevideo.com/a_aac.m4a" }
                ]
            }
        };

        const result = await yt.extract({ url: "https://www.youtube.com/watch?v=nature4k", playerResponse: fakePlayerResponse });
        assert(result.title === "4K Nature Documentary 60fps", "Title match");
        assert(result.videoRepresentations.length === 4, `Expected 4 video reps (2 muxed + 2 adaptive), got ${result.videoRepresentations.length}`);
        assert(result.audioRepresentations.length === 2, "Expected 2 audio reps");
        assert(result.maximumAvailable.qualityLabel === "2160p60 (4K UHD)", `Max available label mismatch: ${result.maximumAvailable.qualityLabel}`);
    });

    // 4. Vimeo Extractor Progressive MP4s & HLS
    await runTest("EXT-04", "Vimeo Extractor: Parses progressive MP4s (720p, 1080p) and HLS master config", async () => {
        const vimeo = new VimeoExtractor();
        const fakeVimeoConfig = {
            video: { title: "Short Film in 1080p", duration: 320 },
            request: {
                files: {
                    progressive: [
                        { id: 1, quality: "720p", width: 1280, height: 720, fps: 30, url: "https://vod.vimeo.com/720p.mp4" },
                        { id: 2, quality: "1080p", width: 1920, height: 1080, fps: 30, url: "https://vod.vimeo.com/1080p.mp4" }
                    ],
                    hls: {
                        cdns: {
                            fastly: { url: "https://vod.vimeo.com/hls/master.m3u8" }
                        }
                    }
                }
            }
        };

        const result = await vimeo.extract({ url: "https://vimeo.com/987654", config: fakeVimeoConfig });
        assert(result.title === "Short Film in 1080p", "Title match");
        assert(result.videoRepresentations.length === 3, "Expected 2 progressive + 1 HLS rep");
        assert(result.maximumAvailable.qualityLabel === "1080p (Full HD)", `Max quality mismatch: ${result.maximumAvailable.qualityLabel}`);
    });

    // 5. Generic Extractor (Direct MP4, HLS, Audio)
    await runTest("EXT-05", "Generic Extractor: Resolves standalone HTML5 MP4 and HLS streams", async () => {
        const gen = new GenericExtractor();
        const resMp4 = await gen.extract({ url: "https://example.com/movie.mp4", videoWidth: 1920, videoHeight: 1080, duration: 120 });
        assert(resMp4.videoRepresentations.length === 1, "1 MP4 representation");
        assert(resMp4.videoRepresentations[0].qualityLabel === "1080p (Full HD)", "1080p label match");

        const resHls = await gen.extract({ url: "https://example.com/live/index.m3u8" });
        assert(resHls.videoRepresentations[0].container === "m3u8", "HLS container match");
    });

    // 6. Extractor Registry Routing & Priority
    await runTest("EXT-06", "Extractor Registry: Automatically selects specialized extractors over generic fallback", async () => {
        const registry = new ExtractorRegistry();
        const extYt = registry.findExtractor("https://www.youtube.com/watch?v=123");
        assert(extYt.name === "YouTubeExtractor", "Must select YouTubeExtractor for YouTube URL");

        const extVim = registry.findExtractor("https://vimeo.com/12345");
        assert(extVim.name === "VimeoExtractor", "Must select VimeoExtractor for Vimeo URL");

        const extGen = registry.findExtractor("https://myrandomsite.com/video.html");
        assert(extGen.name === "GenericExtractor", "Must select GenericExtractor as universal fallback");
    });
}

executeAllTests().then(() => {
    console.log("\n================================================================================");
    console.log(` SUMMARY: Total Tests: ${totalTests} | Passed: ${passedTests} | Failed: ${failedTests}`);
    console.log("================================================================================");

    if (failedTests > 0) {
        process.exit(1);
    } else {
        process.exit(0);
    }
});
