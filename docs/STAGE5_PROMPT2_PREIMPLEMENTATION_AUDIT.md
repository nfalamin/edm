# STAGE 5 — PROMPT 2: PRE-IMPLEMENTATION AUDIT

**Audit Date:** 2026-08-15  
**Document Version:** 1.0.0  
**Ecosystem Projects:** `EDM.ControlPlane.Api`, `EDM.ControlPlane.Dashboard`, `EDM.Tests`  

---

## 1. Existing Backend Endpoints & Models Status

### Database Entities in `ControlPlaneDbContext`
- `User` (Id, Username, Email, PasswordHash, Role, IsActive, CreatedAtUtc) — **Persisted**
- `Device` (Id, InstallationId, ClientType, OsVersion, AppVersion, LastSeenAtUtc) — **Persisted**
- `Session` (Id, UserId, DeviceId, FamilyId, AccessTokenHash, CoarseIp, ExpiresAtUtc, IsRevoked) — **Persisted**
- `RefreshToken` (Id, SessionId, UserId, DeviceId, FamilyId, TokenHash, IsUsed, IsRevoked) — **Persisted**
- `Ban` (Id, TargetType, TargetValue, Reason, BannedBy, IsActive, ExpiresAtUtc) — **Persisted**
- `AuditLog` (Id, ActorId, ActorUsername, Action, TargetEntity, TargetId, DetailsJson, CorrelationId, ResultStatus) — **Persisted**
- `Release` (Id, Platform, Version, MinimumSupportedVersion, Title, ReleaseNotes, IsMandatory, IsWithdrawn, Severity, PublishedAtUtc) — **Persisted**
- `ReleaseArtifact` (Id, ReleaseId, ArtifactName, DownloadUrl, Sha256Hash, FileSizeBytes, SignatureBase64) — **Persisted**
- `UpdatePolicy` (Id, Platform, Channel, RolloutPercentage, MinimumVersion, IsActive) — **Persisted**
- `ExtensionRelease` (Id, Browser, ExtensionVersion, MinBrowserVersion, ManifestVersion, StoreUrl, DirectZipUrl) — **Persisted**
- `FeatureEntitlement` (Id, UserId, FeatureCode, IsEnabled, ExpiresAtUtc) — **Persisted**
- `TelemetryEvent` (Id, DeviceId, EventName, EventPayloadJson, TimestampUtc) — **Persisted**
- `AdminAction` (Id, AdminUserId, ActionType, TargetUserId, DetailsJson, TimestampUtc) — **Persisted**

### Existing Endpoints
- `/health`, `/health/ready`, `/health/live` — **Implemented & Wired**
- `/api/v1/auth/register`, `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/auth/logout-all`, `/api/v1/auth/change-password`, `/api/v1/auth/me`, `/api/v1/auth/sessions` — **Implemented & Tested**
- `/api/v1/updates/check`, `/api/v1/updates/releases/{platform}` — **Implemented & Tested**
- `/api/v1/admin/ban`, `/api/v1/admin/unban`, `/api/v1/admin/users`, `/api/v1/admin/revoke-user-sessions`, `/api/v1/admin/audit-logs` — **Implemented & Tested**

---

## 2. Missing Functionality to Implement in Stage 5 Prompt 2

1. **Telemetry Ingestion API**:
   - `POST /api/v1/telemetry/event` (rate-limited, validated JSON payload, event allowlist: `download_completed`, `download_failed`, `video_detected`, `app_started`, `update_applied`).
2. **Admin Dashboard Summary & Analytics APIs**:
   - `GET /api/v1/admin/dashboard/summary` (real database aggregates: Total Users, Active Users, Registered Devices, Active Sessions, Total Downloads, Today Downloads, Current Releases, Security Events, Banned Accounts).
   - `GET /api/v1/admin/analytics/downloads?range=7d|30d|90d|1y`
   - `GET /api/v1/admin/analytics/users?range=7d|30d|90d|1y`
   - `GET /api/v1/admin/analytics/versions`
   - `GET /api/v1/admin/analytics/platforms`
   - `GET /api/v1/admin/analytics/activity`
   - `GET /api/v1/admin/analytics/security`
3. **Admin Release Management APIs**:
   - `GET /api/v1/admin/releases`
   - `POST /api/v1/admin/releases` (create draft release + artifacts)
   - `PUT /api/v1/admin/releases/{id}/publish` (transition to published, write audit log)
   - `PUT /api/v1/admin/releases/{id}/archive`
4. **Admin Device & Session Inspection APIs**:
   - `GET /api/v1/admin/devices` (paginated, search by installation ID or client type)
   - `GET /api/v1/admin/sessions` (paginated active sessions)
   - `POST /api/v1/admin/revoke-session/{sessionId}`
5. **Real Central Control Dashboard Frontend** in `EDM.ControlPlane.Dashboard/`:
   - `index.html` (Dark theme control center UI with sidebar navigation)
   - `styles.css` (Glassmorphism dark aesthetics matching EDM design system)
   - `app.js` (App lifecycle, view router, polling refresh)
   - `api.js` (Centralized fetch wrapper, token refresh, 401/403/429 handling, error toasts)
   - `auth.js` (Admin login/logout state management)
   - `users.js` (Users table, pagination, search, side panel modal, ban/unban, session reset)
   - `devices.js` (Device table, privacy-safe metadata, session counts)
   - `sessions.js` (Active session management, remote revocation)
   - `telemetry.js` (Real-time telemetry event stream)
   - `analytics.js` (Chart.js integration, date range selectors, dynamic metric cards)
   - `releases.js` (Release management, draft creation, artifact verification, publishing)
   - `security.js` (Security dashboard, ban management, failed login logs)
   - `settings.js` (System settings, update policy configuration)

---

## 3. Files to Create & Modify

### Modify
- [`EDM.ControlPlane.Api/Controllers/AdminController.cs`](file:///d:/Update%20EDM/EDM/EDM.ControlPlane.Api/Controllers/AdminController.cs) (Add dashboard summary, analytics, devices, sessions, and release management endpoints)
- [`EDM.ControlPlane.Api/Program.cs`](file:///d:/Update%20EDM/EDM/EDM.ControlPlane.Api/Program.cs) (Add CORS for dashboard origin, static files for dashboard hosting)

### Create
- `EDM.ControlPlane.Api/Controllers/TelemetryController.cs`
- `EDM.ControlPlane.Dashboard/index.html`
- `EDM.ControlPlane.Dashboard/styles.css`
- `EDM.ControlPlane.Dashboard/app.js`
- `EDM.ControlPlane.Dashboard/api.js`
- `EDM.ControlPlane.Dashboard/auth.js`
- `EDM.ControlPlane.Dashboard/users.js`
- `EDM.ControlPlane.Dashboard/devices.js`
- `EDM.ControlPlane.Dashboard/sessions.js`
- `EDM.ControlPlane.Dashboard/telemetry.js`
- `EDM.ControlPlane.Dashboard/analytics.js`
- `EDM.ControlPlane.Dashboard/releases.js`
- `EDM.ControlPlane.Dashboard/security.js`
- `EDM.ControlPlane.Dashboard/settings.js`
- `EDM.Tests/ControlPlane/ControlPlaneDashboardAndAnalyticsTests.cs`

---

## 4. Test & Verification Plan
- Unit and integration tests covering:
  - Telemetry event ingestion (valid payload, allowlist enforcement, oversized payload rejection)
  - Dashboard summary calculation with real DB data
  - Analytics range filtering (Today, 7d, 30d, 90d, 1y)
  - Release creation, artifact SHA-256 validation, and publishing workflow
  - Device and session listing with remote revocation
  - Error response standardization (`{ "error": { "code", "message", "correlationId" } }`)
