# STAGE 5 — PROMPT 4: ORPHAN CODE & SERVICE LIFECYCLE REPORT

**Audit Date:** 2026-08-15  
**Auditor:** Principal Software Architect  
**Scope:** Complete solution scan of all services and helper classes in `EDM/Services`.  

---

## 1. Concrete Production Call-Site Verification

Every active service in the EDM ecosystem has been mapped to its concrete call-sites:

| Service Name | Registered in DI? | Concrete Call-Site | Verified Status |
| :--- | :---: | :--- | :---: |
| `DownloadService` | Yes | `DownloadOrchestrator.cs`, `DownloadProgressWindow.xaml.cs` | **ACTIVE** |
| `DownloadOrchestrator` | Trans | `DownloadProgressWindow.xaml.cs`, `AddUrlWindow.xaml.cs` | **ACTIVE** |
| `MultiPartDownloader` | Static | `MultiPartAdapter.cs` $\to$ `DownloadOrchestrator.cs` | **ACTIVE** |
| `AdaptiveConnectionManager` | Instantiated | `DownloadOrchestrator.cs` | **ACTIVE** |
| `ControlPlaneClient` | Yes | `App.xaml.cs`, `DownloadOrchestrator.cs`, `UpdateService.cs`| **ACTIVE** |
| `ControlPlaneTelemetryService` | Yes | `App.xaml.cs`, `DownloadOrchestrator.cs` | **ACTIVE** |
| `SecureCredentialVault` | Static | `ControlPlaneClient.cs`, `SettingsService.cs` | **ACTIVE** |
| `SettingsService` | Yes | `App.xaml.cs`, `ThemeService.cs`, `DownloadOrchestrator.cs` | **ACTIVE** |
| `UpdateService` | Yes | `App.xaml.cs`, `SettingsWindow.xaml.cs` | **ACTIVE** |
| `NativeIpcServer` | Instantiated | `App.xaml.cs` | **ACTIVE** |
| `BrowserExtensionInstaller` | Static | `App.xaml.cs` | **ACTIVE** |
| `SmartFileOrganizerService` | Instantiated | `DownloadOrchestrator.cs` (Post-download pipeline) | **ACTIVE** |
| `SubtitleAutoDownloaderService`| Instantiated | `DownloadOrchestrator.cs` (Post-download pipeline) | **ACTIVE** |
| `CloudHandoffUploadService` | Instantiated | `DownloadOrchestrator.cs` (Post-download pipeline) | **ACTIVE** |
| `AutoExtractorAndStreamService` | Instantiated | `DownloadOrchestrator.cs` (Post-download pipeline) | **ACTIVE** |
| `DownloadAnalyticsEngine` | Instantiated | `DownloadOrchestrator.cs` (Post-download pipeline) | **ACTIVE** |

---

## 2. Conclusion
- **Total Orphan Services Identified:** 0
- **Total Unreachable Features:** 0
- All 16 primary subsystem services are wired into concrete execution paths.
