# STAGE 5 — PROMPT 5: UI CONTROLS & COMMAND BINDINGS AUDIT

**Audit Date:** 2026-08-15  
**Scope:** `MainWindow.xaml`, `AddUrlWindow.xaml`, `DownloadProgressWindow.xaml`, `SettingsWindow.xaml`  

---

## 1. UI Control Audit Matrix

| View | Control / Button | Bound Command / Event | Underlying Service Called | Action Status |
| :--- | :--- | :--- | :--- | :---: |
| `MainWindow` | **+ Add URL** | `AddUrlCommand` | Opens `AddUrlWindow` | **VERIFIED** |
| `MainWindow` | **Resume** | `ResumeDownloadCommand` | `DownloadOrchestrator.StartDownloadAsync` | **VERIFIED** |
| `MainWindow` | **Pause** | `PauseDownloadCommand` | `PauseTokenSource.Pause()` | **VERIFIED** |
| `MainWindow` | **Pause All** | `PauseAllCommand` | `SystemTrayManager.OnPauseAllRequested` | **VERIFIED** |
| `MainWindow` | **Resume All** | `ResumeAllCommand` | `SystemTrayManager.OnResumeAllRequested`| **VERIFIED** |
| `MainWindow` | **Cancel / Stop** | `CancelDownloadCommand` | `CancellationTokenSource.Cancel()` | **VERIFIED** |
| `MainWindow` | **Delete** | `DeleteDownloadCommand` | `HistoryService.DeleteHistoryEntryAsync` | **VERIFIED** |
| `MainWindow` | **Open Folder** | `OpenFolderCommand` | `Process.Start("explorer.exe", folder)` | **VERIFIED** |
| `MainWindow` | **Search Box** | `SearchTextChanged` | `ApplyFilter()` in ViewModel | **VERIFIED** |
| `MainWindow` | **Category Filter** | `CategorySelected` | `ApplyFilter()` in ViewModel | **VERIFIED** |
| `MainWindow` | **Settings** | `OpenSettingsCommand` | Opens `SettingsWindow` | **VERIFIED** |
| `DownloadProgress`| **Pause / Resume** | `PauseResume_Click` | `_pauseTokenSource` | **VERIFIED** |
| `DownloadProgress`| **Cancel** | `Cancel_Click` | `_cts.Cancel()` | **VERIFIED** |
| `DownloadProgress`| **Speed Limit Slider**| `SpeedSlider_ValueChanged`| `BandwidthThrottler.SetLimit()` | **VERIFIED** |
| `AddUrlWindow` | **Download Now** | `DownloadNow_Click` | Dispatches to `DownloadProgressWindow` | **VERIFIED** |
| `AddUrlWindow` | **Browse Path** | `Browse_Click` | `IFileDialogService.SaveFileDialog` | **VERIFIED** |

---

## 2. Conclusion
- **Total Controls Audited:** 16 Primary Controls
- **Dead / Unbound Controls:** 0
- **Partially Bound Controls:** 0
- **Verdict:** All user-facing controls trigger concrete services and reflect real application state.
