/**
 * EDM (Exclusive Download Manager) - Production Canonical Background Worker
 * MV3 Service Worker Architecture
 * 
 * Responsibilities:
 * 1. Network Traffic Sniffing (M3U8, MPD, direct media, Content-Disposition).
 * 2. Native Messaging Bridge (Stdio 32-bit LE binary framing).
 * 3. Variant Resolution Proxy (GET_MEDIA_VARIANTS with timeout).
 * 4. Transactional Download Interception & Contract Preservation (All 22 fields).
 */

const NATIVE_HOST_NAME = "com.edm.downloader";
const LOCAL_HTTP_ENDPOINT = "http://127.0.0.1:48912/handoff";
const LOCAL_VARIANTS_ENDPOINT = "http://127.0.0.1:48912/variants";
const HANDOFF_TIMEOUT_MS = 6000;

// Per-tab media store
const tabMediaStreams = new Map();
const recentHandoffs = new Set();

// =============================================================================
// 1. LIVE NETWORK STREAM SNIFFER
// =============================================================================
if (chrome.webRequest && chrome.webRequest.onHeadersReceived) {
    const filter = { urls: ["<all_urls>"] };

    chrome.webRequest.onHeadersReceived.addListener(
        (details) => {
            if (!details.url || details.tabId < 0) return;

            const url = details.url;
            let isMedia = false;
            let mimeType = "";
            let contentLength = 0;
            let isAttachment = false;

            if (details.responseHeaders) {
                for (const header of details.responseHeaders) {
                    const name = header.name.toLowerCase();
                    const val = (header.value || "").toLowerCase();

                    if (name === "content-type") {
                        mimeType = val;
                        if (val.includes("video/") ||
                            val.includes("audio/") ||
                            val.includes("application/vnd.apple.mpegurl") ||
                            val.includes("application/x-mpegurl") ||
                            val.includes("application/dash+xml") ||
                            val.includes("application/octet-stream")) {
                            isMedia = true;
                        }
                    } else if (name === "content-length") {
                        contentLength = parseInt(header.value, 10) || 0;
                    } else if (name === "content-disposition" && val.includes("attachment")) {
                        isAttachment = true;
                    }
                }
            }

            // URL Pattern matching fallback
            if (!isMedia) {
                const cleanUrl = url.split("?")[0].toLowerCase();
                if (cleanUrl.endsWith(".m3u8") ||
                    cleanUrl.endsWith(".mpd") ||
                    cleanUrl.endsWith(".mp4") ||
                    cleanUrl.endsWith(".webm") ||
                    cleanUrl.endsWith(".mkv") ||
                    cleanUrl.endsWith(".ts") ||
                    cleanUrl.endsWith(".mp3") ||
                    cleanUrl.endsWith(".m4a") ||
                    cleanUrl.endsWith(".aac") ||
                    cleanUrl.endsWith(".flac")) {
                    isMedia = true;
                }
            }

            if (isMedia || isAttachment) {
                // Filter out small visual fragments/chunks (< 256KB) unless manifest
                const isManifest = url.includes(".m3u8") || url.includes(".mpd") || mimeType.includes("mpegurl") || mimeType.includes("dash+xml");
                if (!isManifest && contentLength > 0 && contentLength < 262144) {
                    return;
                }

                if (!tabMediaStreams.has(details.tabId)) {
                    tabMediaStreams.set(details.tabId, new Map());
                }

                const streamKey = url.split("?")[0];
                tabMediaStreams.get(details.tabId).set(streamKey, {
                    url: url,
                    mimeType: mimeType,
                    contentLength: contentLength,
                    isManifest: isManifest,
                    timestamp: Date.now()
                });
            }
        },
        filter,
        ["responseHeaders"]
    );
}

// Clean tab cache on tab removal
if (chrome.tabs && chrome.tabs.onRemoved) {
    chrome.tabs.onRemoved.addListener((tabId) => {
        tabMediaStreams.delete(tabId);
    });
}

// =============================================================================
// 2. RUNTIME MESSAGE ROUTER
// =============================================================================
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message || !message.action) return false;

    const tabId = (sender && sender.tab) ? sender.tab.id : -1;

    if (message.action === "GET_MEDIA_VARIANTS") {
        resolveMediaVariants(message.url, message.cookies, tabId)
            .then(variants => sendResponse(variants))
            .catch(err => {
                console.warn("[EDM] Variant resolution error:", err);
                sendResponse({ success: false, variants: [] });
            });
        return true; // Async response
    }

    if (message.action === "GET_TAB_CAPTURED_MEDIA") {
        const streams = (tabId >= 0 && tabMediaStreams.has(tabId)) 
            ? Array.from(tabMediaStreams.get(tabId).values()) 
            : [];
        sendResponse({ success: true, streams: streams });
        return false;
    }

    if (message.action === "START_EDM_DOWNLOAD") {
        handoffDownloadToEdm(message)
            .then(result => sendResponse(result))
            .catch(err => sendResponse({ success: false, error: err.message }));
        return true; // Async response
    }

    if (message.action === "PING_EDM") {
        sendNativePing()
            .then(res => sendResponse(res))
            .catch(err => sendResponse({ success: false, error: err.message }));
        return true;
    }

    return false;
});

// =============================================================================
// 3. VARIANT RESOLVER PROXY (Stdio -> HTTP -> Tab Sniffer Fallback)
// =============================================================================
async function resolveMediaVariants(url, cookies, tabId) {
    if (!url) return { success: false, variants: [] };

    // 1. Try Native Messaging Host
    try {
        const nativeResponse = await sendNativeMessageWithTimeout({
            action: "GET_MEDIA_VARIANTS",
            url: url,
            cookies: cookies || ""
        }, 7000);

        if (nativeResponse && nativeResponse.success) {
            const variantsList = nativeResponse.variants || nativeResponse.data || (nativeResponse.result && nativeResponse.result.variants) || [];
            if (Array.isArray(variantsList) && variantsList.length > 0) {
                return {
                    success: true,
                    title: nativeResponse.title || (nativeResponse.result && nativeResponse.result.title) || "",
                    isDrmProtected: !!nativeResponse.isDrmProtected,
                    variants: variantsList
                };
            }
        }
    } catch (nativeErr) {
        // Fallback to local HTTP
    }

    // 2. Try Local HTTP Endpoint
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3500);

        const httpRes = await fetch(LOCAL_VARIANTS_ENDPOINT, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ url: url, cookies: cookies || "" }),
            signal: controller.signal
        });

        clearTimeout(timeoutId);

        if (httpRes.ok) {
            const data = await httpRes.json();
            const variantsList = (data && (data.variants || data.data || (data.result && data.result.variants))) || [];
            if (data && data.success && Array.isArray(variantsList) && variantsList.length > 0) {
                return {
                    success: true,
                    title: data.title || (data.result && data.result.title) || "",
                    isDrmProtected: !!data.isDrmProtected,
                    variants: variantsList
                };
            }
        }
    } catch (httpErr) {}

    // 3. Fallback: Return tab-sniffed media streams if available
    if (tabId >= 0 && tabMediaStreams.has(tabId)) {
        const sniffed = Array.from(tabMediaStreams.get(tabId).values());
        if (sniffed.length > 0) {
            const fallbackVariants = sniffed.map((s, idx) => {
                const isAudio = (s.mimeType && s.mimeType.includes("audio")) || s.url.includes(".mp3") || s.url.includes(".m4a");
                const ext = isAudio ? "m4a" : (s.url.includes(".webm") ? "webm" : "mp4");
                return {
                    variantId: "sniffed_" + idx,
                    qualityLabel: isAudio ? "Audio Stream" : (s.isManifest ? "HLS/DASH Stream" : "Captured Media Stream"),
                    container: ext,
                    codec: isAudio ? "AAC" : "H.264",
                    isAudioOnly: isAudio,
                    hasAudio: true,
                    estimatedSizeBytes: s.contentLength || -1,
                    directUrl: s.url,
                    manifestUrl: s.isManifest ? s.url : ""
                };
            });

            return {
                success: true,
                title: "Captured Stream",
                variants: fallbackVariants
            };
        }
    }

    return { success: false, variants: [] };
}

// =============================================================================
// 4. TRANSACTIONAL NATIVE & HTTP HANDOFF
// =============================================================================
async function handoffDownloadToEdm(payload) {
    const correlationId = payload.correlationId || ("edm_corr_" + Date.now());
    if (recentHandoffs.has(correlationId)) {
        return { success: true, deduplicated: true };
    }

    recentHandoffs.add(correlationId);
    setTimeout(() => recentHandoffs.delete(correlationId), 3000);

    const message = {
        action: "DOWNLOAD_REQUEST",
        url: payload.url,
        videoUrl: payload.videoUrl || payload.url,
        audioUrl: payload.audioUrl || "",
        manifestUrl: payload.manifestUrl || "",
        pageUrl: payload.pageUrl || "",
        title: payload.title || "Video Media",
        filename: payload.filename || payload.fileName || "download",
        fileName: payload.filename || payload.fileName || "download",
        quality: payload.quality || "",
        format: payload.format || "",
        formatId: payload.formatId || "",
        formatArg: payload.formatArg || "",
        width: payload.width || 0,
        height: payload.height || 0,
        fps: payload.fps || 0,
        videoCodec: payload.videoCodec || payload.codec || "",
        codec: payload.codec || payload.videoCodec || "",
        audioCodec: payload.audioCodec || "",
        container: payload.container || "",
        requiresFfmpegMerge: !!payload.requiresFfmpegMerge,
        downloadIdentity: payload.downloadIdentity || "",
        correlationId: correlationId,
        estimatedSizeBytes: payload.estimatedSizeBytes || -1,
        videoSizeBytes: payload.videoSizeBytes || -1,
        audioSizeBytes: payload.audioSizeBytes || -1,
        isAudioOnly: !!payload.isAudioOnly,
        cookies: payload.cookies || "",
        headers: payload.headers || {},
        source: payload.source || "BrowserExtension"
    };

    // 1. Primary: Stdio Native Messaging Host
    try {
        const res = await sendNativeMessageWithTimeout(message, HANDOFF_TIMEOUT_MS);
        if (res && res.success !== false) {
            return { success: true, channel: "NativeHost", response: res };
        }
    } catch (err) {}

    // 2. Secondary: Local REST Server
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 2500);

        const httpRes = await fetch(LOCAL_HTTP_ENDPOINT, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(message),
            signal: controller.signal
        });

        clearTimeout(timeoutId);

        if (httpRes.ok) {
            return { success: true, channel: "HttpHandoff" };
        }
    } catch (err) {}

    return { success: false, error: "EDM desktop application is not currently responding." };
}

function sendNativeMessageWithTimeout(message, timeoutMs) {
    return new Promise((resolve, reject) => {
        let done = false;
        const timer = setTimeout(() => {
            if (!done) {
                done = true;
                reject(new Error("Native messaging request timed out"));
            }
        }, timeoutMs);

        try {
            chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, message, (response) => {
                clearTimeout(timer);
                if (done) return;
                done = true;

                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    resolve(response);
                }
            });
        } catch (err) {
            clearTimeout(timer);
            if (!done) {
                done = true;
                reject(err);
            }
        }
    });
}

function sendNativePing() {
    return sendNativeMessageWithTimeout({ action: "ping" }, 2000);
}

// =============================================================================
// 5. SAFE BROWSER DOWNLOADS INTERCEPTION & BYPASS PROTECTION
// =============================================================================
const bypassNextUrls = new Set();

function bypassNextUrl(url) {
    if (!url) return;
    bypassNextUrls.add(url);
    setTimeout(() => bypassNextUrls.delete(url), 10000);
}

if (chrome.downloads && chrome.downloads.onCreated) {
    chrome.downloads.onCreated.addListener(async (downloadItem) => {
        if (!downloadItem || !downloadItem.url) return;
        if (downloadItem.url.startsWith("blob:") || downloadItem.url.startsWith("data:")) return;

        const url = downloadItem.url;
        if (bypassNextUrls.has(url)) {
            bypassNextUrls.delete(url);
            return;
        }

        const correlationId = "browser_dl_" + downloadItem.id;

        const handoffResult = await handoffDownloadToEdm({
            url: url,
            videoUrl: url,
            filename: downloadItem.filename || "",
            correlationId: correlationId,
            source: "BrowserDownloadInterception"
        });

        // Transactional: ONLY cancel browser download if EDM explicitly accepted
        if (handoffResult && handoffResult.success) {
            try {
                chrome.downloads.cancel(downloadItem.id);
                if (chrome.downloads.erase) {
                    chrome.downloads.erase({ id: downloadItem.id });
                }
            } catch (err) {}
        }
    });
}

