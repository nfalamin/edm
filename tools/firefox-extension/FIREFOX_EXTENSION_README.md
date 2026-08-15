# Exclusive Download Manager (EDM) - Mozilla Firefox Extension

This folder contains the WebExtension for **Mozilla Firefox**. It enables media link sniffing, quality resolution detection, and automatic transfer of download jobs into the EDM Desktop App.

---

## Key Firefox Architecture Differences
1. **Extension Identifier**: Assigned explicit ID `"edm@exclusive-download-manager.com"` in `manifest.json` under `browser_specific_settings.gecko`.
2. **Cross-Browser Polyfill**: `background.js` and `content.js` seamlessly utilize `browser.*` Promises with fallback to `chrome.*` callbacks.
3. **Native Messaging Host Specification**: Uses `allowed_extensions` array (`["edm@exclusive-download-manager.com"]`) instead of Chromium's `allowed_origins`.

---

## Installation Steps for Mozilla Firefox

### 1. Register Native Messaging Host in Windows Registry
Firefox uses a separate registry path from Chrome/Edge to discover native messaging hosts. Register under:

```registry
HKEY_CURRENT_USER\Software\Mozilla\NativeMessagingHosts\com.edm.downloader
```

- **(Default)** string value: Full path to `com.edm.downloader.firefox.json` (e.g. `C:\Program Files\EDM\com.edm.downloader.firefox.json`).

### 2. Load Extension in Mozilla Firefox
1. Open Mozilla Firefox and navigate to `about:debugging#/runtime/this-firefox`.
2. Click **Load Temporary Add-on...**
3. Select the `tools/firefox-extension/manifest.json` file.
4. The **Exclusive Download Manager (EDM) Firefox Capturer** extension is now installed!

---

## Verification
- Visit any site with embedded videos/audio (e.g. YouTube, Vimeo, MP3 hosting).
- The floating **"Download with EDM"** badge will render at the bottom-right corner.
- Clicking **Download** will trigger `browser.runtime.sendNativeMessage` to launch or communicate directly with `EDM.exe`.
