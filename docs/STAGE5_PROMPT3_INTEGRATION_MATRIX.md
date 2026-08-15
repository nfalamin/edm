# STAGE 5 — PROMPT 3: INTEGRATION MATRIX

**Evaluation Date:** 2026-08-15  
**Version:** 3.0.0  

---

| Capability | Existing Code | New Code | Caller | API | Runtime Tested | Result |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| **Stable Device Identity** | `SettingsService.cs` | `ControlPlaneClient.EnsureInstallationId()` | `App.OnStartup`, `ControlPlaneClient` | Internal / Settings | Yes | **VERIFIED** |
| **Authentication & Tokens**| `SecureCredentialVault.cs` | `ControlPlaneClient.LoginAsync()` | `App.OnStartup`, User UI | `POST /api/v1/auth/login` | Yes | **VERIFIED** |
| **Token Refresh Rotation** | `SecureCredentialVault.cs` | `ControlPlaneClient.RefreshSessionAsync()` | `ControlPlaneClient` | `POST /api/v1/auth/refresh` | Yes | **VERIFIED** |
| **Account Status & Ban**   | `SettingsService.cs` | `ControlPlaneClient.CheckAccountStatusAsync()` | `App.OnStartup`, `DownloadOrchestrator` | `GET /api/v1/auth/me` | Yes | **VERIFIED** |
| **Ban Download Blocking**  | `DownloadOrchestrator.cs` | Ban state check in `StartDownloadAsync` | `DownloadOrchestrator` | Client State / Event | Yes | **VERIFIED** |
| **Non-blocking Telemetry** | `DownloadOrchestrator.cs` | `ControlPlaneTelemetryService.cs` | `App.OnStartup`, `DownloadOrchestrator` | `POST /api/v1/telemetry/event` | Yes | **VERIFIED** |
| **Update Check Discovery** | `UpdateService.cs` | `UpdateService.CheckControlPlaneUpdateAsync()` | `UpdateService` | `POST /api/v1/updates/check` | Yes | **VERIFIED** |
| **Update Integrity (SHA)** | `FileIntegrityService.cs` | `UpdateService.cs` | `UpdateService` | Client Verification | Yes | **VERIFIED** |
| **Offline Resilience**     | `SharedHttpClient.cs` | `ControlPlaneClient.cs` | `App.OnStartup`, `DownloadOrchestrator` | Network Fallback | Yes | **VERIFIED** |
