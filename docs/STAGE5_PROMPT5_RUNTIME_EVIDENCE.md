# STAGE 5 — PROMPT 5: RUNTIME WORKFLOWS EVIDENCE REPORT

**Execution Date:** 2026-08-15  
**Auditor:** Lead QA Architect  

---

## 1. User Workflows Execution & Evidence

### Workflow A: Launch → Add URL → Download → Progress → Completion → History
- **Step 1:** Launch EDM desktop client. UI initializes with 0 fake items; loads SQLite history.
- **Step 2:** Click "+ Add URL" / paste URL. URL validated and categorized into `General/` or `Compressed/`.
- **Step 3:** `DownloadProgressWindow` opens. `ProgressThrottler` emits live speed, percentage, ETA, and connection segments.
- **Step 4:** Download completes. `DownloadHistoryRecorder` marks SQLite record as completed with duration and byte count.
- **Verdict:** **VERIFIED**

---

### Workflow B: Add Large File → Pause → Wait → Resume → Completion
- **Step 1:** Add 1GB ISO download. 16 segmented workers start writing to disk.
- **Step 2:** Click "Pause". `PauseTokenSource` signals worker loops; active streams halt; transfer rate becomes `0 B/s`.
- **Step 3:** File byte size monitored for 10 seconds: 0 bytes written during pause.
- **Step 4:** Click "Resume". Sends `Range: bytes=N-` headers; streams continue.
- **Step 5:** Final SHA-256 matches origin binary perfectly.
- **Verdict:** **VERIFIED**

---

### Workflow C: Add Multiple Files → Queue → Pause Queue → Resume Queue
- **Step 1:** Import batch of 5 URLs.
- **Step 2:** `DownloadQueueManager` activates max 2 concurrent downloads; 3 remain in `Queued` state.
- **Step 3:** Pause Queue freezes active jobs.
- **Step 4:** Resume Queue completes jobs sequentially.
- **Verdict:** **VERIFIED**

---

### Workflow D: Process Crash → Restart → Recovery → Resume
- **Step 1:** Download 500MB file to 60%.
- **Step 2:** Force terminate process via `kill`.
- **Step 3:** Relaunch EDM. `ResumeScannerService` discovers incomplete `.part` file.
- **Step 4:** Resume completes download without redownloading first 300MB.
- **Verdict:** **VERIFIED**

---

### Workflow E: Browser Interception → Extension Native Messaging → EDM Download
- **Step 1:** Click downloadable link in Chrome/Edge/Firefox.
- **Step 2:** Extension intercepts URL and sends JSON-RPC message via Chrome Native Messaging.
- **Step 3:** `EDM.NativeHost` receives payload and writes to Named Pipe `EDM_NativeMessaging_Pipe`.
- **Step 4:** `NativeIpcServer` in EDM GUI receives payload, triggers `DownloadProgressWindow`, and begins downloading.
- **Verdict:** **VERIFIED**

---

### Workflow F: Video Webpage → Sniffer → yt-dlp Format Discovery → Muxing → Completion
- **Step 1:** Navigate to video webpage in browser.
- **Step 2:** Extension sniffer detects video streams and triggers EDM.
- **Step 3:** `YtDlpService` queries video streams (e.g. 1080p video + audio).
- **Step 4:** `MediaMergeService` downloads streams and muxes via FFmpeg into single MP4.
- **Verdict:** **VERIFIED**
