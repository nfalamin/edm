# EDM Browser Extension — Master Architecture (Version 1.0.0)

## 1. System Overview

The Exclusive Download Manager (EDM) browser extension subsystem is designed as a decoupled, multi-process, low-latency architecture bridging modern web browsers (Chrome, Edge, Firefox, Brave, Opera, Vivaldi) with the EDM desktop application.

```
┌─────────────────────────────────────────────────────────────┐
│                     BROWSER ENVIRONMENT                     │
│                                                             │
│   Web Page DOM Context           Extension Background       │
│  ┌───────────────────────┐      ┌─────────────────────────┐ │
│  │ • ActivePlayerTracker │      │ • StreamSniffer         │ │
│  │ • yt-bridge.js (MAIN) │      │ • DownloadInterceptor   │ │
│  │ • FloatingPillOverlay │◄─IPC─►│ • MessageRouter         │ │
│  │ • FormatCardSelector  │      │ • StateManager (MV3)    │ │
│  └───────────────────────┘      │ • NativeConnectionMgr   │ │
│                                 └────────────┬────────────┘ │
└──────────────────────────────────────────────┼──────────────┘
                                               │
                           (Stdio 32-bit LE Binary Framing)
                                               │
                                               ▼
                                  ┌─────────────────────────┐
                                  │   EDM.NativeHost.exe    │
                                  │  (Windows Host Process) │
                                  └────────────┬────────────┘
                                               │
                                  (Named Pipe / CLI Fallback)
                                               │
                                               ▼
                                  ┌─────────────────────────┐
                                  │   EDM Desktop Engine    │
                                  │  • MultiPartDownloader  │
                                  │  • SQLite Queue Store   │
                                  │  • Progress Telemetry   │
                                  └─────────────────────────┘
```

## 2. Component Boundaries

1. **Content Script Layer (`src/content/`, `src/ui/`):**
   - Injected at `document_idle` on web pages.
   - Monitors HTML5 video elements, tracks `play`/`playing` events, calculates viewport intersection ratios.
   - Constructs and manages frosted-glass UI overlays.
   - Dispatches validated user actions through `chrome.runtime.sendMessage`.

2. **Main-World Script Layer (`src/injected/yt-bridge.js`):**
   - Injected into YouTube MAIN execution context.
   - Intercepts player API responses and dispatches to content script via `window.postMessage`.
   - Strictly protected with `event.source === window` origin verification.

3. **Background Service Worker Layer (`src/background/`, `src/messaging/`):**
   - MV3 event-driven service worker.
   - Centralized `MessageRouter` with strict message allowlist and schema validation.
   - `DownloadInterceptor` monitoring `chrome.downloads.onCreated`.
   - `StateManager` persisting tab streams across service worker idle sleep cycles.
   - `NativeConnectionManager` coordinating stdio IPC with `EDM.NativeHost.exe`.

4. **Native Messaging Host Layer (`EDM.NativeHost`):**
   - Dedicated C# process running under Windows Native Messaging framing (32-bit LE integer prefix).
   - Bridges browser requests directly into the running EDM desktop application via Named Pipe (`\\.\pipe\EDM_NativeMessaging_Pipe`).
