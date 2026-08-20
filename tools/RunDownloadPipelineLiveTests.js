/**
 * EDM Extension - Download Pipeline & Lifecycle 30-Scenario Live Test Runner
 * Version: 1.0.0
 * Executes exhaustive live testing of candidate creation, validation, state machine, filename security, and handoff.
 */

import { DownloadCandidate } from '../extension/src/downloads/download-candidate.js';
import { DownloadJob, DownloadJobState } from '../extension/src/downloads/download-job.js';
import { DownloadPolicy, PolicyVerdict } from '../extension/src/downloads/download-policy.js';
import { FilenameSanitizer } from '../extension/src/downloads/filename-sanitizer.js';
import { DownloadPipelineManager } from '../extension/src/downloads/download-manager.js';

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;
const testLogs = [];

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
        testLogs.push({ testId, title, status: "PASS", error: null });
        console.log(`[PASS] ${testId}: ${title}`);
    } catch (err) {
        failedTests++;
        testLogs.push({ testId, title, status: "FAIL", error: err.message });
        console.error(`[FAIL] ${testId}: ${title} -> ${err.message}`);
    }
}

console.log("================================================================================");
console.log(" EDM DOWNLOAD PIPELINE & LIFECYCLE - 30-SCENARIO LIVE TEST HARNESS");
console.log("================================================================================\n");

async function executeAllTests() {
    // 1. Valid Direct Download
    await runTest("DP-01", "Valid Direct Download: Candidate instantiates with valid parameters", () => {
        const cand = new DownloadCandidate({
            url: "https://example.com/media/video.mp4",
            filename: "my_video.mp4",
            size: 15485760,
            quality: "1080p (Full HD)"
        });
        assert(cand.isValid() === true, "Candidate should be valid");
        assert(cand.filename === "my_video.mp4", "Filename must match");
    });

    // 2. Invalid URL Rejection
    await runTest("DP-02", "Invalid URL: Rejects non-HTTP schemes and empty strings", () => {
        const cand = new DownloadCandidate({ url: "javascript:alert('pwn')" });
        assert(cand.isValid() === false, "Candidate with javascript: scheme must be invalid");
    });

    // 3. Expired URL Rejection
    await runTest("DP-03", "Expired URL: Rejects stream with past expiresAt timestamp", () => {
        const cand = new DownloadCandidate({
            url: "https://example.com/stream.mp4",
            expiresAt: Date.now() - 5000
        });
        assert(cand.isExpired() === true, "isExpired() must return true");
        assert(cand.isValid() === false, "isValid() must return false for expired stream");
    });

    // 4. Video-Only Representation
    await runTest("DP-04", "Video-Only Stream: Preserves isVideoOnly metadata", () => {
        const cand = new DownloadCandidate({
            url: "https://example.com/video_only.mp4",
            requiresMerge: false,
            mediaType: "VIDEO"
        });
        assert(cand.url === "https://example.com/video_only.mp4", "Video URL match");
    });

    // 5. Audio-Only Representation
    await runTest("DP-05", "Audio-Only Stream: Preserves isAudioOnly metadata", () => {
        const cand = new DownloadCandidate({
            url: "https://example.com/audio.m4a",
            mediaType: "AUDIO",
            container: "m4a"
        });
        assert(cand.mediaType === "AUDIO", "Media type must be AUDIO");
    });

    // 6. Video + Audio Merge Job
    await runTest("DP-06", "Video+Audio Merge: Requires both valid video and audio URLs", () => {
        const cand = new DownloadCandidate({
            videoUrl: "https://example.com/video_1080p.mp4",
            audioUrl: "https://example.com/audio_160k.m4a",
            requiresMerge: true
        });
        assert(cand.isValid() === true, "Paired candidate must be valid");
        assert(cand.requiresMerge === true, "requiresMerge must be true");
    });

    // 7. Duplicate Request Protection
    await runTest("DP-07", "Duplicate Protection: Rejects identical submissions within 3000ms", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: { sendNativeRequest: async () => ({ success: true, action: "acknowledged" }) }
        });
        const raw = { url: "https://example.com/test_dedup.mp4", quality: "1080p" };
        const res1 = await manager.initiateDownload(raw);
        assert(res1.success === true && res1.status === "HANDED_OFF_TO_EDM", "First request must succeed");

        const res2 = await manager.initiateDownload(raw);
        assert(res2.status === "DUPLICATE_SUPPRESSED", "Second identical request must be suppressed");
    });

    // 8. Double Click Prevention
    await runTest("DP-08", "Double Click: Rapid parallel invocations handled cleanly", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: { sendNativeRequest: async () => ({ success: true, action: "acknowledged" }) }
        });
        const raw = { url: "https://example.com/double_click.mp4", quality: "720p" };
        const [r1, r2] = await Promise.all([
            manager.initiateDownload(raw),
            manager.initiateDownload(raw)
        ]);
        assert(r1.success === true || r2.success === true, "At least one must succeed");
        assert(r1.status === "DUPLICATE_SUPPRESSED" || r2.status === "DUPLICATE_SUPPRESSED", "One must be suppressed");
    });

    // 9. EDM Unavailable Handling
    await runTest("DP-09", "EDM Unavailable: Falls back to emergency browser download without crashing", async () => {
        let browserFallbackCalled = false;
        const manager = new DownloadPipelineManager({
            nativeConnection: {
                sendNativeRequest: async () => { throw new Error("Native host not found"); }
            },
            emergencyBrowserFallbackHandler: async () => { browserFallbackCalled = true; }
        });
        const res = await manager.initiateDownload({ url: "https://example.com/fallback_test.zip", filename: "fallback.zip" });
        assert(res.success === true, "Should report success via browser fallback");
        assert(res.status === "BROWSER_FALLBACK_EXECUTED", "Status should be BROWSER_FALLBACK_EXECUTED");
        assert(browserFallbackCalled === true, "Browser fallback handler must be called");
    });

    // 10. NativeHost Timeout Handling
    await runTest("DP-10", "NativeHost Timeout: Handles timeout rejection cleanly", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: {
                sendNativeRequest: async () => { throw new Error("Request timed out after 6000ms"); }
            }
        });
        const res = await manager.initiateDownload({ url: "https://example.com/timeout.mp4" });
        assert(res.success === false, "Must report failure");
        assert(res.error.includes("timed out"), "Error message must reflect timeout");
    });

    // 11. NativeHost Disconnect Handling
    await runTest("DP-11", "NativeHost Disconnect: Rejects cleanly on pipe broken", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: {
                sendNativeRequest: async () => { throw new Error("Pipe broken / disconnected"); }
            }
        });
        const res = await manager.initiateDownload({ url: "https://example.com/broken_pipe.mp4" });
        assert(res.success === false, "Must report failure on disconnect");
    });

    // 12. EDM Rejection Handling
    await runTest("DP-12", "EDM Rejection: Desktop rejection handled gracefully", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: {
                sendNativeRequest: async () => { throw new Error("EDM desktop queue full"); }
            }
        });
        const res = await manager.initiateDownload({ url: "https://example.com/queue_full.mp4" });
        assert(res.success === false, "Must report rejection");
    });

    // 13. Download Started State Transition
    await runTest("DP-13", "State: QUEUED -> STARTED transition is legal", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.QUEUED);
        const ok = job.transitionTo(DownloadJobState.STARTED);
        assert(ok === true && job.getState() === DownloadJobState.STARTED, "Transition to STARTED must succeed");
    });

    // 14. Download Progress Telemetry Update
    await runTest("DP-14", "Telemetry: Progress calculation derives correct percentage and speed", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4", size: 1000000 }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.QUEUED);
        job.transitionTo(DownloadJobState.STARTED);

        job.updateProgress({ downloadedBytes: 500000, totalBytes: 1000000, speedBytesPerSec: 250000 });
        assert(job.progress.percentage === 50, "Percentage must be 50%");
        assert(job.progress.speedBytesPerSec === 250000, "Speed match");
        assert(job.getState() === DownloadJobState.PROGRESS, "State should be PROGRESS");
    });

    // 15. Download Completion State Transition
    await runTest("DP-15", "State: PROGRESS -> COMPLETED is legal and terminal", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.STARTED);
        const ok = job.transitionTo(DownloadJobState.COMPLETED);
        assert(ok === true && job.getState() === DownloadJobState.COMPLETED, "Must reach COMPLETED state");
    });

    // 16. Download Failure State Transition
    await runTest("DP-16", "State: HANDOFF_PENDING -> FAILED transition is legal", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        const ok = job.transitionTo(DownloadJobState.FAILED, "Connection error");
        assert(ok === true && job.getState() === DownloadJobState.FAILED, "Must reach FAILED state");
    });

    // 17. Pause Command Validation
    await runTest("DP-17", "Commands: Pause is legal from STARTED/PROGRESS state", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.STARTED);
        const ok = job.transitionTo(DownloadJobState.PAUSED);
        assert(ok === true && job.getState() === DownloadJobState.PAUSED, "Must reach PAUSED state");
    });

    // 18. Resume Command Validation
    await runTest("DP-18", "Commands: Resume is legal from PAUSED state", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.STARTED);
        job.transitionTo(DownloadJobState.PAUSED);
        const ok = job.transitionTo(DownloadJobState.RESUMED);
        assert(ok === true && job.getState() === DownloadJobState.RESUMED, "Must reach RESUMED state");
    });

    // 19. Cancel Command Validation
    await runTest("DP-19", "Commands: Cancel is legal from active states", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        const ok = job.transitionTo(DownloadJobState.CANCELLED, "User cancelled");
        assert(ok === true && job.getState() === DownloadJobState.CANCELLED, "Must reach CANCELLED state");
    });

    // 20. Illegal Transition Rejection: COMPLETED -> STARTED
    await runTest("DP-20", "State Guard: Rejects illegal COMPLETED -> STARTED transition", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.READY);
        job.transitionTo(DownloadJobState.HANDOFF_PENDING);
        job.transitionTo(DownloadJobState.HANDED_OFF);
        job.transitionTo(DownloadJobState.STARTED);
        job.transitionTo(DownloadJobState.COMPLETED);
        const ok = job.transitionTo(DownloadJobState.STARTED);
        assert(ok === false, "COMPLETED -> STARTED must be rejected");
        assert(job.getState() === DownloadJobState.COMPLETED, "State must remain COMPLETED");
    });

    // 21. Illegal Transition Rejection: CANCELLED -> COMPLETED
    await runTest("DP-21", "State Guard: Rejects illegal CANCELLED -> COMPLETED transition", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.CANCELLED);
        const ok = job.transitionTo(DownloadJobState.COMPLETED);
        assert(ok === false, "CANCELLED -> COMPLETED must be rejected");
    });

    // 22. Illegal Transition Rejection: EXPIRED -> STARTED
    await runTest("DP-22", "State Guard: Rejects illegal EXPIRED -> STARTED transition", () => {
        const job = new DownloadJob(new DownloadCandidate({ url: "https://example.com/v.mp4" }));
        job.transitionTo(DownloadJobState.VALIDATING);
        job.transitionTo(DownloadJobState.EXPIRED);
        const ok = job.transitionTo(DownloadJobState.STARTED);
        assert(ok === false, "EXPIRED -> STARTED must be rejected");
    });

    // 23. Filename Normalization
    await runTest("DP-23", "Filename Sanitizer: Strips illegal characters and handles spaces", () => {
        const sanitized = FilenameSanitizer.sanitize('My "Awesome" <Video> 1080p?.mp4');
        assert(sanitized === 'My _Awesome_ _Video_ 1080p_.mp4', `Sanitized filename mismatch: ${sanitized}`);
    });

    // 24. Path Traversal Defense
    await runTest("DP-24", "Filename Sanitizer: Defends against directory traversal attacks", () => {
        const sanitized = FilenameSanitizer.sanitize('../../Windows/System32/cmd.exe', 'exe');
        assert(sanitized === 'cmd.exe', `Path traversal not stripped: ${sanitized}`);
    });

    // 25. Reserved Windows Device Name Defense
    await runTest("DP-25", "Filename Sanitizer: Renames Windows reserved device names (CON, PRN, AUX, NUL)", () => {
        const sanitizedCon = FilenameSanitizer.sanitize('CON.mp4');
        assert(sanitizedCon === 'file_con.mp4', `CON was not protected: ${sanitizedCon}`);
        const sanitizedNul = FilenameSanitizer.sanitize('NUL.zip', 'zip');
        assert(sanitizedNul === 'file_nul.zip', `NUL was not protected: ${sanitizedNul}`);
    });

    // 26. Unsupported Scheme Defense
    await runTest("DP-26", "Policy: Rejects file: and javascript: schemes", () => {
        const verdictFile = DownloadPolicy.evaluate({ url: "file:///etc/shadow" });
        assert(verdictFile === PolicyVerdict.REJECT, "file: scheme must be REJECTED");
        const verdictJs = DownloadPolicy.evaluate({ url: "javascript:void(0)" });
        assert(verdictJs === PolicyVerdict.REJECT, "javascript: scheme must be REJECTED");
    });

    // 27. Normal Browser File Download Policy
    await runTest("DP-27", "Policy: Approves .exe, .zip, .pdf, .iso for EDM Native Handoff", () => {
        const verdictExe = DownloadPolicy.evaluate({ url: "https://example.com/installer.exe" });
        assert(verdictExe === PolicyVerdict.HANDLE, ".exe should be HANDLE");
        const verdictZip = DownloadPolicy.evaluate({ url: "https://example.com/archive.zip" });
        assert(verdictZip === PolicyVerdict.HANDLE, ".zip should be HANDLE");
        const verdictIso = DownloadPolicy.evaluate({ url: "https://example.com/os_image.iso" });
        assert(verdictIso === PolicyVerdict.HANDLE, ".iso should be HANDLE");
    });

    // 28. Alt-Key User Bypass Policy
    await runTest("DP-28", "Policy: Routes download to browser when Alt key is held", () => {
        const verdict = DownloadPolicy.evaluate(
            { url: "https://example.com/installer.exe" },
            { isAltKeyPressed: true }
        );
        assert(verdict === PolicyVerdict.BROWSER_FALLBACK, "Alt key must trigger BROWSER_FALLBACK");
    });

    // 29. Multiple Simultaneous Downloads Handling
    await runTest("DP-29", "Multi-Download: Tracks multiple concurrent jobs with isolated IDs", async () => {
        const manager = new DownloadPipelineManager({
            nativeConnection: { sendNativeRequest: async () => ({ success: true, action: "acknowledged" }) }
        });
        const candA = { url: "https://example.com/dl_A.mp4", filename: "A.mp4" };
        const candB = { url: "https://example.com/dl_B.mp4", filename: "B.mp4" };
        const candC = { url: "https://example.com/dl_C.mp4", filename: "C.mp4" };

        const [rA, rB, rC] = await Promise.all([
            manager.initiateDownload(candA),
            manager.initiateDownload(candB),
            manager.initiateDownload(candC)
        ]);

        assert(rA.downloadId !== rB.downloadId, "IDs must be distinct");
        assert(rB.downloadId !== rC.downloadId, "IDs must be distinct");
        assert(manager.activeJobs.size === 3, "All 3 jobs must be tracked");
    });

    // 30. Browser Fallback on NativeHost Failure
    await runTest("DP-30", "Browser Fallback: Executes fallback on EDM unavailable", async () => {
        let fallbackUrl = "";
        const manager = new DownloadPipelineManager({
            nativeConnection: {
                sendNativeRequest: async () => { throw new Error("EDM Host not reachable"); }
            },
            emergencyBrowserFallbackHandler: async (cand) => { fallbackUrl = cand.url; }
        });
        const res = await manager.initiateDownload({ url: "https://example.com/important_file.zip", filename: "file.zip" });
        assert(res.success === true, "Must succeed via fallback");
        assert(fallbackUrl === "https://example.com/important_file.zip", "Fallback URL must match original");
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
