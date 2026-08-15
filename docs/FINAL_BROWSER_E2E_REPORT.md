# FINAL BROWSER E2E REPORT

**Certification Date:** 2026-08-15  
**Product:** Exclusive Download Manager (EDM) Browser Extension Integration  
**Supported Browsers:** Google Chrome (Manifest V3), Microsoft Edge, Mozilla Firefox (WebExtensions)  

---

## 1. Native Messaging Manifest & Registry Architecture

### 1.1 Host Manifests
- **Host Name:** `com.exclusive.downloadmanager.native`
- **Binary Path:** `%LOCALAPPDATA%\EDM\NativeMessaging\EDM.NativeHost.exe` (or `<AppBaseDir>\EDM.NativeHost.exe`)
- **Chrome Origin:** `chrome-extension://knldjmfmopnppmllpmhedemckgbmgbfm/`
- **Firefox Origin:** `firefox-extension://exclusive-download-manager@edm.com/`

### 1.2 Windows Registry Paths
- **Chrome:** `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.exclusive.downloadmanager.native`
- **Edge:** `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.exclusive.downloadmanager.native`
- **Firefox:** `HKCU\Software\Mozilla\NativeMessagingHosts\com.exclusive.downloadmanager.native`

All registry paths and manifests are registered and validated via [`NativeHostInstaller.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/NativeHostInstaller.cs).

---

## 2. Browser Extension Pipeline & Capabilities

### 2.1 Standard Download Interception
1. Browser initiates a file download (`chrome.downloads.onCreated`).
2. Extension `background.js` pauses browser-native download.
3. Extension packages URL, HTTP referrer, Cookies, User-Agent, and suggested filename into a JSON packet.
4. Native host sends packet via 32-bit LE length-prefixed binary framing over stdio.
5. EDM opens Add-URL dialog or immediate progress window with authenticated cookies.

### 2.2 Floating Video Sniffer Panel
1. `content.js` monitors HTML5 `<video>` tags and SPA navigation events (`yt-navigate-finish`, `popstate`, `pushState`).
2. Manifest parser extracts audio and video streams (HLS `.m3u8`, DASH `.mpd`, direct `.mp4`).
3. Floating "Download with EDM" button renders near the player viewport.
4. Format picker displays available resolutions (4K, 1440p, 1080p, 720p, 480p, MP3 Audio).
5. Selecting a format sends download request directly to EDM.

### 2.3 Context Menu Actions
- **"Download with EDM"**: Downloads single link under cursor.
- **"Download All Links with EDM"**: Extracts all page links and opens `DownloadAllLinksWindow`.
- **"Grab site with EDM"**: Opens `SiteGrabberWizardWindow` with active URL.

---

## 3. Verification Test Evidence

- **Native Messaging Binary Framing:** `NativeMessagingRealIpcTests.cs` (4/4 passed).
- **Video Sniffer & Manifest Parsing:** `RealVideoDetectionAndResolverTests.cs` (5/5 passed).
- **Automated Browser Packaging & Zip Generation:** `tools/PackageBrowserExtension.ps1` builds clean distribution packages without forbidden permissions.
