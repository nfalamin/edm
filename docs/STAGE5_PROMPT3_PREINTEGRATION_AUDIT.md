# STAGE 5 — PROMPT 3: PRE-INTEGRATION AUDIT

**Document Type:** Pre-Integration Technical Audit  
**Date:** 2026-08-15  
**Target:** EDM Desktop App (.NET 10.0 WPF) ↔ EDM.ControlPlane.Api  

---

## 1. Existing Desktop Architecture Overview

| Subsystem | Existing Component | Existing Responsibility | Integration Decision |
| :--- | :--- | :--- | :--- |
| **HTTP Transport** | [`EDM/Services/SharedHttpClient.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SharedHttpClient.cs) | SocketsHttpHandler connection pooling (64–128 sockets), 16MB HTTP/2 window | **REUSE** for all Control Plane API calls |
| **Credential Storage** | [`EDM/Services/SecureCredentialVault.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SecureCredentialVault.cs) | Windows DPAPI encryption with custom entropy | **REUSE** for storing JWT Access and Refresh tokens |
| **Settings & Config** | [`EDM/Services/SettingsService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SettingsService.cs) | SQLite/JSON persistent settings in `%APPDATA%\EDM\` | **EXTEND** with `ControlPlaneApiUrl`, `InstallationId`, and `TelemetryOptIn` |
| **Download Engine** | [`EDM/Services/DownloadService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/DownloadService.cs), `DownloadOrchestrator.cs` | Segmented/Adaptive download workers | **REUSE** & hook telemetry events on download state transitions |
| **Client Updater** | [`EDM/Services/UpdateService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/UpdateService.cs) | Manifest parsing, SHA-256 verification, Authenticode check | **EXTEND** to query `/api/v1/updates/check` with fallback |
| **Application Lifecycle**| [`EDM/App.xaml.cs`](file:///d:/Update%20EDM/EDM/EDM/App.xaml.cs) | DI container, startup, background task orchestration | **EXTEND** to register `ControlPlaneClient` and start background tasks |

---

## 2. Reusable Services vs. New Services Needed

- **Do Not Duplicate**:
  - `SharedHttpClient.cs` (Already optimized for .NET 10 SocketsHttpHandler).
  - `SecureCredentialVault.cs` (Already provides DPAPI encryption and token redaction).
  - `SettingsService.cs` (Already persists settings in `%APPDATA%\EDM\`).
  - `UpdateService.cs` (Already handles file integrity and signature checking).
- **Single Integrated Client to Create**:
  - `EDM/Services/ControlPlaneClient.cs`: Unified client for Device Identity, Authentication, Token Refresh, Account/Ban status checks, Telemetry batch queue, and Update checking.
  - `EDM/Services/ControlPlaneTelemetryService.cs`: Non-blocking, bounded background telemetry queue buffer.

---

## 3. Call Graph for Key Integrations

```
[ EDM Application Startup / Lifecycle ]
        │
        ├──► [ Load Settings & DPAPI Tokens ] (SecureCredentialVault)
        ├──► [ Get / Generate Stable InstallationId ] (Privacy-Safe GUID)
        │
        ├──► [ ControlPlaneClient ]
        │         ├── POST /api/v1/auth/login or /api/v1/auth/refresh
        │         ├── GET  /api/v1/auth/me (Check Active vs Banned)
        │         └── POST /api/v1/updates/check (Check Version & SHA-256)
        │
[ Download Lifecycle (Start / Complete / Fail) ]
        │
        └──► [ ControlPlaneTelemetryService ] (Bounded Queue)
                  │ (Asynchronous / Non-blocking)
                  └──► POST /api/v1/telemetry/event
```

---

## 4. Offline-First Resilience Principles

1. **Zero Point of Failure**: If `EDM.ControlPlane.Api` is unreachable, offline, or returns 500/timeout, normal file downloading proceeds unhindered.
2. **Network Error $\neq$ Ban**: A network outage or unreachable API will never mark an account as suspended/banned.
3. **No UI Freezing**: All Control Plane calls run strictly asynchronously off the WPF Dispatcher thread with bounded retries and timeouts.
