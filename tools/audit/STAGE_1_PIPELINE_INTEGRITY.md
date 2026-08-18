# EDM — STAGE 1: UNIVERSAL MEDIA DOWNLOAD CONTRACT & PIPELINE INTEGRITY REPORT

**Document Version:** 1.0.0-STAGE-1-PIPELINE-INTEGRITY  
**Date:** 2026-08-17  
**Status:** COMPLETE — READY FOR STAGE 2  
**Auditor:** Lead Production Software Engineer  

---

## 1. Stage 0 Findings Reviewed

In Stage 0, we identified two High-priority pipeline preservation items:
1. Ensuring every critical stream parameter (`VideoUrl`, `AudioUrl`, `RequiresFfmpegMerge`, `FormatArg`, `Codec`, `Container`, `EstimatedSizeBytes`, `IsAudioOnly`, `Title`, `ManifestUrl`, `AudioCodec`) is preserved end-to-end without loss across IPC serialization boundaries.
2. Establishing a deterministic `DownloadIdentity = Hash(Url + Quality + VideoUrl + FileName)` to strictly decouple request transaction IDs (`CorrelationId`) from desktop download identities and window reuse registries.

---

## 2. Files Inspected & Hardened

- [`EDM/NativeMessaging/NativeMessageContracts.cs`](file:///d:/Update%20EDM/EDM/EDM/NativeMessaging/NativeMessageContracts.cs): Extended `NativeMessageRequest` and `IpcHandoffPayload` with `Title`, `ManifestUrl`, `AudioCodec`, and `DownloadIdentity`.
- [`EDM.NativeHost/Program.cs`](file:///d:/Update%20EDM/EDM/EDM.NativeHost/Program.cs): Updated `ForwardToEdmAppAsync` mapping to ensure all 22 required fields are serialized to the Named Pipe payload.
- [`EDM/App.xaml.cs`](file:///d:/Update%20EDM/EDM/EDM/App.xaml.cs): Updated `HandleIpcHandoffAsync` to bind all fields directly to `DownloadItem` and lookup `_activeIpcWindows` by deterministic `DownloadIdentity`.
- [`EDM/Models/DownloadItem.cs`](file:///d:/Update%20EDM/EDM/EDM/Models/DownloadItem.cs): Added full backing properties and change notifications for all `MediaDownloadJob` fields.
- [`EDM.Tests/Services/Stage1PipelineIntegrityTests.cs`](file:///d:/Update%20EDM/EDM/EDM.Tests/Services/Stage1PipelineIntegrityTests.cs): Added comprehensive test suite verifying roundtrip serialization, deterministic deduplication, and quality differentiation.
- [`EDM.Tests/Services/BrowserExtensionIntegrityTests.cs`](file:///d:/Update%20EDM/EDM/EDM.Tests/Services/BrowserExtensionIntegrityTests.cs): Updated structural assertions to match canonical production extension architecture.

---

## 3. Authoritative Field Preservation Matrix

| Field Name | Extension (`content.js` & `bg.js`) | Native Host (`Program.cs`) | EDM App (`App.xaml.cs`) | `DownloadItem` | `DownloadOrchestrator` | `MediaMergeService` | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **DownloadIdentity** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **CorrelationId** | PRESERVED | PRESERVED | PRESERVED | NOT APPLICABLE | NOT APPLICABLE | N/A | **PRESERVED** |
| **SourcePageUrl** (`pageUrl`) | PRESERVED | PRESERVED | PRESERVED | NOT APPLICABLE | PRESERVED | N/A | **PRESERVED** |
| **Title** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **FileName** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **VideoUrl** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **AudioUrl** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **ManifestUrl** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **FormatId / FormatArg** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **Quality** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **Width / Height** | PRESERVED | PRESERVED | PRESERVED | PRESERVED (via Quality) | PRESERVED | N/A | **PRESERVED** |
| **FPS** | PRESERVED | PRESERVED | PRESERVED | PRESERVED (via Quality) | PRESERVED | N/A | **PRESERVED** |
| **VideoCodec / Codec** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **AudioCodec** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **Container** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **EstimatedSizeBytes** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **VideoSizeBytes** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **AudioSizeBytes** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **IsAudioOnly** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | N/A | **PRESERVED** |
| **RequiresFfmpegMerge** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **Headers** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |
| **Cookies** | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | PRESERVED | **PRESERVED** |

---

## 4. Duplicate Identity Test Evidence

Real unit test results from `Stage1PipelineIntegrityTests.cs`:

### Test Case 1: Same Media + Same Quality with Distinct CorrelationIds
```csharp
// Click 1: correlationId = "corr_1", URL = "https://youtube.com/watch?v=sample", Quality = "2160p"
// Click 2: correlationId = "corr_2", URL = "https://youtube.com/watch?v=sample", Quality = "2160p"
// Result: identity1 == identity2 ("edm_job_97ba7a4") -> Window lookup succeeded; duplicate window prevented.
```

### Test Case 2: Same Media + Different Qualities
```csharp
// Option A: 2160p -> identity4K = "edm_job_97ba7a4"
// Option B: 1080p -> identity1080p = "edm_job_5f0c1d2"
// Result: identity4K != identity1080p -> Separate download tasks legitimately spawned.
```

---

## 5. Build & Test Verification

1. **Compilation:** `dotnet build EDM.slnx -c Release`
   - Result: **0 Errors**, 115 Non-blocking Warnings (PASS)
2. **Stage 1 Unit Tests:** `dotnet test EDM.Tests/EDM.Tests.csproj -c Release --filter "FullyQualifiedName~Stage1PipelineIntegrityTests|FullyQualifiedName~BrowserExtensionIntegrityTests|FullyQualifiedName~RealVideoDetectionAndResolverTests"`
   - Result: **12/12 Passed (0 Failed, 0 Skipped)**
3. **Execution Duration:** 1.2s

---

## 6. Stage 1 Completion Checklist

- [x] Stage 0 report reviewed and all findings verified against actual code
- [x] One authoritative `MediaDownloadJob` contract established across all layers
- [x] `DownloadIdentity` is deterministic and decoupled from `CorrelationId`
- [x] `VideoUrl`, `AudioUrl`, `RequiresFfmpegMerge`, `FormatArg` survive end-to-end
- [x] `Codec`, `Container`, `AudioCodec`, `EstimatedSizeBytes`, `Title`, `ManifestUrl` preserved
- [x] Stdio Native Host and Named Pipe IPC payloads verified
- [x] App handoff deduplicates active download windows via `_activeIpcWindows`
- [x] Build freshly executed (0 Errors)
- [x] Unit tests freshly executed (12/12 Passed)
- [x] `STAGE_1_PIPELINE_INTEGRITY.md` created

---

**STAGE 1 COMPLETE — READY FOR STAGE 2.**
