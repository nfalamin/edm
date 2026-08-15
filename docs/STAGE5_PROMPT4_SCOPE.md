# STAGE 5 — PROMPT 4: SCOPE FREEZE & AUDIT SPECIFICATION

**Document Version:** 4.0.0  
**Date:** 2026-08-15  
**Strict Policy:** NO NEW FEATURE EXPANSION. Strictly audit, repair, wire, test, runtime-verify, and document existing EDM capabilities.  

---

## 1. Existing EDM Functionality to be Preserved & Strengthened
1. **Core Download Engine**: Multi-threaded segmented download engine (`MultiPartDownloader`, `SegmentDownloader`, `DownloadService`, `DownloadOrchestrator`) with 206 Partial Content range probing, adaptive socket pooling, zero-allocation buffering, and resume capabilities.
2. **Speed Limiting & Dynamic Throttling**: Token-bucket bandwidth limiter (`BandwidthThrottler`, `AdaptiveConnectionManager`) with time-based scheduling.
3. **Database & Persistence**: SQLite WAL history (`DownloadHistoryRecorder`, `DownloadJournalEngine`, `HistoryService`) with crash recovery and resume detection.
4. **Browser Extensions**: Chrome, Edge, and Firefox Manifest V3 extensions with IDM-style Native Messaging IPC and video detection.
5. **Security & Control Plane**: DPAPI zero-trust credential vault, JWT session management, Argon2id authentication, privacy-safe `InstallationId`, server ban enforcement, and non-blocking telemetry queuing.
6. **Smart Automation Pipeline**: Subtitle downloader, auto-extractor, file organizer, and media merge pipeline (`MediaMergeService`, `YtDlpService`).

---

## 2. Existing IDM-Parity Gaps to be Investigated & Repaired
1. **Zero Fake/Sample Download State**: Remove hardcoded sample download entries from all production ViewModels.
2. **Progress Window Binding Verification**: Verify all bindings in `DownloadProgressWindow` (filename, speed, ETA, percent, downloaded/total bytes, connection segments) reflect live engine states.
3. **Pause / Resume Hard Verification**: Confirm byte-level freezing during pause and seamless continuation upon resume.
4. **Browser Native Messaging Handoff**: Verify real-world URL handoff from Chrome/Edge/Firefox native host to EDM desktop window.
5. **Update Pipeline Safety**: Verify `CheckControlPlaneUpdateAsync` end-to-end with SHA-256 and Authenticode validation.
6. **Exception Handling & Empty Catch Cleanup**: Audit all empty catch blocks, converting critical failure points to structured logging and user-visible errors.
7. **Orphan Code Audit**: Document and inventory all services, ensuring concrete call-sites for all production components.

---

## 3. Features Explicitly Excluded
- Third-party cloud vendor storage engines (beyond standard HTTP handoff).
- Unrelated enterprise subsystems or protocol expansions.
- Cosmetic mock themes.
