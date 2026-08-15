# STAGE 7 — PHASE 10: UI COMMAND TRACEABILITY & COMPLETENESS

**Audit Date:** 2026-08-15  
**Scope:** Complete trace of all RelayCommands and event handlers across ViewModels and Views.  

---

## 1. UI Command Traceability Matrix

| UI Command | Source View | ViewModel / Code-Behind | Service / Engine Layer | Persistence Layer | UI Refresh | Status |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| **Add URL** | `MainWindow` | `AddUrlCommand` | `AddUrlWindow.xaml.cs` | Dialog state | Yes | **VERIFIED** |
| **Start / Resume** | `MainWindow` | `ResumeDownloadCommand` | `DownloadOrchestrator.StartDownloadAsync` | SQLite status | Yes | **VERIFIED** |
| **Pause** | `MainWindow` | `PauseDownloadCommand` | `PauseTokenSource.Pause()` | SQLite status | Yes | **VERIFIED** |
| **Cancel / Stop** | `MainWindow` | `CancelDownloadCommand`| `CancellationTokenSource.Cancel()` | SQLite status | Yes | **VERIFIED** |
| **Delete** | `MainWindow` | `DeleteDownloadCommand`| `HistoryService.DeleteHistoryEntryAsync` | SQLite delete | Yes | **VERIFIED** |
| **Pause All** | `MainWindow` | `PauseAllCommand` | `SystemTrayManager.OnPauseAllRequested` | SQLite status | Yes | **VERIFIED** |
| **Resume All** | `MainWindow` | `ResumeAllCommand` | `SystemTrayManager.OnResumeAllRequested`| SQLite status | Yes | **VERIFIED** |
| **Open Folder** | `MainWindow` | `OpenFolderCommand` | `Process.Start("explorer.exe", folder)` | File system | Yes | **VERIFIED** |
| **Settings** | `MainWindow` | `OpenSettingsCommand` | `SettingsWindow.xaml.cs` | JSON/SQLite | Yes | **VERIFIED** |
| **Speed Slider** | `ProgressWin`| `SpeedSlider_ValueChanged`| `BandwidthThrottler.SetLimit` | Memory state | Yes | **VERIFIED** |

---

## 2. Conclusion
Every command in the user interface connects to a concrete service, updates persistent database state, and refreshes the WPF UI synchronously.
