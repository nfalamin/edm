# STAGE 6 — PHASE 1: IDM BEHAVIORAL PARITY INVENTORY

**Audit Date:** 2026-08-15  
**Scope:** Complete inventory of 46 distinct download manager behaviors (A through AT).  

---

## 1. Domain Behavioral Inventory (A to AT)

| # | Behavior Domain | IDM Behavior Description | EDM Corresponding Subsystem | Verification Method |
| :--- | :--- | :--- | :--- | :--- |
| **A** | **Add URL** | URL parsing, filename detection, cookie extraction | `AddUrlWindow`, `UrlValidator` | Automated UI test |
| **B** | **Download Dialog** | Category, save path, download now/later | `AddUrlWindow.xaml.cs` | Dialog test |
| **C** | **Queue** | Sequential and concurrent queue scheduling | `DownloadQueueManager.cs` | Concurrency test |
| **D** | **Pause** | Immediate network and disk write freeze | `PauseTokenSource.cs` | Byte-level test |
| **E** | **Resume** | Range header continuation from last byte | `DownloadService.cs` (206) | Integrity test |
| **F** | **Stop** | Graceful worker halt and temporary file retain | `CancellationTokenSource` | Process test |
| **G** | **Restart** | Redownload file from 0 bytes | `DownloadOrchestrator.cs` | Restart test |
| **H** | **Retry** | Exponential backoff retry loop | `DownloadService.cs` | Fault injection |
| **I** | **Error Recovery** | Network drop and socket reset recovery | Bounded retry loop | Drop simulation |
| **J** | **Expired URL Recovery**| Refresh download address without re-downloading | `DownloadItem.Url` update | URL refresh test |
| **K** | **Segmented Downloading**| 1–32 chunk range allocation | `MultiPartDownloader.cs` | Segment test |
| **L** | **Dynamic Segmentation**| Splits slowest segment dynamically | `AdaptiveChunkSizer.cs` | Chunk sizing test |
| **M** | **Connection Management**| HTTP socket pooling and reuse | `SocketsHttpHandler` pool | Socket pool test |
| **N** | **Speed Limiting** | Global bandwidth throttling | `BandwidthThrottler.cs` | Token-bucket test |
| **O** | **Per-Download Speed** | Slider limit on progress window | `DownloadProgressWindow` | Slider test |
| **P** | **Global Speed Control**| Global cap across all downloads | `BandwidthThrottler.Global` | Multi-stream test |
| **Q** | **Scheduler** | Cron and time-window downloads | `AdvancedQueueScheduler.cs` | Scheduler test |
| **R** | **Download Categories** | Folder routing by extension/MIME | `FileCategorizationService` | Category test |
| **S** | **File Naming** | Content-Disposition and URL sanitization | `PathHelper.cs` | Naming test |
| **T** | **Existing-File Collision**| Auto-rename `(1).ext` or prompt overwrite | `FileCollisionHandler.cs` | Collision test |
| **U** | **Browser Interception**| Native Messaging interception | Chrome/Edge/Firefox MV3 | Native IPC test |
| **V** | **Browser Bypass** | Alt-key bypass during click | Extension content script | Keydown test |
| **W** | **Download Confirmation**| Option to show/hide Add URL dialog | `SettingsService.ShowDialog`| Settings test |
| **X** | **Batch Downloads** | Import URL list from TXT/CSV/JSON | `DownloadListImportExportService`| Batch test |
| **Y** | **Site Grabber** | Spider/recursive website crawler | `SiteGrabberService.cs` | Crawler test |
| **Z** | **Video Detection** | Webpage media sniffer | Manifest V3 content script | Sniffer test |
| **AA**| **Video Format Selection**| Quality selector (1080p, 4K, 8K) | `YtDlpService.cs` | Format query test |
| **AB**| **Audio Extraction** | Extract MP3/M4A from video streams | `MediaMergeService.cs` | FFmpeg mux test |
| **AC**| **Resume After Restart**| Incomplete `.part` detection on startup | `ResumeScannerService.cs` | Startup test |
| **AD**| **Crash Recovery** | SQLite transaction journal restoration | `DownloadJournalEngine.cs` | Crash test |
| **AE**| **History** | Persistent download records | `HistoryService.cs` (SQLite) | DB query test |
| **AF**| **Download List Persist**| Saves queue across launches | SQLite history database | Reload test |
| **AG**| **Checksum / Integrity**| SHA-256, MD5, SHA-512 auto validation | `FileIntegrityService.cs` | Checksum test |
| **AH**| **Proxy** | HTTP/SOCKS4/SOCKS5 proxy support | `ProxySettings.cs` | Proxy test |
| **AI**| **Authentication** | Basic / Digest / DPAPI login | `SecureCredentialVault.cs` | Vault test |
| **AJ**| **Cookies / Sessions** | Browser cookie handoff | `IpcHandoffPayload.Cookies` | Cookie test |
| **AK**| **HTTPS / TLS Behavior**| TLS 1.3, ALPN, certificate validation | .NET 10 SocketsHandler | TLS test |
| **AL**| **Windows Integration** | Shell open, Show in Explorer | `Process.Start("explorer.exe")`| Shell test |
| **AM**| **Auto-Shutdown** | Shutdown/Sleep PC upon completion | `PowerManagementService.cs` | Win32 API test |
| **AN**| **Sound Notifications** | Audio chime on completion | `SoundNotificationService.cs` | Audio test |
| **AO**| **Antivirus Integration**| Scan completed file with Defender | `PostDownloadScannerService` | Scanner test |
| **AP**| **Import / Export** | Export download lists | `DownloadListImportExportService`| Export test |
| **AQ**| **Language / Loc** | Multi-language UI (English/Bengali) | `LocalizationManager.cs` | Locale test |
| **AR**| **Extension Integration**| Manifest V3 native host registration | `BrowserExtensionInstaller` | Registry test |
| **AS**| **Update System** | Control Plane cloud update check | `UpdateService.cs` | Update test |
| **AT**| **Error Reporting** | Crash report and structured log | `LoggingService.cs` | Crash dump test |
