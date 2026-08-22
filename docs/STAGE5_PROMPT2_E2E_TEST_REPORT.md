# STAGE 5 — PROMPT 2: END-TO-END AUTOMATED TEST REPORT

**Execution Date:** 2026-08-15  
**Test Harness:** xUnit 2.9.3 (.NET 10.0 Windows) + WebApplicationFactory  
**Suite Status:** 🟢 **16/16 TESTS PASSED (100% PASS RATE)**  

---

## 1. Automated Test Results

```
=================================================================
 CONTROL PLANE DOMAIN, SECURITY & ANALYTICS TEST RESULTS
=================================================================
Test Run: EDM.Tests.dll (.NET 10.0 Windows)
Filter: FullyQualifiedName~ControlPlane

[PASS] Argon2idPasswordHasher_HashesAndVerifiesPasswordCorrectly
[PASS] PrivacySafeDeviceService_GeneratesValidInstallationIdAndMasksIp
[PASS] ControlPlaneDbContext_PersistsEntitiesAndRelationshipsInSqlite
[PASS] UpdateController_ReturnsLatestReleaseCorrectly
[PASS] AuditLoggingService_AppendsImmutableRecordWithCorrelationId
[PASS] FullAuthLifecycle_Register_Login_ProtectedAccess_Refresh_ReuseDetection
[PASS] AntiIDOR_UserCannotAccessOrRevokeOtherUsersSessions
[PASS] RBAC_RegularUserCannotAccessAdminEndpoints
[PASS] BanEnforcement_ActiveBanBlocksAuthenticatedRequests
[PASS] PasswordChange_InvalidatesOtherSessions
[PASS] Concurrency_SimultaneousRefresh_OnlyOneSucceeds
[PASS] Telemetry_RecordsValidEvent_And_RejectsDisallowedEvent
[PASS] DashboardSummary_ReturnsAccurateAggregatesFromDatabase
[PASS] Analytics_ReturnsDownloadAndPlatformMetrics
[PASS] ReleaseManagement_CreateAndArchiveRelease_Workflow
[PASS] DeviceAndSessionInspection_ReturnsData

Summary: 16 Passed, 0 Failed, 0 Skipped, Duration: 9s
=================================================================
 ALL 16 TESTS PASSED - 100% SUCCESS
=================================================================
```

---

## 2. Real E2E Pipeline Workflow Verified

1. **Telemetry Event Ingestion**: Verified `download_completed` persists with structured JSON, while disallowed event types are rejected with 400 Bad Request.
2. **Dashboard Summary Aggregates**: Real counts of active users, sessions, devices, downloads, and current release version returned directly from SQLite database.
3. **Analytics Metrics & Time Buckets**: Dynamic range calculation (`7d`, `30d`, `90d`, `1y`) correctly groups download completions and user registrations.
4. **Release Lifecycle**: End-to-end test confirmed that creating release `3.x.0` with artifacts makes it available on `/api/v1/updates/check`, and calling `/archive` withdraws the release.
5. **Session & Device Visibility**: Verified administrative inspection of devices and active sessions.
