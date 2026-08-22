# IDM PARITY FIX ROADMAP: EXCLUSIVE DOWNLOAD MANAGER (EDM)

**Document Type:** Actionable Engineering Fix Roadmap  
**Audit Date:** 2026-08-15  
**Auditor:** Senior Windows Download-Manager Architect & WPF Specialist  

---

## 1. Prioritization Taxonomy

- **P0 (Critical / Blocker)**: Blocks core download engine, Native Messaging IPC, or causes crashes / data corruption.
- **P1 (Major IDM Capability Gaps)**: Existing backend capabilities and XAML views that are unwired from the UI.
- **P2 (Partial / Weak Implementations)**: Functional features that lack specific edge-case IDM ergonomics.
- **P3 (Secondary Parity & Polish)**: Audio cues, tray menus, drop target widget toggles, theme adjustments.
- **P4 (Obsolete IDM Features)**: Features that should NOT be copied (e.g. dial-up modem RAS dialing, IE6 integration).

---

## 2. Prioritized Fix Backlog

### P0 — Critical Blockers *(All Resolved in Stage 4 Prompt 3)*
1. **Native Messaging 32-bit LE Binary Stdio Framing**:
   - *Status*: **RESOLVED & CERTIFIED**.
   - *Production Files*: `EDM.NativeHost\Program.cs`, `NativeIpcServer.cs`.
   - *Verification*: `tools/TestNativeMessaging.ps1`.
2. **Add-URL Double-Click Race Prevention**:
   - *Status*: **RESOLVED & CERTIFIED**.
   - *Production Files*: `DownloadProgressWindow.xaml.cs`.
   - *Verification*: `tools/TestAddUrlDownload.ps1`.

---

### P1 — Major IDM Capability Gaps (Unwired UI Views)

#### 1. Batch Downloader UI Wiring
- **Exact Broken Capability**: User cannot open the Batch Downloader dialog to download wildcard URL sequences (`file[01-10].zip`).
- **Exact Files Inspected**:
  - `EDM/Services/UrlPatternExpander.cs`
  - `EDM/Views/BatchDownloadWindow.xaml`
  - `EDM/Views/BatchDownloadWindow.xaml.cs`
  - `EDM/Views/Sidebar.xaml`
- **Code Path Responsible**: `Sidebar.xaml` and `Dashboard.xaml` have no command/click handler instantiating `BatchDownloadWindow`.
- **Files That Must Change**:
  - `EDM/Views/Sidebar.xaml`: Add "Batch Download" button under "Tasks" / "Add Download".
  - `EDM/Views/Sidebar.xaml.cs`: Add `BatchDownload_Click` handler opening `new BatchDownloadWindow().ShowDialog()`.
- **Existing Tests**: `UrlPatternExpanderTests.cs` (Unit tests for wildcard parser).
- **Missing Tests**: UI integration test opening `BatchDownloadWindow` and adding batch items to queue.
- **Expected Final Behavior**: Clicking "Batch Download" opens the dialog, user enters `http://domain.com/item[1-5].mp4`, clicks OK, and 5 downloads are added to the queue.

#### 2. Site Grabber UI Wiring
- **Exact Broken Capability**: User cannot launch the multi-level Site Grabber web crawler to download all assets/media from a domain.
- **Exact Files Inspected**:
  - `EDM/Services/SiteGrabberService.cs`
  - `EDM/Services/WebCrawlerSubsystem.cs`
  - `EDM/Views/SiteGrabberWindow.xaml`
  - `EDM/Views/SiteGrabberWizardWindow.xaml`
  - `EDM/Views/Sidebar.xaml`
- **Code Path Responsible**: Neither `Sidebar.xaml` nor the Main Menu references `SiteGrabberWindow`.
- **Files That Must Change**:
  - `EDM/Views/Sidebar.xaml`: Add "Site Grabber" navigation item.
  - `EDM/Views/Sidebar.xaml.cs`: Add `SiteGrabber_Click` handler launching `new SiteGrabberWindow()`.
- **Existing Tests**: `SiteGrabberTests.cs`, `SiteGrabberServiceTests.cs`.
- **Missing Tests**: E2E test initiating crawl from window and queueing discovered media items.
- **Expected Final Behavior**: User clicks "Site Grabber", configures depth and file filters, runs crawler, reviews discovered links, and initiates download.

#### 3. Site Logins Manager UI Wiring
- **Exact Broken Capability**: User cannot manage pre-configured site credentials and session cookies via GUI.
- **Exact Files Inspected**:
  - `EDM/Services/SecureCredentialVault.cs`
  - `EDM/Views/SiteLoginsManagerWindow.xaml`
  - `EDM/Views/SettingsWindow.xaml`
- **Code Path Responsible**: `SettingsWindow.xaml` lacks a button/tab linking to `SiteLoginsManagerWindow`.
- **Files That Must Change**:
  - `EDM/Views/SettingsWindow.xaml`: Add "Site Logins" button in General / Network tab.
  - `EDM/Views/SettingsWindow.xaml.cs`: Add click handler opening `new SiteLoginsManagerWindow()`.
- **Existing Tests**: `SecureCredentialVaultTests.cs`.
- **Missing Tests**: UI test for adding/editing/deleting encrypted site credentials in vault.
- **Expected Final Behavior**: User enters default username/password for specific domains in GUI; downloads automatically retrieve credentials.

#### 4. Remote ZIP Preview Context Menu Wiring
- **Exact Broken Capability**: User cannot right-click a `.zip` download link to inspect contents before downloading.
- **Exact Files Inspected**:
  - `EDM/Services/RemoteZipPreviewService.cs`
  - `EDM/Views/RemoteZipPreviewWindow.xaml`
  - `EDM/Views/DownloadsTable.xaml`
- **Code Path Responsible**: `DownloadsTable.xaml` context menu lacks a "Preview Archive" menu item.
- **Files That Must Change**:
  - `EDM/Views/DownloadsTable.xaml`: Add "Preview Archive (.zip)" context menu item enabled for `.zip` URLs.
  - `EDM/Views/DownloadsTable.xaml.cs`: Add handler instantiating `new RemoteZipPreviewWindow(selectedItem.Url)`.
- **Existing Tests**: `RemoteZipPreviewTests.cs`.
- **Missing Tests**: UI context menu action test.
- **Expected Final Behavior**: Right-clicking a `.zip` file opens `RemoteZipPreviewWindow`, streams the central directory header via HTTP Range requests, and displays inner files.

#### 5. Category Rules Editor UI Wiring
- **Exact Broken Capability**: User cannot define custom file-extension-to-folder mapping rules via GUI.
- **Exact Files Inspected**:
  - `EDM/Services/DownloadPathCategoryService.cs`
  - `EDM/Views/CategoryRulesEditorWindow.xaml`
  - `EDM/Views/SettingsWindow.xaml`
- **Code Path Responsible**: `SettingsWindow.xaml` lacks a "Configure Categories" button.
- **Files That Must Change**:
  - `EDM/Views/SettingsWindow.xaml`: Add "Manage Categories" button in General / Save Paths section.
  - `EDM/Views/SettingsWindow.xaml.cs`: Add handler launching `new CategoryRulesEditorWindow()`.
- **Existing Tests**: `DownloadPathCategoryServiceTests.cs`.
- **Missing Tests**: UI rule persistence test.
- **Expected Final Behavior**: User customizes file extension categories and target download directories.

---

### P2 — Partial / Secondary Ergonomics

#### 1. Floating Drop Target Window Toggle
- **Capability**: IDM floating drag & drop target on desktop.
- **Files Involved**: `EDM/Views/FloatingDropTargetWindow.xaml`, `EDM/Services/SystemTrayManager.cs`.
- **Fix**: Add "Show Drop Target" checkbox in System Tray and View menu.

#### 2. FTP Active Mode Fallback
- **Capability**: If PASV mode times out or is blocked by client firewall, fall back to PORT (active mode).
- **Files Involved**: `EDM/Services/FtpDownloadService.cs`.
- **Fix**: Catch PASV socket timeout and retry with active PORT socket command.

---

### P3 — Minor Polish & Non-Functional Parity
- Custom sound notification wave selection in `SettingsWindow.xaml`.
- Context menu shell integration installer toggle in `SettingsWindow.xaml`.

---

### P4 — Obsolete Features (Explicitly Excluded)
- Dial-up Modem / RAS Connection manager.
- Internet Explorer 6 BHO integration.
- Windows 98/XP style raw GDI rendering.
