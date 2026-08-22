# ORPHAN CODE AUDIT: EXCLUSIVE DOWNLOAD MANAGER (EDM)

**Document Type:** Orphan Code & Subsystem Wiring Audit  
**Audit Date:** 2026-08-15  
**Auditor:** Senior Windows Download-Manager Architect & .NET 10 WPF Engineer  

---

## 1. Classification Guidelines

- **USED**: Fully wired in DI, referenced by production workflows, connected to UI or Native Host, and covered by tests.
- **PARTIALLY USED**: Instantiated in DI or helper methods, but missing direct user-facing triggers or specific UI bindings.
- **ORPHAN**: Class exists in `EDM/Services` or `EDM/Views`, has valid logic, but is not instantiated in `App.xaml.cs` or called from any user workflow.
- **TEST-ONLY**: Class is only instantiated and referenced in `EDM.Tests`.
- **DEAD / UNREACHABLE**: Code cannot be reached under any condition.

---

## 2. Services Inventory & Classification (109 Services)

| Service Name | Production Ref? | DI Registered? | UI Connected? | Browser / Host? | Tests? | Classification | Action Plan |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- | :--- |
| `DownloadService` | Yes | Yes | Yes | Yes | Yes | **USED** | Core pipeline |
| `MultiPartDownloader` | Yes | Direct | Yes | Yes | Yes | **USED** | Multi-segment core |
| `SegmentWorker` | Yes | Direct | Yes | Yes | Yes | **USED** | Segment streaming |
| `SegmentScheduler` | Yes | Direct | Yes | Yes | Yes | **USED** | Range calculations |
| `ResumeScannerService` | Yes | Yes | Yes | No | Yes | **USED** | Incomplete download recovery |
| `DurableMetadataManager`| Yes | Direct | Yes | Yes | Yes | **USED** | WAL persistence engine |
| `DownloadJournalEngine` | Yes | Direct | Yes | No | Yes | **USED** | Transaction logging |
| `BrowserExtensionInstaller` | Yes | Static | Yes | Yes | Yes | **USED** | Registry setup |
| `NativeIpcServer` | Yes | Direct | Yes | Yes | Yes | **USED** | Browser Named Pipe server |
| `HttpProbeService` | Yes | Direct | Yes | Yes | Yes | **USED** | HTTP HEAD/GET inspector |
| `BandwidthThrottler` | Yes | Singleton | Yes | Yes | Yes | **USED** | Speed throttling |
| `UnifiedBandwidthGovernor` | Yes | Singleton | Yes | No | Yes | **USED** | Global throughput governor |
| `MediaVariantResolver` | Yes | Direct | Yes | Yes | Yes | **USED** | HLS/DASH/Direct resolver |
| `HlsParser` / `DashParser` | Yes | Direct | Yes | Yes | Yes | **USED** | Adaptive playlist parsers |
| `HistoryService` | Yes | Yes | Yes | No | Yes | **USED** | SQLite history provider |
| `SettingsService` | Yes | Yes | Yes | No | Yes | **USED** | Configuration manager |
| `ThemeService` | Yes | Yes | Yes | No | Yes | **USED** | Dark/Light theme skinning |
| `LocalizationService` | Yes | Direct | Yes | No | Yes | **USED** | Multi-language strings |
| `AntivirusScannerService` | Yes | Direct | Yes | No | Yes | **USED** | Defender MpCmdRun scanner |
| `UpdateService` | Yes | Direct | Yes | No | Yes | **USED** | Auto-updater engine |
| `SchedulerService` | Yes | Yes | Yes | No | Yes | **USED** | Timed queue scheduler |
| `ClipboardMonitorService` | Yes | Direct | Yes | No | Yes | **USED** | Windows clipboard listener |
| `SiteGrabberService` | Yes | Direct | **NO** | No | Yes | **PARTIALLY USED** | Wire to `SiteGrabberWindow` in Sidebar |
| `WebCrawlerSubsystem` | Yes | Direct | **NO** | No | Yes | **PARTIALLY USED** | Wire into Site Grabber workflow |
| `UrlPatternExpander` | Yes | Direct | **NO** | No | Yes | **PARTIALLY USED** | Wire to `BatchDownloadWindow` |
| `RemoteZipPreviewService`| Yes | Direct | **NO** | No | Yes | **PARTIALLY USED** | Wire to context menu in DownloadsTable |
| `SecureCredentialVault` | Yes | Direct | **NO** | Yes | Yes | **PARTIALLY USED** | Wire to `SiteLoginsManagerWindow` |
| `LanP2PSharingEngine` | No | No | No | No | Yes | **TEST-ONLY** | Keep isolated for future P2P sharing |
| `CloudHandoffUploadService`| Yes | Direct | No | No | Yes | **PARTIALLY USED** | Wire to secondary cloud upload option |
| `BitTorrentService` | Yes | Direct | No | No | Yes | **PARTIALLY USED** | Wire to torrent magnet ingestion |
| `AutoExtractorAndStreamService` | Yes | Direct | No | No | Yes | **PARTIALLY USED** | Wire to post-download unzipping |
| `MultiSourceMirrorAggregatorService` | Yes | Direct | No | No | Yes | **PARTIALLY USED** | Wire to mirror probing |
| `VpnTunnelOrchestrator` | Yes | Direct | Yes | No | Yes | **USED** | VPN tunnel manager |
| `NativePowerActions` | Yes | Static | Yes | No | Yes | **USED** | Sleep/Shutdown triggers |
| `SoundNotificationService` | Yes | Singleton | Yes | No | Yes | **USED** | Audio event alerts |
| `DownloadListImportExportService` | Yes | Direct | Yes | No | Yes | **USED** | List import/export |

---

## 3. UI Views & Dialogs Wiring Audit (50 Views)

| View Name | File Path | Instantiated In UI? | User Triggers | Status | Required Fix |
| :--- | :--- | :---: | :--- | :--- | :--- |
| `MainWindow` | `Views/MainWindow.xaml` | Yes | App Startup | **USED** | None |
| `Dashboard` | `Views/Dashboard.xaml` | Yes | MainWindow Content | **USED** | None |
| `Sidebar` | `Views/Sidebar.xaml` | Yes | Dashboard Navigation | **USED** | None |
| `DownloadsTable` | `Views/DownloadsTable.xaml` | Yes | Dashboard Main View | **USED** | None |
| `AddUrlWindow` | `Views/AddUrlWindow.xaml` | Yes | Add URL Button in Sidebar/Toolbar | **USED** | None |
| `DownloadProgressWindow` | `Views/DownloadProgressWindow.xaml` | Yes | Start Download action | **USED** | None |
| `SettingsWindow` | `Views/SettingsWindow.xaml` | Yes | Settings Button in Sidebar | **USED** | None |
| `SchedulerWindow` | `Views/SchedulerWindow.xaml` | Yes | Scheduler Button in Sidebar | **USED** | None |
| `ResumeDialog` | `Views/ResumeDialog.xaml` | Yes | Incomplete download prompt | **USED** | None |
| `UpdatePopup` | `Views/UpdatePopup.xaml` | Yes | New Version detected prompt | **USED** | None |
| `PowerActionCountdownDialog` | `Views/PowerActionCountdownDialog.xaml` | Yes | Post-download shutdown timer | **USED** | None |
| `BatchDownloadWindow` | `Views/BatchDownloadWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Batch Download" to Sidebar/Tools menu |
| `SiteGrabberWindow` | `Views/SiteGrabberWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Site Grabber" to Sidebar/Tools menu |
| `SiteGrabberWizardWindow` | `Views/SiteGrabberWizardWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Wire Step-by-Step wizard inside SiteGrabber |
| `SiteLoginsManagerWindow` | `Views/SiteLoginsManagerWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Site Logins" to Settings / Menu |
| `CategoryRulesEditorWindow` | `Views/CategoryRulesEditorWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Category Rules" to Settings / Categories |
| `RemoteZipPreviewWindow` | `Views/RemoteZipPreviewWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Preview Archive" context menu in table |
| `FloatingDropTargetWindow` | `Views/FloatingDropTargetWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add "Show Drop Target" toggle in Tray / Sidebar |
| `ContextMenuRegistrationWindow` | `Views/ContextMenuRegistrationWindow.xaml` | **NO** | None (Unwired) | **ORPHAN VIEW** | Add context menu config in Settings / Advanced |

---

## 4. Remediation Strategy
1. **Zero Deletions**: Do not delete any existing class or view. The unwired views represent valuable IDM parity features that already have complete XAML layouts and underlying service logic.
2. **Deterministic UI Wiring**: Connect each orphaned view directly to the main navigation menu (`Sidebar.xaml`), application menus (`MainWindow.xaml`), or context menus (`DownloadsTable.xaml`).
