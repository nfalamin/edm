# EDM Extension — Verification & Testing Harness

## 1. Test Tier Overview

| Test Category | Location | Runner | Purpose |
| :--- | :--- | :--- | :--- |
| **Unit Tests (JS)** | `extension/tests/unit/` | In-process test runner | Validates MessageRouter, schemas, SecurityValidator, and protocol envelopes |
| **Browser Integration** | `tools/TestBrowserIntegration.ps1` | PowerShell + .NET VSTest | Validates manifest permissions, background script contracts, and C# manifest generators |
| **Native Messaging Stdio** | `tools/TestNativeMessaging.ps1` | PowerShell stdio pipe | Tests 32-bit LE binary framing with `ping` and `resolve_media_variants` |
| **Real Video Detection E2E**| `tools/TestVideoDetectionE2E.ps1` | PowerShell + Live Server | Tests SPA video sniffing, HLS/DASH manifest resolution, and stream downloads |
| **Real Browser Integration E2E**| `tools/TestRealBrowserIntegrationE2E.ps1` | PowerShell + Headless Chrome | 17-point certification for registry keys, downloads interception, and browser launch |

## 2. Test Execution Commands

```powershell
# 1. Run Browser Packaging & Manifest Tests
powershell.exe -ExecutionPolicy Bypass -File tools/TestBrowserIntegration.ps1

# 2. Run Native Messaging Framing Tests
powershell.exe -ExecutionPolicy Bypass -File tools/TestNativeMessaging.ps1

# 3. Run Video Detection E2E Tests
powershell.exe -ExecutionPolicy Bypass -File tools/TestVideoDetectionE2E.ps1

# 4. Run Full 17-Point Real Browser Certification Harness
powershell.exe -ExecutionPolicy Bypass -File tools/TestRealBrowserIntegrationE2E.ps1
```
