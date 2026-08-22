# EDM Native Messaging & Desktop IPC Architecture

## 1. Native Messaging Host Specification

- **Host Executable:** `EDM.NativeHost.exe`
- **Host Identifier:** `com.edm.downloader`
- **Framing Format:** 32-bit unsigned little-endian integer prefix representing length in bytes of UTF-8 JSON payload.
- **Maximum Length:** 10 MB (10,485,760 bytes).
- **Communication Channels:** `stdin` (Incoming from Browser), `stdout` (Outgoing to Browser), `stderr` (Diagnostic logs only).

## 2. Manifest Registry Paths (Windows)

| Browser | Registry Key |
| :--- | :--- |
| Google Chrome | `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader` |
| Microsoft Edge | `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader` |
| Mozilla Firefox | `HKCU\Software\Mozilla\NativeMessagingHosts\com.edm.downloader` |
| Brave Browser | `HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.edm.downloader` |
| Opera | `HKCU\Software\Opera Software\NativeMessagingHosts\com.edm.downloader` |
| Vivaldi | `HKCU\Software\Vivaldi\NativeMessagingHosts\com.edm.downloader` |

## 3. Desktop IPC Handoff Chain

1. **Named Pipe Client:** `EDM.NativeHost.exe` attempts to connect to Named Pipe `\\.\pipe\EDM_NativeMessaging_Pipe` with 1000ms timeout.
2. **On-Demand GUI Launch:** If pipe connection fails (GUI not running), `EDM.NativeHost.exe` launches `EDM.exe --handoff <base64-json-payload>` via Windows `Process.Start`.
3. **Local REST Fallback:** In the event that stdio native messaging is unavailable, the background worker sends an HTTP POST request to `http://127.0.0.1:48912/handoff`.
4. **Emergency Browser Fallback:** If both native and HTTP channels fail, direct standalone media files are downloaded via `chrome.downloads.download` (manifests and adaptive streams are safely blocked).
