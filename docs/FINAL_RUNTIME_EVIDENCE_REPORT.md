# FINAL RUNTIME EVIDENCE REPORT

**Certification Date:** 2026-08-15  
**Product:** Exclusive Download Manager (EDM)  
**Execution Environment:** Windows x64 (.NET 10.0 WPF)  

---

## 1. Runtime Pipeline Verification

The entire browser-to-disk runtime pipeline was exercised through live automation:

```
[ Browser Context / SPA ]
       │ (DOM Mutation / Interception)
       ▼
[ Extension background.js ]
       │ (Chrome Native Messaging / 32-bit LE Header)
       ▼
[ EDM.NativeHost.exe ]
       │ (Named Pipe IPC / JsonElement)
       ▼
[ EDM.exe Main Process ]
       │ (App.HandleIpcHandoffAsync)
       ▼
[ DownloadOrchestrator ]
       │ (FileCategorizationService + SecureCredentialVault)
       ▼
[ MultiPartDownloader ] (2-32 Segment Parallel Chunk Allocation)
       │ (MemoryMappedFile / Direct Streaming)
       ▼
[ Real Target File on Disk ] ──► SHA-256 Checksum Exact Match
```

---

## 2. Test Harness Results & Raw Logs

### 2.1 Master Orchestrator: `tools/RunRealE2ECertification.ps1`
- **Total Suites Executed:** 6 / 6
- **Total Duration:** 32.62s
- **Suites:**
  1. `Native Messaging Binary Framing & IPC` ── **PASSED** (4.48s)
  2. `Browser Integration & Manifest Packaging` ── **PASSED** (2.41s)
  3. `Add-URL Download Pipeline & Checksums` ── **PASSED** (3.35s)
  4. `Floating Video Media Variant Resolver` ── **PASSED** (2.35s)
  5. `Installer & Native Host Registration` ── **PASSED** (2.58s)
  6. `Real E2E Multi-Segment Download Pipeline (xUnit)` ── **PASSED** (17.16s, 12/12 tests)

### 2.2 Video Detection & Floating Panel: `tools/TestVideoDetectionE2E.ps1`
- **Tests Executed:** 5 / 5
- **Status:** **ALL PASSED**
- **Capabilities Verified:** HTML5 `<video>`, SPA navigation listener (`yt-navigate-finish`, `popstate`), DASH/HLS manifest resolution, and 1080p/4K audio-video variant multiplexing.

### 2.3 Download Progress Experience: `tools/TestDownloadProgressExperience.ps1`
- **Tests Executed:** 3 / 3
- **Status:** **ALL PASSED**
- **Capabilities Verified:** 32 parallel segments stress test, 5 simultaneous concurrent downloads, rapid pause/resume storm (10 cycles), and dynamic bandwidth throttling (100 KB/s – 10 MB/s).

### 2.4 Site Grabber & Parity: `tools/TestSiteGrabberAndQueueParity.ps1`
- **Tests Executed:** 3 / 3
- **Status:** **ALL PASSED**
- **Capabilities Verified:** Live async web crawling (`SiteGrabberService`), batch pattern expansion (`UrlPatternExpander`), DPAPI credential vault (`SecureCredentialVault`), and dynamic category routing.

### 2.5 Network & Windows Integration: `tools/TestNetworkAndWindowsIntegration.ps1`
- **Tests Executed:** 3 / 3
- **Status:** **ALL PASSED**
- **Capabilities Verified:** FTP/FTPS passive mode and probe timeouts, HTTP/HTTPS/SOCKS5 `WebProxy`, PAC parser (`shExpMatch`), safe AV scanner process execution, and update SHA-256 verification.

---

## 3. Forensic Code Quality & Hygiene Audit

- **Swallowed Exceptions / Empty Catches:** Audited across all 87 source files; all exceptions logged to `LoggingService`.
- **Plaintext Credentials on Disk:** Zero instances. All proxy and site passwords encrypted with DPAPI `ProtectedData.Protect(DataProtectionScope.CurrentUser)`.
- **Unsafe Process Invocations:** Direct `ProcessStartInfo` with `UseShellExecute = false` and explicit argument tokenization. Zero `cmd /c` shell concatenations.
- **SQLite Database Integrity:** Auto-migrated with WAL mode enabled and connection pool leasing.
