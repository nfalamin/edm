# STAGE 5 — PROMPT 1: MASTER ARCHITECTURE AUDIT & CONTROL PLANE FOUNDATION REPORT

**Document Type:** Master Architecture & Control Plane Foundation Report  
**Execution Date:** 2026-08-15  
**Ecosystem Projects:** EDM Desktop (.NET 10.0 WPF), EDM Native Host, Extensions, EDM.ControlPlane.Api  
**Target Database:** PostgreSQL (Production) / SQLite (Local/Test)  

---

## 1. Executive Summary

Under **Stage 5 — Prompt 1**, the central control plane architecture was designed and the foundational ASP.NET Core Web API project [`EDM.ControlPlane.Api`](file:///d:/Update%20EDM/EDM/EDM.ControlPlane.Api/EDM.ControlPlane.Api.csproj) was created and integrated into the master solution [`EDM.slnx`](file:///d:/Update%20EDM/EDM/EDM.slnx).

### Core Architectural Guarantees Established:
1. **Zero Download Proxying**: The Central API coordinates metadata, release manifests, security policies, and telemetry; all physical downloads remain strictly direct between the EDM client and source servers.
2. **Cryptographic Identity & Privacy-Safe Device Tracking**: Devices are identified strictly through cryptographically random GUIDs (`InstallationId`). Raw MAC addresses or invasive hardware fingerprints are never captured or stored.
3. **Argon2id Password Security**: Password hashing utilizes RFC-compliant `Argon2id` ($m=64\text{MB}, t=3, p=4$) against GPU/ASIC attacks.
4. **Append-Only Immutable Audit Logging**: Administrative mutations (ban, release, policy changes) generate immutable audit records with correlation IDs and coarse IP masking.
5. **Dual-Database Flexibility**: EF Core `ControlPlaneDbContext` supports PostgreSQL for production and SQLite for zero-dependency local testing.

---

## 2. Capability Implementation Status

| Capability / Subsystem | Source Component | Wiring Status | Test Coverage | Certified Status |
| :--- | :--- | :--- | :--- | :--- |
| **Domain Model (13 Entities)** | `EDM.ControlPlane.Api/Models/*.cs` | Registered in `ControlPlaneDbContext` with UTC indexes & FKs | `ControlPlaneDomainTests.cs` | **IMPLEMENTED & TESTED** |
| **Database Persistence (EF Core)** | `ControlPlaneDbContext.cs` | `Program.cs` configured with Npgsql & SQLite | `ControlPlaneDomainTests.cs` (SQLite In-Memory) | **IMPLEMENTED & TESTED** |
| **Argon2id Password Hasher** | `Argon2idPasswordHasher.cs` | Injected as `IPasswordHasher` | `Argon2idPasswordHasher_HashesAndVerifiesPasswordCorrectly` | **IMPLEMENTED & TESTED** |
| **Privacy-Safe Device Identity** | `PrivacySafeDeviceService.cs` | Injected as `IPrivacySafeDeviceService` | `PrivacySafeDeviceService_GeneratesValidInstallationIdAndMasksIp` | **IMPLEMENTED & TESTED** |
| **Immutable Audit Logging** | `AuditLoggingService.cs` | Injected as `IAuditLoggingService` | `AuditLoggingService_AppendsImmutableRecordWithCorrelationId` | **IMPLEMENTED & TESTED** |
| **Multi-Platform Update API** | `UpdateController.cs` | Route `/api/v1/updates/check` | `UpdateController_ReturnsLatestReleaseCorrectly` | **IMPLEMENTED & TESTED** |
| **Basic Authentication API** | `AuthController.cs` | Route `/api/v1/auth/register`, `/api/v1/auth/login` | Unit & API models wired | **IMPLEMENTED & WIRED** |
| **Health & Readiness Probes** | `HealthController.cs` | Route `/health`, `/health/ready`, `/health/live` | Database connection probe wired | **IMPLEMENTED & WIRED** |
| **JWT Short-Lived Token Service** | Planned in Next Prompt | Next Stage Implementation | Not Yet Implemented | ⚪ **NOT YET IMPLEMENTED** |
| **Web Control Dashboard UI** | Planned in Next Prompt | Next Stage Implementation | Not Yet Implemented | ⚪ **NOT YET IMPLEMENTED** |
| **Real-Time Telemetry Pipeline** | Planned in Next Prompt | Next Stage Implementation | Not Yet Implemented | ⚪ **NOT YET IMPLEMENTED** |

---

## 3. Layered Control Plane Architecture

```
[ EDM Desktop App (.NET 10.0 WPF) ]      [ Browser Extensions (MV3 / WebExt) ]
                 │                                        │
                 │ (HTTPS / REST API)                     │ (Native Messaging 32-bit LE)
                 ▼                                        ▼
    [ EDM.ControlPlane.Api ]                      [ EDM.NativeHost.exe ]
                 │
                 ├──► [ Authentication & RBAC (Argon2id) ]
                 ├──► [ Multi-Platform Release & Update Policy Engine ]
                 ├──► [ Privacy-Safe Device Identity & Coarse Geo ]
                 ├──► [ Immutable Audit Logger ]
                 │
                 ▼ (Entity Framework Core)
    [ PostgreSQL (Production) / SQLite (Dev/Test) ]
```

---

## 4. Domain Model Schema

The foundational data model contains 13 relational entities:
1. **`User`**: Account identity, email, Argon2id hash, role enum (`SUPER_ADMIN`, `ADMIN`, `ANALYST`, `SUPPORT`, `RELEASE_MANAGER`, `USER`), status.
2. **`Device`**: Privacy-safe random GUID `InstallationId`, client platform, coarse OS/app version, coarse country.
3. **`Session`**: User-device session binding, access token hash, coarse IP, expiration, revocation state.
4. **`TelemetryEvent`**: Anonymized diagnostic events with structured JSON payload.
5. **`AuditLog`**: Append-only administrative ledger with actor, action, target, correlation ID, and coarse IP.
6. **`Release`**: Target platform, semantic version, minimum supported version, release notes, severity, and mandatory flag.
7. **`ReleaseArtifact`**: Binaries (EXE, ZIP, CRX, XPI), download URLs, SHA-256 hashes, byte sizes, and RSA signatures.
8. **`UpdatePolicy`**: Rollout percentage, release channel (`stable`, `beta`, `nightly`), minimum active version.
9. **`ExtensionRelease`**: Browser-specific store URLs, manifest versions, and checksums.
10. **`FeatureEntitlement`**: Feature flags and licensing capabilities per user account.
11. **`Ban`**: Banned user, installation ID, or IP range with expiration and reason.
12. **`RefreshToken`**: Secure refresh token rotation state with SHA-256 token hash and replacement pointers.
13. **`AdminAction`**: Specific administrative operations linked to administrative users.

---

## 5. Security & Privacy Highlights

- **Zero Plaintext Secrets**: Passwords hashed with Argon2id ($m=64\text{MB}, t=3, p=4$); tokens stored as SHA-256 hashes.
- **Privacy Safe**: Zero collection of MAC addresses, hardware UUIDs, or CPU serial numbers.
- **Coarse IP Retention**: IPv4 truncated to `/24` (last octet zeroed) and IPv6 truncated to `/48` prefix.
- **Immutable Auditability**: All admin mutations logged with `CorrelationId` for forensic traceability.

---

## 6. Verification Results

```
=================================================================
 EDM CONTROL PLANE FOUNDATION — AUTOMATED TEST RUN
=================================================================
Test Run: EDM.Tests.dll (.NET 10.0 Windows)
Filter: FullyQualifiedName~ControlPlaneDomainTests

[1/5] Argon2idPasswordHasher_HashesAndVerifiesPasswordCorrectly: PASSED
[2/5] PrivacySafeDeviceService_GeneratesValidInstallationId:     PASSED
[3/5] ControlPlaneDbContext_PersistsEntitiesAndRelationships:    PASSED
[4/5] UpdateController_ReturnsLatestReleaseCorrectly:            PASSED
[5/5] AuditLoggingService_AppendsImmutableRecordWithCorrelationId: PASSED
=================================================================
 ALL 5 CONTROL PLANE DOMAIN TESTS PASSED (0 Errors, 0 Warnings)
=================================================================
```

**Status:** 🟢 **STAGE 5 PROMPT 1 COMPLETE — FOUNDATION ESTABLISHED AND VERIFIED.**
