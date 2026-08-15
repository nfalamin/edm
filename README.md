# Exclusive Download Manager (EDM)

[![Build & Test Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)](#building--running)
[![Framework](https://img.shields.io/badge/.NET-10.0--windows-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Exclusive Download Manager (EDM)** is a state-of-the-art, high-performance Windows download manager built with **.NET 10** and **WPF**. Designed to outpace traditional download tools, EDM delivers multi-threaded segmented file acceleration, deep site grabbing, real-time bandwidth scheduling, post-download threat scanning, and seamless integration with **Chrome**, **Microsoft Edge**, and **Mozilla Firefox**.

---

## Key Features

### ⚡ Accelerated Segmented Downloading
- **Multi-Part Pipeline**: Splitting downloads across up to 32 parallel HTTP range connections using `MultiPartDownloader`.
- **Adaptive Connection Sizer**: Dynamically adjusts connection density based on network interface type (Ethernet, Wi-Fi, 5G/Cellular, Metered) and host latency.
- **Pause & Resume**: Resumes interrupted or paused downloads without losing progress.

### 🌐 Universal Browser Integration & Session Import
- **Multi-Browser Extensions**: Native support for **Google Chrome**, **Microsoft Edge**, and **Mozilla Firefox** (`tools/chrome-extension/`, `tools/edge-extension/`, `tools/firefox-extension/`).
- **Session Cookie Import**: Automatically captures browser session cookies (`chrome.cookies.getAll`) for login-gated and authenticated downloads.
- **DPAPI Encryption**: At-rest credentials and session cookies are protected using Windows Data Protection API (DPAPI).

### 🕸️ Deep Site Grabber
- **Multi-Level Crawling**: Follows links up to configurable depth levels (`MaxDepth`).
- **Domain Restrictions**: Toggles same-domain restrictions (`SameDomainOnly`) vs. external link discovery.
- **Smart Filtering**: Filters by file extension, regex URL patterns, and minimum file size.
- **Metadata Discovery**: Performs dry-run `HEAD` requests to inspect file sizes before queuing downloads.

### 🛡️ Security & Post-Download Quarantine
- **Google Safe Browsing**: Pre-checks URLs against malware, phishing, and unsafe domain blacklists.
- **Windows Defender Post-Scan**: Automatically scans completed files via Windows Defender CLI (`MpCmdRun.exe`).
- **Automatic Quarantine**: Moves detected threats to `%USERPROFILE%\EDM\Quarantine\` to isolate host systems.

### 📊 Named Bandwidth Schedule Profiles
- **Profile Management**: Custom named profiles (e.g. *"Work Hours"*, *"Night"*, *"Weekend"*).
- **Priority Resolution**: Resolves overlapping time windows based on profile priority (`Priority`) and speed limit strictness.

### 📦 Windows Installer & Auto-Update Engine
- **Inno Setup Installer**: Production-ready installer (`tools/installer/EDMSetup.iss`) registering native messaging hosts in Windows Registry for Chrome, Edge, and Firefox, `edm://` protocol handler, and `.edm` container associations.
- **Resumable Auto-Update Engine**: Reliable version updates with live progress feedback and SHA256 checksum verification.

---

## Screenshot Previews

```
+-------------------------------------------------------------------------------+
|  Exclusive Download Manager (EDM)                                       - □ x |
+-------------------------------------------------------------------------------+
| [ + Add URL ] [ ⏸ Pause All ] [ ▶ Resume All ] [ ⚙ Settings ] [ 🌙 Dark Mode ] |
+---------------+---------------------------------------------------------------+
| CATEGORIES    | File Name           Size       Progress     Speed      Status |
| ------------- | ------------------  ---------  -----------  ---------  ------ |
| 📁 All        | 🎬 Movie_1080p.mkv  2.40 GB    [██████░░░░] 14.8 MB/s  72%    |
| 🎵 Music      | 📦 Setup_v2.exe     145.0 MB   [██████████] --         100%   |
| 📹 Videos     | 📄 Document.pdf     12.5 MB    [████░░░░░░] 4.2 MB/s   45%    |
| 📦 Compressed | 🎵 AudioTrack.flac  68.0 MB    [░░░░░░░░░░] --         Queued |
| ⚙️ Settings   |                                                               |
+---------------+---------------------------------------------------------------+
| 📊 Real-Time Network Speed: 19.0 MB/s (Peak: 24.5 MB/s) | Active Profile: Work |
+-------------------------------------------------------------------------------+
```

---

## Building & Running

### Prerequisites
- **.NET 10.0 SDK** (or later)
- **Windows 10 / 11** (x64)
- **Inno Setup 6+** (Optional, for building the installer)

### Build Application
```powershell
dotnet build EDM\EDM.csproj -c Release
```

### Run Unit & Stress Tests
```powershell
dotnet test EDM.Tests
```

### Compile Production Installer
```powershell
iscc tools\installer\EDMSetup.iss
```
The generated installer will be saved to `tools\installer\Output\EDMSetup.exe`.

### Package Web Extensions
```powershell
powershell -ExecutionPolicy Bypass -File tools\package-extensions.ps1
```
Extension ZIP packages will be generated in `tools\store-packages\`.

---

## Project Architecture

```text
EDM/
├── EDM/                     # Core WPF Desktop Application
│   ├── Models/              # DownloadItem, BandwidthSchedule, ProxySettings
│   ├── Services/            # DownloadService, MultiPartDownloader, SiteGrabberService, SafeBrowsingService, UpdateService
│   ├── ViewModels/          # DownloadManagerViewModel, AddUrlViewModel
│   ├── Views/               # Dashboard, DownloadsTable, UpdatePopup, SettingsWindow
│   └── Themes/              # DarkTheme.xaml & LightTheme.xaml (DynamicResource System)
├── EDM.Tests/               # xUnit Test Suite (56 Unit & Stress Tests)
├── tools/
│   ├── chrome-extension/    # Chrome Web Extension (Manifest V3)
│   ├── edge-extension/      # Microsoft Edge Web Extension (Manifest V3)
│   ├── firefox-extension/   # Mozilla Firefox WebExtension (Manifest V3)
│   ├── installer/           # Inno Setup Script & Build Guide
│   └── store-packages/      # Production Store ZIP Packages
└── CHANGELOG.md             # Project Release History & Architecture Log
```

---

## License

Exclusive Download Manager is licensed under the [MIT License](LICENSE).
