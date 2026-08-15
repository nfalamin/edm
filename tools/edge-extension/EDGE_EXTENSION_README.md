# Exclusive Download Manager (EDM) - Microsoft Edge Extension

This folder contains the Manifest V3 browser extension for **Microsoft Edge**. It enables stream capturing, high-speed video/audio link detection, and automatic transfer of downloads to the EDM Desktop App.

---

## Features
- **Media Sniffing**: Automatically intercepts `.mp4`, `.m3u8`, `.webm`, `.mp3`, `.pdf`, `.zip`, `.rar`, `.exe` downloads.
- **Floating Download Badge**: Displays detected media quality on active web pages with a single-click download popup.
- **Native Messaging Integration**: Passes URLs directly to `EDM.exe` via Windows Native Messaging stdio protocol.

---

## Installation Steps for Microsoft Edge

### 1. Register Native Messaging Host in Windows Registry
To allow Microsoft Edge to communicate with `EDM.exe`, register the Native Host in Windows Registry under Edge's native messaging key:

```registry
HKEY_CURRENT_USER\Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader
```

- **(Default)** string value: Full path to `com.edm.downloader.edge.json` (e.g. `C:\Program Files\EDM\com.edm.downloader.edge.json`).

### 2. Load Extension in Microsoft Edge
1. Open Microsoft Edge and navigate to `edge://extensions`.
2. Enable **Developer mode** using the toggle switch in the left sidebar.
3. Click **Load unpacked**.
4. Select the `tools/edge-extension/` directory.
5. The **Exclusive Download Manager (EDM) Edge Capturer** extension is now installed and active!

---

## Verification
- Navigate to any video/audio streaming page (e.g. YouTube, Vimeo, or a media site).
- The floating **"Download with EDM"** badge will appear at the bottom-right of the page.
- Clicking **Download** will forward the link directly into EDM Desktop App for multi-part accelerated downloading.
