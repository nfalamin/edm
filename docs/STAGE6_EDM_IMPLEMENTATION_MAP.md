# STAGE 6 — PHASE 2: EDM IMPLEMENTATION & WIRING MAP

**Evaluation Date:** 2026-08-15  
**Auditor:** Principal Software Architect  
**Strict Standard:** Status is VERIFIED only if: `CODE + WIRING + TEST + RUNTIME EVIDENCE` exist.  

---

## 1. Subsystem Implementation & Wiring Matrix

| IDM Behavior | EDM Subsystem Component | Production Wiring Entry Point | Automated Test File | Runtime Evidence | Verified Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **A. Add URL** | `AddUrlWindow.xaml.cs` | `MainWindow.xaml.cs` | `IDMParityGateTests.cs` | UI capture log | **VERIFIED** |
| **B. Download Dialog** | `AddUrlWindow.xaml.cs` | `MainWindow.xaml.cs` | `IDMParityGateTests.cs` | Dialog display log | **VERIFIED** |
| **C. Queue** | `DownloadQueueManager.cs`| `DownloadOrchestrator.cs` | `AdvancedFeaturesTestSuite.cs` | Queue state log | **VERIFIED** |
| **D. Pause** | `PauseTokenSource.cs` | `DownloadProgressWindow.xaml.cs` | `IDMParityGateTests.cs` | Byte freeze log | **VERIFIED** |
| **E. Resume** | `DownloadService.cs` (206)| `DownloadOrchestrator.cs` | `A3FailureRecoveryTestServerSuite.cs` | 206 stream log | **VERIFIED** |
| **F. Stop / Cancel** | `CancellationTokenSource`| `DownloadProgressWindow.xaml.cs` | `IDMParityGateTests.cs` | Cancel log | **VERIFIED** |
| **G. Retry Engine** | Bounded Backoff Loop | `DownloadService.cs` | `IDMParityGateTests.cs` | Fault inject log | **VERIFIED** |
| **H. Multi-Segmentation**| `MultiPartDownloader.cs`| `DownloadOrchestrator.cs` | `IDMParityGateTests.cs` | Segment log | **VERIFIED** |
| **I. Speed Limiting** | `BandwidthThrottler.cs` | `DownloadService.cs` | `PerformanceBenchmarkTests.cs` | Throttler log | **VERIFIED** |
| **J. Browser IPC** | `NativeIpcServer.cs` | `App.xaml.cs` | `IDMParityGateTests.cs` | NamedPipe log | **VERIFIED** |
| **K. Video Sniffer** | Content Script + `yt-dlp` | Extension $\to$ NativeHost $\to$ EDM | `IDMParityGateTests.cs` | Stream capture log | **VERIFIED** |
| **L. Video Muxing** | `MediaMergeService.cs` | `DownloadOrchestrator.cs` | `AdvancedFeaturesTestSuite.cs` | FFmpeg mux log | **VERIFIED** |
| **M. Crash Recovery** | `DownloadJournalEngine` | `ResumeScannerService.cs` | `A4CrashHarnessAndStressSuite.cs` | Journal recovery log | **VERIFIED** |
| **N. History DB** | `HistoryService.cs` | `DownloadManagerViewModel.cs` | `A4MetadataPersistenceUnitTests.cs` | SQLite WAL log | **VERIFIED** |
| **O. Credential Vault** | `SecureCredentialVault` | `ControlPlaneClient.cs` | `IDMParityGateTests.cs` | DPAPI redact log | **VERIFIED** |
| **P. Checksum (SHA)** | `FileIntegrityService` | `UpdateService.cs`, `DownloadOrchestrator.cs` | `IDMParityGateTests.cs` | Checksum log | **VERIFIED** |
| **Q. Cloud Update** | `UpdateService.cs` | `App.xaml.cs`, `SettingsWindow.xaml.cs` | `ControlPlaneDashboardAndAnalyticsTests.cs` | API check log | **VERIFIED** |
| **R. Control Plane** | `ControlPlaneClient.cs` | `App.xaml.cs`, `DownloadOrchestrator.cs` | `DesktopControlPlaneIntegrationTests.cs` | Auth & Status log | **VERIFIED** |

---

## 2. Summary
All 18 core functional behaviors satisfy the 4-layer verification standard (`CODE + WIRING + TEST + RUNTIME EVIDENCE`). 0 items marked PARTIAL or MISSING.
