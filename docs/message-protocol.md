# EDM Extension — Internal Message Protocol (Version: v1)

## 1. Core Principles

- Every internal message is routed exclusively through `MessageRouter`.
- Every message has an explicit `action`, unique `requestId`, `protocolVersion`, `timestamp`, and validated `payload`.
- Unapproved actions are rejected by the Allowlist before execution.
- Responses reference the originating `requestId`.
- Late responses (e.g. following a request timeout) are safely settled and ignored without raising unhandled errors.

## 2. Approved Message Types (Allowlist)

| Action String | Direction | Payload Required | Expected Response | Description |
| :--- | :--- | :--- | :--- | :--- |
| `PING` | Content / BG → NativeHost | None | `{ success: true, action: "pong", version: "1.0.0" }` | Health & connectivity check |
| `GET_MEDIA_VARIANTS` | Content → BG → NativeHost | `{ url: string, cookies?: string }` | `{ success: true, title: string, variants: Array<MediaVariant> }` | Query available stream bitrates/qualities |
| `START_EDM_DOWNLOAD` | Content → BG | `{ url: string, videoUrl?: string, filename?: string, quality?: string, ... }` | `{ success: true, via: string, response: object }` | Handoff download to EDM desktop |
| `GET_TAB_CAPTURED_MEDIA` | Content → BG | None | `{ success: true, streams: Array<StreamInfo> }` | Fetch webRequest sniffed streams for tab |
| `DOWNLOAD_REQUEST` | BG → NativeHost | Full `IpcHandoffPayload` | `{ success: true, status: "handed_off" }` | Primary handoff message to EDM host |

## 3. Standard Request Envelope

```json
{
  "action": "DOWNLOAD_REQUEST",
  "requestId": "edm_req_1723987200000_a1b2c3",
  "protocolVersion": "v1",
  "extensionVersion": "1.0.0",
  "timestamp": "2026-08-18T15:55:00.000Z",
  "url": "https://example.com/video.mp4",
  "filename": "video.mp4",
  "quality": "1080p",
  "format": "mp4",
  "correlationId": "edm_corr_1723987200000",
  "source": "BrowserExtension_v1.0.0"
}
```

## 4. Standard Response Envelope

```json
{
  "success": true,
  "action": "DOWNLOAD_REQUEST",
  "requestId": "edm_req_1723987200000_a1b2c3",
  "protocolVersion": "v1",
  "status": "handed_off",
  "timestamp": "2026-08-18T15:55:00.025Z"
}
```
