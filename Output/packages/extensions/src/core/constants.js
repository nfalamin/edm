/**
 * EDM (Exclusive Download Manager) - Core Constants & Enums
 * Version: 1.0.0
 * Architecture: Clean 1.0.0 Modular Foundation
 */

export const PROTOCOL_VERSION = "v1";
export const EXTENSION_VERSION = "1.0.0";
export const NATIVE_HOST_NAME = "com.edm.downloader";
export const LOCAL_HTTP_ENDPOINT = "http://127.0.0.1:48912/handoff";
export const LOCAL_VARIANTS_ENDPOINT = "http://127.0.0.1:48912/variants";
export const HANDOFF_TIMEOUT_MS = 6000;
export const RESOLVER_TIMEOUT_MS = 6000;
export const MIN_MEDIA_SIZE_BYTES = 262144; // 256 KB

export const CandidateState = Object.freeze({
    DISCOVERED: 'DISCOVERED',
    ANALYZING: 'ANALYZING',
    READY: 'READY',
    SELECTOR_OPEN: 'SELECTOR_OPEN',
    HANDOFF_PENDING: 'HANDOFF_PENDING',
    HANDOFF_CONFIRMED: 'HANDOFF_CONFIRMED',
    DOWNLOADING: 'DOWNLOADING',
    COMPLETED: 'COMPLETED',
    FAILED: 'FAILED',
    DESTROYED: 'DESTROYED'
});

export const DownloadState = Object.freeze({
    DETECTED: 'DETECTED',
    VALIDATING: 'VALIDATING',
    HANDOFF_PENDING: 'HANDOFF_PENDING',
    QUEUED: 'QUEUED',
    STARTED: 'STARTED',
    PROGRESS: 'PROGRESS',
    PAUSED: 'PAUSED',
    RESUMED: 'RESUMED',
    COMPLETED: 'COMPLETED',
    FAILED: 'FAILED',
    CANCELLED: 'CANCELLED'
});

export const ActionNames = Object.freeze({
    PING: "PING",
    PONG: "pong",
    GET_MEDIA_VARIANTS: "GET_MEDIA_VARIANTS",
    MEDIA_VARIANTS_RESOLVED: "media_variants_resolved",
    GET_TAB_CAPTURED_MEDIA: "GET_TAB_CAPTURED_MEDIA",
    START_EDM_DOWNLOAD: "START_EDM_DOWNLOAD",
    DOWNLOAD_REQUEST: "DOWNLOAD_REQUEST",
    DOWNLOAD_STATUS: "DOWNLOAD_STATUS"
});
