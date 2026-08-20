# EDM — STAGE 4 FORENSIC AUDIT (BEFORE MODIFICATIONS)
## Complete Download Path Trace, Seam Analysis, and Architectural Root Causes

**Document Version:** 1.0.0-STAGE-4-FORENSIC-BEFORE  
**Date:** 2026-08-17  
**Auditor:** Lead Production Software Engineer  
**Status:** FORENSIC BASELINE ESTABLISHED  

---

## 1. End-to-End Download Path Trace

```
[Browser Content Script] (content.js)
  │ Format Selection: e.g. 2160p 4K MP4 AV1 + Matching Audio
  │ Compute DownloadIdentity = Hash(url + quality + filename + directUrl)
  │ Send runtime message: START_EDM_DOWNLOAD (22 contract fields)
  ▼
[Background Service Worker] (background.js)
  │ Deduplicate correlationId (3000ms window)
  │ Stdio Native Messaging Framing (32-bit LE prefix)
  │ Target: com.edm.downloader (EDM.NativeHost.exe)
  ▼
[Native Messaging Host] (EDM.NativeHost.exe -> Program.cs)
  │ Read exact bytes from Stdin
  │ Deserialize NativeMessageRequest
  │ Connect to Named Pipe: \\.\pipe\EDM_Native_IPC_Pipe
  │ (Fallback: launch EDM.exe --handoff <base64>)
  ▼
[EDM Desktop Application] (App.xaml.cs -> HandleIpcHandoffAsync)
  │ Deduplicate via _activeIpcWindows[downloadIdentity]
  │ If active window exists: Restore, Activate(), Focus() -> Return True
  │ Else: Construct DownloadItem (all 22 fields)
  │ Register in _activeIpcWindows[downloadIdentity]
  │ Instantiate DownloadProgressWindow
  ▼
[Download Execution & Routing]
  │ DownloadProgressWindow / DownloadOrchestrator
  │ MultiPartDownloader (Turbo Segmented Engine)
  │ MediaMergeService (Dual-stream Video+Audio parallel download & FFmpeg mux)
  ▼
[Authoritative Progress Pipeline]
  │ IProgress<DownloadProgressInfo> -> ProgressThrottler (100ms)
  │ Live Area Wave Graph (30 FPS)
  │ Status Transitions: Connecting -> Downloading -> Merging -> Completed
  ▼
[Final File Validation]
  │ Verify File.Exists(SavePath) && FileInfo.Length > 0
  │ Clean temporary files (.tmp)
```

---

## 2. Transition-by-Transition Analysis

| Transition Step | Input | Output | Identity Preserved | Thread / Context | Cancellation Handling | Error Handling | Progress Source |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. content.js -> background.js** | User format click `{url, quality, variant}` | Runtime message `START_EDM_DOWNLOAD` | `DownloadIdentity` calculated deterministically | Browser UI thread | None (in-flight set) | Stale response protection | Initial UI feedback |
| **2. background.js -> Native Host** | `START_EDM_DOWNLOAD` payload | Stdio 32-bit LE binary stream | `DownloadIdentity` in JSON | MV3 Service Worker | 6000ms timeout | Fallback to HTTP endpoint | None |
| **3. Native Host -> App.xaml.cs** | Stdio bytes | `IpcHandoffPayload` via Named Pipe | `DownloadIdentity` in JSON | NativeHost async loop | Named Pipe timeout (1000ms) | Launch `EDM.exe --handoff` | None |
| **4. App.xaml.cs -> DownloadItem** | `IpcHandoffPayload` | `DownloadItem` in ViewModel | `item.DownloadIdentity` | WPF Dispatcher thread | Window Closed unbinds | Try/Catch logged | Initial 0.0% |
| **5. App.xaml.cs -> Window** | `DownloadItem` | `DownloadProgressWindow` | Looked up in `_activeIpcWindows` | UI Thread | Unregisters on Close | Dialog on error | Subscribed to progress |
| **6. Window -> Execution Engine** | `DownloadItem` | Stream downloads & merge | Preserved in `item` | Background Task | `CancellationToken` | Formatted error card | `DownloadProgressInfo` |
| **7. Execution Engine -> Output File** | Stream URLs | Merged / segmented output file | File path matches item | Thread pool / Worker threads | Tokens checked on every block | Exception thrown & caught | Byte count accumulator |

---

## 3. Identification of Architectural Seams & Root Causes

### Root Cause 1: Independent Re-Resolution in DownloadProgressWindow
- **Observed Code:** In `DownloadProgressWindow.xaml.cs` lines 284–487, when a YouTube URL is downloaded, `DownloadProgressWindow` creates a new `YoutubeClient` and calls `youtube.Videos.Streams.GetManifestAsync(...)`, searching for stream resolutions and independently picking streams!
- **Impact:** The browser extension's selected representation (e.g. 2160p 4K AV1 direct stream or yt-dlp format argument) is discarded or re-resolved independently. If `YoutubeExplode` fails or picks a different stream, a quality downgrade or double-download occurs.
- **Remediation:** Remove independent media resolution from `DownloadProgressWindow`. All downloads must be routed authoritatively through `DownloadOrchestrator` and `MediaMergeService` using the exact `VideoUrl`, `AudioUrl`, and `RequiresFfmpegMerge` provided in `DownloadItem`.

### Root Cause 2: Non-Progressive Dual-Stream Adaptive Merging
- **Observed Code:** In `MediaMergeService.cs`, downloading of separate video and audio streams was using basic `HttpClient.GetAsync` into temporary files without segmented downloading, speed calculation, or unified progress reporting.
- **Impact:** Dual-stream adaptive downloads (e.g. 4K Video + Opus Audio) showed uninformative progress and did not utilize EDM's Turbo engine.
- **Remediation:** Enhance `MediaMergeService` to accept `IProgress<DownloadProgressInfo>`, download video and audio in parallel with combined progress accounting for total bytes (`VideoBytes + AudioBytes`), display "Merging Audio & Video (FFmpeg)..." during muxing, and check FFmpeg process exit code strictly.

### Root Cause 3: Temporary File Name Collision Risk
- **Observed Code:** `MediaMergeService` named temporary files as `outputPath + ".video.tmp"` and `outputPath + ".audio.tmp"`.
- **Impact:** If two concurrent downloads target the same filename in different categories or save locations with identical names, temp file collisions could corrupt streams.
- **Remediation:** Incorporate unique job identity or Guid into temporary filenames (`$"{outputPath}.{Guid.NewGuid():N}.video.tmp"`).

### Root Cause 4: Duplicate Download Start Triggers
- **Observed Code:** `DownloadProgressWindow_Loaded` called `StartDownloadProcessCoreAsync()`, while `App.xaml.cs` ALSO called `StartDownloadForItemAsync()`.
- **Impact:** Multiple start triggers caused race conditions on initialization.
- **Remediation:** Establish a single authoritative entry point (`StartDownloadForItemAsync`) guarded by atomic state transition.

### Root Cause 5: Final File Verification
- **Observed Code:** Status was marked "Completed" upon HTTP completion before verifying that the merged file exists on disk and is non-empty.
- **Impact:** If FFmpeg failed silently or disk ran out of space, the UI could report fake success.
- **Remediation:** Add mandatory post-download verification: `File.Exists(savePath) && new FileInfo(savePath).Length > 0`. If false, throw `InvalidOperationException("Output file creation failed or is 0 bytes.")`.

---

**STAGE 4 FORENSIC AUDIT BEFORE MODIFICATION COMPLETE.**
