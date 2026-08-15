# Exclusive Download Manager (EDM) - Known Issues & Performance Report

This document records performance benchmarks, stress testing results, and open minor issues for future iterations.

---

## Performance & Stress Testing Summary

Stress test suite executed via `StressTestProgram` (`EDM.Tests/Services/StressTestHarnessTests.cs`):

| Metric | Result | Benchmark Threshold | Status |
| :--- | :--- | :--- | :--- |
| **Concurrent Downloads** | 25 Parallel Downloads | 20+ Active Streams | **PASS** |
| **Memory Delta (Pre/Post)** | `+4.12 MB` | `< 15.00 MB` | **PASS** (Zero Leaks) |
| **Pause/Resume Toggles** | 10 Cycles @ 100ms | Rapid State Shifts | **PASS** (No Deadlock) |
| **Cancellation Handling** | 2 Streams Cancelled | Graceful Failure | **PASS** (Clean Cleanup) |
| **Disk Write Exception** | `UnauthorizedAccessException` | Graceful UI Dialog | **PASS** (No Crash) |

---

## Known Minor Issues & Future Enhancement Roadmap

### 1. `MpCmdRun.exe` Elevation Requirements on Hardened Systems
- **Description**: On certain enterprise-managed Windows systems with User Account Control (UAC) hardening, invoking Windows Defender CLI (`MpCmdRun.exe -ScanType 3`) from a non-elevated application context may produce return code `1` (Access Denied).
- **Workaround/Impact**: `SafeBrowsingService.cs` catches this condition gracefully, logs the notice to `LoggingService`, and allows the download to finish without blocking the user.

### 2. High Connection Density on Low-End Network Adapters
- **Description**: Running 16+ parallel download segments per file across 5+ simultaneous downloads (80+ total TCP sockets) can trigger socket exhaustion on older Wi-Fi adapters or low-bandwidth routers.
- **Mitigation**: `AdaptiveConnectionManager.determineConnectionCountAsync()` dynamic connection throttling automatically steps down per-file connections based on metered/mobile network type detection.
