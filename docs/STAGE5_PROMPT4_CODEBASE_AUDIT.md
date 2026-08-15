# STAGE 5 — PROMPT 4: COMPLETE CODEBASE DEPENDENCY & CALL-GRAPH AUDIT

**Audit Date:** 2026-08-15  
**Scope:** `EDM`, `EDM.ControlPlane.Api`, `EDM.ControlPlane.Dashboard`, `EDM.NativeHost`, `EDM.Tests`  
**Total Projects:** 5 Projects in Solution `EDM.slnx`  

---

## 1. Component Dependency & Runtime Reachability Matrix

| Component | Exists | DI Registered | Caller | Runtime Reachable | Tested | Production Used | Status |
| :--- | :---: | :---: | :--- | :---: | :---: | :---: | :---: |
| `DownloadService` | Yes | Yes (`App.xaml.cs`) | `DownloadOrchestrator`, `DownloadProgressWindow` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `DownloadOrchestrator` | Yes | Trans (`AddUrlWindow`) | `DownloadProgressWindow`, `AddUrlWindow` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `MultiPartDownloader` | Yes | No (Static/Helper) | `MultiPartAdapter`, `DownloadOrchestrator` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `DownloadHistoryRecorder`| Yes | No (Static SQLite) | `DownloadOrchestrator`, `HistoryServiceFacade` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `HistoryServiceFacade` | Yes | Yes (`IHistoryProvider`) | `DownloadManagerViewModel`, `AddUrlViewModel` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `SettingsService` | Yes | Yes (`ISettingsService`) | All Services & ViewModels | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `SecureCredentialVault`| Yes | No (Static DPAPI) | `ControlPlaneClient`, `SettingsService` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `ControlPlaneClient` | Yes | Yes (`App.xaml.cs`) | `App.OnStartup`, `DownloadOrchestrator`, `UpdateService` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `ControlPlaneTelemetryService`| Yes | Yes (`App.xaml.cs`)| `App.OnStartup`, `DownloadOrchestrator` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `NativeIpcServer` | Yes | Instantiated in App | `App.OnStartup` (NamedPipe Listener) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `BrowserExtensionInstaller`| Yes | Instantiated in App | `App.OnStartup` (Registry Manifests) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `UpdateService` | Yes | Trans (`SettingsWindow`) | `App`, `SettingsWindow`, `ControlPlaneClient` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `FileIntegrityService` | Yes | Instantiated in Update | `UpdateService`, `DownloadOrchestrator` | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `SmartFileOrganizerService`| Yes| Instantiated in Orch | `DownloadOrchestrator` (Post-download) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `SubtitleAutoDownloaderService`| Yes| Instantiated in Orch | `DownloadOrchestrator` (Post-download) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `CloudHandoffUploadService`| Yes| Instantiated in Orch | `DownloadOrchestrator` (Post-download) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `AutoExtractorAndStreamService`| Yes| Instantiated in Orch| `DownloadOrchestrator` (Post-download) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |
| `DownloadAnalyticsEngine`| Yes| Instantiated in Orch | `DownloadOrchestrator` (Post-download) | Yes | Yes | Yes | **ACTIVE / PRODUCTION** |

---

## 2. Codebase Stubs & Fake Data Scan Results

1. **Hardcoded Fake Data Elimination**:
   - `LoadSampleData()` in `DownloadManagerViewModel.cs` was completely deleted.
   - Verified that `AllDownloads` initializes purely from SQLite database history on startup.
2. **`NotImplementedException` Scan**:
   - 0 instances of `throw new NotImplementedException()` in production code.
3. **Dead UI Buttons**:
   - Verified that every button in `MainWindow.xaml`, `AddUrlWindow.xaml`, and `DownloadProgressWindow.xaml` is bound to concrete RelayCommands.
