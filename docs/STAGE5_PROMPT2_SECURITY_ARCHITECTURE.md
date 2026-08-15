# STAGE 5 — PROMPT 2: PRODUCTION-GRADE SECURITY ARCHITECTURE SPECIFICATION

**Document Version:** 2.0.0  
**Status:** IMPLEMENTED & INTEGRATION VERIFIED  
**Target Projects:** `EDM.ControlPlane.Api`, `EDM.Tests`, `EDM` WPF Desktop  

---

## 1. Authentication Architecture

The EDM Control Plane implements a dual-token, cryptographically guarded authentication model:
- **Short-Lived Access Tokens (JWT)**: Valid for 15 minutes, signed with HMAC-SHA256, carrying standard identity claims (`sub`, `unique_name`, `email`, `jti`), session tracking (`session_id`, `family_id`), and role-based permissions (`role`).
- **Cryptographically Rotated Refresh Tokens**: 256-bit cryptographically secure random entropy (`RandomNumberGenerator.Fill`), valid for 30 days. Only the SHA-256 hash is persisted in the database.
- **Server-Side Session Records**: Active sessions are bound to a specific user and device with last-activity timestamps and idle timeout (7 days).

---

## 2. Refresh-Token Lifecycle & Reuse Detection

```
[ Client ]                            [ Control Plane Auth Service ]
    │                                              │
    ├─── POST /api/v1/auth/refresh (Token R1) ────►│ Check R1 hash in DB
    │                                              ├── Valid & Unused?
    │                                              │   ├── Mark R1 as IsUsed = true
    │                                              │   ├── Issue new R2 (same FamilyId)
    │                                              │   └── Issue new Access Token
    │◄── 200 OK (New Token R2 + Access Token) ─────┤
    │                                              │
  [ Attacker / Replay ]                            │
    │                                              │
    ├─── POST /api/v1/auth/refresh (Replay R1) ───►│ Check R1 in DB -> Already IsUsed!
    │                                              ├── SECURITY ALARM: Replay Detected!
    │                                              ├── Revoke ALL tokens with FamilyId
    │                                              ├── Invalidate current Session
    │                                              ├── Append AuditLog (high severity)
    │◄── 401 Unauthorized (TOKEN_REUSE) ───────────┤
```

- **Atomic Concurrency Protection**: Refresh operations are guarded by critical sections to eliminate race-condition window exploits.

---

## 3. Role-Based Access Control (RBAC) Matrix

| Endpoint / Operation | `SUPER_ADMIN` | `ADMIN` | `RELEASE_MANAGER` | `SUPPORT` | `ANALYST` | `USER` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **`POST /api/v1/admin/ban`** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **`POST /api/v1/admin/unban`** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **`POST /api/v1/admin/revoke-user-sessions`** | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| **`GET  /api/v1/admin/users`** | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| **`GET  /api/v1/admin/audit-logs`** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **`POST /api/v1/auth/change-password`** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **`GET  /api/v1/auth/sessions` (Own)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **`DELETE /api/v1/auth/sessions/{id}` (Own)**| ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

- **Anti-IDOR Enforcement**: A regular user can only view and revoke sessions belonging strictly to their own authenticated `userId`. Any cross-user manipulation attempts return `404 Not Found`.

---

## 4. Ban Enforcement Flow

1. Every incoming authenticated HTTP request passes through `BanEnforcementMiddleware`.
2. The middleware resolves the `userId`, `installation_id`, and coarse IP address.
3. If an active matching record is found in `Bans`:
   - Active session is instantly revoked (`ACCOUNT_BANNED`).
   - Request execution is halted and returns `HTTP 403 Forbidden` (`ACCESS_DENIED`).
   - Audit trail is logged.

---

## 5. Login Abuse & Rate Limiting

- Fixed-window rate limiting middleware applied on `/api/v1/auth/login`, `/api/v1/auth/register`, and `/api/v1/auth/refresh`.
- Default: 10 attempts per minute per IP address. Exceeded requests receive `HTTP 429 Too Many Requests`.

---

## 6. HTTP Security Headers

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- `Content-Security-Policy: default-src 'self' ...`
