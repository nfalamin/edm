# STAGE 5 — EXISTING EDM ARCHITECTURE DISCOVERY & REUSE MATRIX

**Document Version:** 1.0.0  
**Audit Date:** 2026-08-15  
**Scope:** Forensic Audit of Existing Desktop (.NET 10.0 WPF), Native Host, Extension, and Shared Services  

---

## 1. Existing Solution & Project Structure

The master solution [`EDM.slnx`](file:///d:/Update%20EDM/EDM/EDM.slnx) organizes the ecosystem into the following projects:

| Project | Target Framework | Purpose & Responsibilities |
| :--- | :--- | :--- |
| **`EDM/EDM.csproj`** | `net10.0-windows7.0` | Desktop WPF client, download orchestrator, UI progress windows, DPAPI credential vault, settings, scheduler, video sniffer UI. |
| **`EDM.NativeHost/EDM.NativeHost.csproj`** | `net10.0-windows7.0` | 32-bit LE stdio JSON-framed Native Messaging Host for Chrome, Edge, and Firefox extensions with IPC named pipe handoff. |
| **`EDM.Tests/EDM.Tests.csproj`** | `net10.0-windows7.0` | xUnit automated regression and real E2E integration test suite. |
| **`extension/`** | WebExtensions / MV3 | Browser content scripts, background workers, and in-page video sniffer widgets for Chrome, Edge, and Firefox. |
| **`tools/`** | PowerShell 7 / Windows PS | Build, packaging, Authenticode code-signing, and test runner harnesses. |

---

## 2. Existing Services Audit & Reuse Strategy

| Existing Subsystem / File | Exact File Path | Existing Responsibility | Stage 5 Decision | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **Credential Storage** | [`EDM/Services/SecureCredentialVault.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SecureCredentialVault.cs) | Windows DPAPI encryption with custom entropy (`EDM.CredentialVault.v1`), stored in `%APPDATA%\EDM\vault.dat`. | **REUSE & EXTEND** | Desktop client credentials stay in DPAPI vault; Control Plane uses Argon2id for server-side auth. |
| **Client Update Service** | [`EDM/Services/UpdateService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/UpdateService.cs) | JSON update manifest parsing, RSA signature validation, and SHA-256 binary checksum validation. | **REUSE & EXTEND** | Existing client logic will consume the new Control Plane `/api/v1/updates/check` API endpoint. |
| **HTTP Transport** | [`EDM/Services/SharedHttpClient.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SharedHttpClient.cs) | SocketsHttpHandler connection pooling (64–128 sockets), 16MB HTTP/2 window, zero-copy buffer streaming. | **REUSE** | High-performance peer-to-source HTTP engine for direct downloads. |
| **Download Pipeline** | [`EDM/Services/MultiPartDownloader.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/MultiPartDownloader.cs) | 2–32 parallel segment splitting, dynamic boundary reallocation, and disk pre-allocation. | **REUSE** | Downloads remain strictly direct from source; Control Plane will never proxy payloads. |
| **Application Settings** | [`EDM/Services/SettingsService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SettingsService.cs) | SQLite/JSON persistent settings storage. | **REUSE & EXTEND** | Add control plane endpoint URLs and telemetry opt-in flags. |
| **Logging Subsystem** | [`EDM/Services/LoggingService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/LoggingService.cs) | File and console structured diagnostic logger with credential redaction. | **REUSE** | Redacts sensitive data before writing logs. |
| **Diagnostics & Telemetry** | [`EDM/Services/DownloadAnalyticsEngine.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/DownloadAnalyticsEngine.cs) | Aggregates download success rates, bandwidth, and segment health. | **EXTEND** | Format events for batch transmission to Control Plane telemetry ingestion endpoint. |
| **Native Host Installer** | [`EDM/Services/NativeHostInstaller.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/NativeHostInstaller.cs) | Windows registry registration for Chrome, Edge, and Firefox. | **REUSE** | Unaltered, registers `knldjmfmopnppmllpmhedemckgbmgbfm`. |
| **Authenticode Verifier** | [`EDM/Services/AuthenticodeVerifier.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/AuthenticodeVerifier.cs) | Checks binary code-signing signatures before update execution. | **REUSE** | Validates downloaded updates prior to invocation. |

---

## 3. Control Plane Architectural Boundary

```
+-----------------------------------------------------------------------------------+
|                                EDM ECOSYSTEM                                      |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Browser Extension ] <── Native Messaging ──> [ EDM Desktop App ]               |
|                                                          │                        |
|                                                          │ (Direct HTTP/FTP)      |
|                                                          ▼                        |
|                                                [ Source Web Server ]              |
|                                                          ▲                        |
|                                                          │ (Direct Payload)       |
|                                                          │                        |
|  [ EDM Desktop App ] ─── HTTPS / REST API ───► [ Central Control Plane API ]      |
|  (Telemetry / Auth / Updates / Policies)                │                         |
|                                                         ▼                         |
|                                                [ PostgreSQL / SQLite ]            |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

- **Zero Proxying:** The Central Control Plane API handles metadata, releases, update policies, authentication, and anonymized telemetry. Actual binary downloads flow strictly between EDM Desktop and the target host.
