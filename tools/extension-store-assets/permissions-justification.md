# Browser Extension Store Submission & Permissions Justification

This document provides detailed permission justifications, store metadata, and screenshot specifications required for submitting Exclusive Download Manager (EDM) to the **Chrome Web Store**, **Microsoft Edge Add-ons Store**, and **Mozilla Add-ons (AMO)**.

---

## 1. Minimal Permission Justifications for Reviewers

| Permission | Purpose & Technical Justification |
| :--- | :--- |
| `nativeMessaging` | **Core Requirement**: Communicates directly with the desktop `EDM.exe` process via stdio native messaging to dispatch captured download URLs, headers, and cookies to the high-speed multi-threaded desktop engine. |
| `downloads` | Intercepts standard browser download attempts when the user clicks a download link or when EDM capture is enabled, routing the download payload to EDM.exe instead of the browser's basic downloader. |
| `cookies` | Captures active session cookies for authenticated downloads (e.g. member portals or private hosts) so downloads don't fail due to HTTP 401/403 login barriers. |
| `webRequest` | Inspects HTTP response headers (such as `Content-Type: video/mp4` or `Content-Disposition`) to detect downloadable media streams and present the EDM overlay button. |
| `scripting` | Dynamically injects the EDM Download Panel overlay UI into web pages containing video elements or downloadable links upon user interaction. |
| `activeTab` | Grants temporary access to inspect the active tab when the user clicks the EDM toolbar icon to scan for downloadable video/audio resources on the current page. |
| `storage` | Persists user preferences locally (e.g. automatic interception toggle, dark/light theme choice, file extension exclusion rules). |
| `<all_urls>` | **Host Permission Justification**: Required because users download media, video streams, and files from arbitrary web domains across the internet. Restricting host permissions to specific domains would break media capture on unlisted websites. |

---

## 2. Store Metadata & Descriptions

### Short Description (Max 132 Characters)
> Intercept browser downloads and capture online videos to download at maximum speed with Exclusive Download Manager (EDM).

### Detailed Store Description
```text
Exclusive Download Manager (EDM) Browser Extension seamlessly connects your web browser to the EDM Desktop App for lightning-fast, multi-threaded downloads.

KEY FEATURES:
• One-Click Video & Audio Capture: Detects embedded HLS, MP4, MP3, and media streams on any site.
• Automatic Download Interception: Replaces slow browser downloads with EDM's multi-part engine.
• Session Cookie Import: Supports login-gated and authenticated downloads automatically.
• Context Menu Integration: Right-click any link or media file to "Download with EDM".
• Custom Filter Rules: Exclude specific domains or file types from automatic capture.

REQUIREMENTS:
Requires the Exclusive Download Manager (EDM) Windows Desktop Application installed on your system.
```

---

## 3. Required Store Screenshots Specifications

To pass store review, prepare at least 2 screenshots per platform following these exact image dimensions:

| Store Platform | Screenshot Size (Pixels) | Format | Requirements |
| :--- | :--- | :--- | :--- |
| **Chrome Web Store** | `1280 x 800` or `640 x 400` | PNG / JPEG | Minimum 1 screenshot, 1280x800 recommended. No device frames unless clean. |
| **Microsoft Edge Add-ons** | `1280 x 800` | PNG / JPEG | 1280x800 resolution required. Max file size 2MB per image. |
| **Mozilla Add-ons (AMO)** | `1280 x 800` or `1600 x 1000` | PNG / JPEG | Minimum 1 screenshot, aspect ratio ~16:10 or 16:9. |

### Recommended Screenshot Composition
1. **Screenshot 1**: Web page displaying the EDM Download Panel overlay capturing a 1080p video stream.
2. **Screenshot 2**: EDM Desktop App downloading the captured video across 16 parallel segments with real-time speed metrics.
