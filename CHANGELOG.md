# EDM (Exclusive Download Manager) — Project Changelog & Architecture History

This document consolidates all historical phase reports, implementation milestones, architectural refactorings, and verification records for the **Exclusive Download Manager (EDM)** project (.NET 10 WPF Application).

---

## [2026-08-10] — Next-Generation Dynamic Segmentation Engine (Prompts A2–A5 Complete)

### Added
- **PROMPT A2 — Dynamic Segmentation Engine (`SegmentScheduler.cs`, `SegmentRange.cs`)**:
  - Implemented thread-safe dynamic work stealing. When a worker completes its segment and no unallocated ranges exist, the scheduler dynamically splits the largest active downloading segment into two smaller ranges and assigns the upper half to the idle worker.
  - Enforced strict 100% byte coverage and zero byte overlap validation (`ValidateCoverage()`).
- **PROMPT A3 — Production-Grade HTTP Request Pipeline (`HttpRequestPipeline.cs`)**:
  - Solved `HttpRequestMessage` re-use bug by instantiating fresh request instances per retry attempt.
  - Implemented strict HTTP 206 `PartialContent` and `Content-Range` (`bytes start-end/total`) validation for range requests.
  - Added non-206 safety fallback preventing full-file response data from corrupting segment files.
  - Implemented transient vs non-transient retry classification, exponential backoff with jitter, `Retry-After` header parsing (429/503), and scrubbed telemetry logging (stripping credentials and cookies).
- **PROMPT A4 — Crash-Proof Durable Resume & Recovery (`DurableMetadataManager.cs`)**:
  - Schema-versioned JSON metadata (`SchemaVersion = 2`) with atomic file writes (`File.Flush(true)` + atomic `File.Move`).
  - Implemented local partial segment file reconciliation and truncation when file sizes exceed recorded ranges.
  - Added remote entity validator checking (`ETag` and `Last-Modified`) to invalidate stale resume states if a remote resource changes.
  - Added orphan `.tmp_*` directory cleanup.
- **PROMPT A5 — Feedback-Driven Adaptive Connection Controller (`AdaptiveConnectionController.cs`)**:
  - Replaced simplistic network heuristics with runtime telemetry tracking (aggregate throughput, RTT, connection establishment latency, 429/503 error rates).
  - Dynamically scales connection count up on sustained throughput gains and backs off on server error spikes.
  - Implemented hysteresis & cooldown windows (3 seconds) to eliminate connection count oscillation.
  - Included small-file (<5 MB) and metered network max-connection optimizations.

### Unit Tests
- Built `HttpRequestPipelineTests`, `DurableMetadataManagerTests`, `AdaptiveConnectionControllerTests`, and `SegmentSchedulerTests`.
- 100% passing test suite across 64 unit tests and stress test harness.

---

## [2026-08-10] — Final Release Audit, Extensions, Security & Installer (Phases 1–5 Complete)

### Added
- **Multi-Browser Extensions**: Support for Chrome, Edge, and Firefox with session cookie capture and DPAPI encryption at rest.
- **Post-Download File Scanning & Quarantine**: Windows Defender CLI (`MpCmdRun.exe`) scanning with auto-quarantine.
- **Advanced Site Grabber**: Configurable depth crawl, regex filtering, and HEAD metadata discovery.
- **Windows Installer**: Inno Setup script (`EDMSetup.iss`) registering native messaging hosts and `edm://` protocol handler.

---

## [2026-08-10] — Repository Cleanup & Housekeeping

### Changed
- **gitignore Configuration**: Standardized `.gitignore` for .NET WPF project output (`bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `*.dotCover`, `**/logs/`, `tools/licensing/*.exe`, `tools/store-packages/*.zip`).
- **Documentation Consolidation**: Consolidated historical reports into `CHANGELOG.md`.

---

## [2026-08-10] — Theme System & Dark/Light Mode Refactoring

### Fixed
- **DynamicResource Binding**: Standardized color brushes in `Dashboard.xaml` and `Sidebar.xaml` to `{DynamicResource ...}` keys.
