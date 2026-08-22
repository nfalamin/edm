# EDM Extension — Standardized Error Model

## 1. Error Codes Reference

| Error Code | Description | User / Runtime Action |
| :--- | :--- | :--- |
| `INVALID_MESSAGE` | Incoming message payload is null, malformed, or missing required fields. | Message rejected; error response returned. |
| `INVALID_PAYLOAD` | Message structure is valid but specific field types/formats failed schema check. | Request rejected with diagnostic code. |
| `UNAUTHORIZED_MESSAGE` | Action identifier is not present in the approved Message Allowlist. | Rejected immediately without function mapping. |
| `VERSION_MISMATCH` | Protocol version specified in message envelope is incompatible with current version (`v1`). | Returns structured version mismatch error. |
| `NATIVE_HOST_UNAVAILABLE` | `EDM.NativeHost.exe` could not be started or `chrome.runtime.sendNativeMessage` threw `lastError`. | Triggers fallback to Local HTTP / Emergency Fallback. |
| `NATIVE_HOST_DISCONNECTED` | Stdio pipe disconnected or closed unexpectedly during message processing. | State marked DISCONNECTED; backoff retry applied. |
| `REQUEST_TIMEOUT` | Native host or background service worker did not respond within configured timeout (6000ms). | Request settled with timeout error; late response ignored. |
| `REQUEST_CANCELLED` | Pending operation cancelled due to tab closure, navigation, or user action. | Timers aborted; late responses dropped cleanly. |
| `UNKNOWN_REQUEST` | No handler registered for the requested action. | Returns unknown request response. |
| `INTERNAL_ERROR` | Unhandled exception occurred within extension background or content script. | Exception caught and serialized safely. |
| `INVALID_MEDIA_URL` | Media URL failed validation (e.g. invalid scheme or plain HTML watch page). | Handoff blocked; user notified via UI toast. |
| `EDM_UNAVAILABLE` | EDM desktop application is not running and cannot be launched. | Prompts user to start EDM desktop app. |

## 2. Standardized Error Response Format

```json
{
  "success": false,
  "errorCode": "NATIVE_HOST_UNAVAILABLE",
  "error": "Specified native messaging host not found.",
  "details": null,
  "requestId": "edm_req_1723987200000_a1b2c3",
  "timestamp": "2026-08-18T15:55:00.010Z"
}
```
