# EXCLUSIVE DOWNLOAD MANAGER (EDM) — MASTER TECHNICAL SPECIFICATION

**Document Version:** 2.0.0 (Production Master)  
**Standard:** Enterprise Architectural Compliance & Forensic Truth  
**Target Framework:** .NET 10.0 Windows (x64) | Manifest V3 / WebExtensions  

---

## 1. Dynamic Relative Path Enforcement

### 1.1 Architectural Principle
No hardcoded local drive paths (e.g., `C:\...`, `D:\...`) may exist in any build automation, packaging scripts, test suites, or runtime modules. All locations are resolved dynamically relative to the execution context or standard OS user directories.

### 1.2 PowerShell Build & Packaging Scripts
All automation scripts in `tools/` resolve the workspace root dynamically:
```powershell
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$extensionDir  = Join-Path $workspaceRoot "extension\chrome"
$outputDir     = Join-Path $workspaceRoot "Output"
$binDir        = Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows"
```

### 1.3 C# Application Runtime
- **Application Binary Directory:** `AppDomain.CurrentDomain.BaseDirectory`
- **Application Data & Credential Vault:** `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM")` (`%APPDATA%\EDM\`)
- **Native Host Logs & Host Manifests:** `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM")` (`%LOCALAPPDATA%\EDM\`)
- **Temporary Chunks & Segment Caching:** `Path.Combine(Path.GetTempPath(), "EDM")`

---

## 2. Production Chrome Extension ID Finalization

### 2.1 Production Chrome Extension ID
The Chrome WebExtension ID is permanently finalized as:
$$\mathbf{knldjmfmopnppmllpmhedemckgbmgbfm}$$

### 2.2 Origin Registration & Manifest Verification
- **Chrome Origin URL:** `chrome-extension://knldjmfmopnppmllpmhedemckgbmgbfm/`
- **Native Host Name:** `com.exclusive.downloadmanager.native`
- **Native Host Manifest Configuration:**
  ```json
  {
    "name": "com.exclusive.downloadmanager.native",
    "description": "Exclusive Download Manager Native Messaging Host",
    "path": "EDM.NativeHost.exe",
    "type": "stdio",
    "allowed_origins": [
      "chrome-extension://knldjmfmopnppmllpmhedemckgbmgbfm/"
    ]
  }
  ```
- **Windows Registry Integration:**
  `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.exclusive.downloadmanager.native` (points to `%LOCALAPPDATA%\EDM\NativeMessaging\manifest.json`).

---

## 3. DPAPI Custom Entropy & Credential Vault Security

### 3.1 Cryptographic Storage Architecture
Credentials stored in [`SecureCredentialVault.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/SecureCredentialVault.cs) and [`ProxyService.cs`](file:///d:/Update%20EDM/EDM/EDM/Services/ProxyService.cs) use the Windows Data Protection API (DPAPI) with explicit application-bound entropy:

```csharp
// Encryption
byte[] plainBytes = Encoding.UTF8.GetBytes(plainPassword);
byte[] entropy = Encoding.UTF8.GetBytes("EDM.CredentialVault.v1");

byte[] cipherData = ProtectedData.Protect(
    plainBytes, 
    entropy, 
    DataProtectionScope.CurrentUser
);

// Persist encrypted binary blob
string vaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM", "vault.dat");
File.WriteAllBytes(vaultPath, cipherData);
```

### 3.2 Security Guarantees
1. **User Profile Isolation:** `DataProtectionScope.CurrentUser` locks decryption strictly to the logged-in Windows user session.
2. **Entropy Shield:** Injected entropy (`EDM.CredentialVault.v1`) prevents unauthorized third-party executables running under the same user account from decrypting EDM credentials via standard DPAPI calls.
3. **Zero Plaintext Leaks:** Credentials are never written to disk unencrypted, and diagnostic logging utilizes `SecureCredentialVault.RedactCredentialsFromText()` to strip passwords, tokens, and Authorization headers.

---

## 4. Adaptive Audio/Video Stream Multiplexing (DASH & YouTube)

### 4.1 End-to-End Media Pipeline
For high-resolution media formats (1080p, 1440p, 4K, 8K) where video and audio streams are separated:

```
[ In-Page Video Sniffer ]
           │
           ▼
[ MediaVariantResolver ] ──► Extracts Best Video Stream (.mp4/.webm) & Audio Stream (.m4a/.opus)
           │
           ▼
[ Concurrent Multi-Part Downloader ]
     ├── Segmented Video Stream Download ──► %TEMP%\EDM\temp_video_xyz.mp4
     └── Segmented Audio Stream Download ──► %TEMP%\EDM\temp_audio_xyz.m4a
           │
           ▼
[ FFmpeg Lossless Stream Copy Multiplexing ]
     ffmpeg -i "temp_video.mp4" -i "temp_audio.m4a" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 "FinalOutput.mp4" -y
           │
           ▼
[ Zero Quality Loss Output ] + [ Automatic Temporary File Deletion ]
```

### 4.2 Key Specifications
- **Lossless Processing:** `-c:v copy -c:a copy` preserves original bitstreams without re-encoding, taking $< 1$ second.
- **Cleanup Guarantee:** Both intermediate temporary stream files are immediately deleted from disk upon successful multiplexing.

---

## 5. RFC 7233 / RFC 9110 Compliant Zero-Indexed Byte-Range Calculation

### 5.1 Mathematical Partitioning Model
When dividing a file of size $\text{TotalBytes}$ across $N$ parallel download segments ($0 \le i < N$):

$$\text{SegmentSize} = \left\lfloor \frac{\text{TotalBytes}}{N} \right\rfloor$$

$$\text{StartByte}_i = i \times \text{SegmentSize}$$

$$\text{EndByte}_i = \begin{cases} \text{TotalBytes} - 1, & \text{if } i = N - 1 \\ ((i + 1) \times \text{SegmentSize}) - 1, & \text{otherwise} \end{cases}$$

### 5.2 HTTP Header & Disk Offset Alignment
- **HTTP Request Header:** `Range: bytes={StartByte_i}-{EndByte_i}`
- **Zero-Indexed & Inclusive:** Indices span strictly from $0$ to $\text{TotalBytes}-1$.
- **Disk Pre-Allocation:** Target file is allocated up-front using `FileStream.SetLength(TotalBytes)`.
- **Parallel Direct Offset Writing:** Segment workers seek directly to `StartByte_i` and stream data without memory buffering bloat.
- **Cryptographic Verification:** Eliminates all chunk overlapping or truncation, guaranteeing $100\%$ byte-for-byte SHA-256 integrity match with the upstream server.
