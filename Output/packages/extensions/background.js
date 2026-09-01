/**
 * EDM (Exclusive Download Manager) - Production Canonical Background Worker
 * MV3 Service Worker Architecture for Chrome, Edge, and Firefox
 * 
 * Responsibilities:
 * 1. Network Traffic Sniffing (M3U8, MPD, direct media, Content-Disposition).
 * 2. Native Messaging Bridge (Stdio 32-bit LE binary framing).
 * 3. Variant Resolution Proxy (GET_MEDIA_VARIANTS).
 * 4. Transactional Download Dispatcher (EDM Desktop Native Host -> Local HTTP API -> Restricted Emergency Fallback).
 */

const NATIVE_HOST_NAME = "com.edm.downloader";
const LOCAL_HTTP_ENDPOINT = "http://127.0.0.1:48912/handoff";
const LOCAL_VARIANTS_ENDPOINT = "http://127.0.0.1:48912/variants";
const HANDOFF_TIMEOUT_MS = 6000;

// Per-tab media store
const tabMediaStreams = new Map();
const recentHandoffs = new Set();

// Comprehensive 60+ EDM-Grade Downloadable File Extensions Dictionary
const DOWNLOAD_EXTENSIONS = new Set([
    // Archives & Compressed (20)
    "zip", "rar", "7z", "tar", "gz", "tgz", "bz2", "tbz2", "xz", "txz", "iso", "img", "dmg", "pkg", "deb", "rpm", "cab", "ace", "arc", "arj",
    // Video Formats (16)
    "mp4", "m4v", "mkv", "webm", "avi", "mov", "wmv", "flv", "f4v", "ts", "m2ts", "mts", "3gp", "3g2", "ogv", "vob",
    // Audio Formats (14)
    "mp3", "m4a", "aac", "flac", "wav", "wma", "ogg", "oga", "opus", "aiff", "aif", "alac", "mid", "midi",
    // Documents & E-books (12)
    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf", "epub",
    // Executables & Installers (10)
    "exe", "msi", "apk", "appx", "msix", "bin", "run", "jar", "crx", "xpi",
    // Streaming Manifests (5)
    "m3u8", "mpd", "f4m", "ism", "m3u"
]);

// Extensive MIME Types Dictionary
const DOWNLOAD_MIME_PATTERNS = [
    "video/", "audio/", "application/zip", "application/x-zip", "application/x-rar",
    "application/x-7z-compressed", "application/x-tar", "application/gzip", "application/x-iso9660-image",
    "application/octet-stream", "application/x-msdownload", "application/vnd.android.package-archive",
    "application/pdf", "application/vnd.apple.mpegurl", "application/x-mpegurl", "application/dash+xml",
    "application/vnd.ms-sstr+xml", "application/x-apple-diskimage", "application/x-debian-package",
    "application/x-redhat-package-manager", "application/vnd.microsoft.portable-executable"
];

// =============================================================================
// 1. LIVE NETWORK STREAM SNIFFER
// =============================================================================
if (typeof chrome !== "undefined" && chrome.webRequest && chrome.webRequest.onHeadersReceived) {
    const filter = { urls: ["<all_urls>"] };

    try {
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
                            for (let i = 0; i < DOWNLOAD_MIME_PATTERNS.length; i++) {
                                if (val.includes(DOWNLOAD_MIME_PATTERNS[i])) {
                                    isMedia = true;
                                    break;
                                }
                            }
                        } else if (name === "content-length") {
                            contentLength = parseInt(header.value, 10) || 0;
                        } else if (name === "content-disposition" && val.includes("attachment")) {
                            isAttachment = true;
                        }
                    }
                }

                // URL Pattern matching fallback against 60+ extensions
                if (!isMedia) {
                    const cleanPath = url.split("?")[0].toLowerCase();
                    const dotIdx = cleanPath.lastIndexOf(".");
                    if (dotIdx !== -1) {
                        const ext = cleanPath.substring(dotIdx + 1);
                        if (DOWNLOAD_EXTENSIONS.has(ext)) {
                            isMedia = true;
                        }
                    }
                }

                if (isMedia || isAttachment) {
                    // Filter out tiny visual fragments (< 64KB) unless manifest
                    const isManifest = url.includes(".m3u8") || url.includes(".mpd") || mimeType.includes("mpegurl") || mimeType.includes("dash+xml");
                    if (!isManifest && contentLength > 0 && contentLength < 65536) {
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
    } catch (e) {}
}

// Clean tab cache on tab removal
if (typeof chrome !== "undefined" && chrome.tabs && chrome.tabs.onRemoved) {
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
                sendResponse({ success: false, errorCode: "FORMAT_EXTRACTION_FAILED", error: err.message, variants: [] });
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

    if (message.action === "START_EDM_DOWNLOAD" || message.action === "START_DOWNLOAD_REQUEST") {
        const payload = message.candidate || message;
        handoffDownloadToEdm(payload)
            .then(result => sendResponse(result))
            .catch(err => {
                // Emergency direct browser fallback ONLY if permissible
                executeEmergencyBrowserFallback(payload, err.message)
                    .then(dlResult => sendResponse(dlResult))
                    .catch(dlErr => sendResponse({ success: false, errorCode: "EDM_UNAVAILABLE", error: dlErr.message }));
            });
        return true; // Async response
    }

    if (message.action === "PING_EDM" || message.action === "TEST_NATIVE_PING") {
        sendNativePing()
            .then(res => sendResponse(res))
            .catch(err => sendResponse({ success: false, errorCode: "NATIVE_HOST_UNAVAILABLE", error: err.message }));
        return true;
    }

    if (message.action === "GET_NATIVE_STATUS") {
        sendNativePing()
            .then(res => sendResponse({ success: true, connected: !!(res && res.success), version: res?.version || "2.0.0", mode: "native" }))
            .catch(async () => {
                // Dual Fallback: Check local HTTP daemon at 127.0.0.1:48912
                try {
                    const resp = await fetch("http://127.0.0.1:48912/status", { method: "GET" });
                    if (resp.ok) {
                        const data = await resp.json().catch(() => ({}));
                        sendResponse({ success: true, connected: true, version: data.version || "2.0.0", mode: "http" });
                        return;
                    }
                } catch (e) {}
                sendResponse({ success: false, connected: false });
            });
        return true;
    }

    return false;
});

// =============================================================================
// 3. VARIANT RESOLVER PROXY (Stdio -> HTTP -> Tab Sniffer Fallback)
// =============================================================================
async function resolveMediaVariants(url, cookies, tabId) {
    if (!url) return { success: false, errorCode: "INVALID_MEDIA_URL", variants: [] };

    // 1. Try Native Messaging Host
    try {
        const nativeResponse = await sendNativeMessageWithTimeout({
            action: "GET_MEDIA_VARIANTS",
            url: url,
            cookies: cookies || ""
        }, 5000);

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
    } catch (nativeErr) {}

    // 2. Try Local HTTP Endpoint
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 2500);

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

    return { success: false, errorCode: "FORMAT_EXTRACTION_FAILED", variants: [] };
}

// =============================================================================
// 4. TRANSACTIONAL NATIVE & HTTP HANDOFF WITH RESTRICTED EMERGENCY FALLBACK
// =============================================================================
async function handoffDownloadToEdm(payload) {
    if (!payload || !payload.url) {
        return { success: false, error: "Empty or invalid download URL." };
    }

    const dlUrl = (payload.url || "").trim();
    if (!dlUrl.startsWith("http://") && !dlUrl.startsWith("https://") && !dlUrl.startsWith("ftp://") && !dlUrl.startsWith("ftps://")) {
        return { success: false, error: "Unsupported URL scheme." };
    }

    if (dlUrl.length > 8192) {
        return { success: false, error: "URL exceeds maximum allowed length." };
    }

    const correlationId = payload.correlationId || ("edm_corr_" + Date.now());
    if (recentHandoffs.has(correlationId)) {
        return { success: true, deduplicated: true };
    }

    recentHandoffs.add(correlationId);
    setTimeout(() => recentHandoffs.delete(correlationId), 3000);

    let safeFilename = (payload.filename || payload.fileName || "download").replace(/[\\\/:\*\?"<>\|]/g, "_").trim();
    if (!safeFilename) safeFilename = "download";

    const message = {
        action: "DOWNLOAD_REQUEST",
        url: dlUrl,
        videoUrl: payload.videoUrl || dlUrl,
        audioUrl: payload.audioUrl || "",
        manifestUrl: payload.manifestUrl || "",
        pageUrl: payload.pageUrl || "",
        title: payload.title || "Video Media",
        filename: safeFilename,
        fileName: safeFilename,
        quality: payload.quality || "",
        format: payload.format || "",
        formatId: payload.formatId || "",
        formatArg: payload.formatArg || "",
        width: payload.width || 0,
        height: payload.height || 0,
        fps: payload.fps || 0,
        videoCodec: payload.videoCodec || payload.codec || "",
        codec: payload.codec || "",
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
        referer: payload.referer || payload.pageUrl || "",
        userAgent: payload.userAgent || (typeof navigator !== "undefined" ? navigator.userAgent : ""),
        mime: payload.mime || payload.mimeType || "",
        authHeader: payload.authHeader || "",
        postData: payload.postData || "",
        tabId: payload.tabId || -1,
        frameId: payload.frameId || 0,
        source: "BrowserExtension_MV3",
        timestamp: new Date().toISOString()
    };

    // Attempt 1: Native Messaging Host
    try {
        const nativeResponse = await sendNativeMessageWithTimeout(message, HANDOFF_TIMEOUT_MS);
        if (nativeResponse && (nativeResponse.success || nativeResponse.status === "QUEUED" || nativeResponse.status === "STARTED" || nativeResponse.status === "accepted" || nativeResponse.status === "handed_off")) {
            return { success: true, via: "native", response: nativeResponse };
        }
    } catch (nativeErr) {
        console.info("[EDM] Native host unavailable, trying local HTTP fallback...");
    }

    // Attempt 2: Local HTTP Endpoint
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), HANDOFF_TIMEOUT_MS);

        const response = await fetch(LOCAL_HTTP_ENDPOINT, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(message),
            signal: controller.signal
        });

        clearTimeout(timeoutId);

        if (response.ok) {
            const data = await response.json();
            return { success: true, via: "http", response: data };
        }
    } catch (httpErr) {
        console.info("[EDM] Local HTTP unavailable.");
    }

    // If both Native Host and HTTP failed, attempt emergency fallback if allowed
    return await executeEmergencyBrowserFallback(payload, "EDM Desktop is not running.");
}

// Emergency Fallback: Strictly restricted to direct standalone media files
async function executeEmergencyBrowserFallback(payload, reason) {
    const downloadUrl = payload.videoUrl || payload.url;

    // Rule: Never fallback for manifests (.m3u8, .mpd) or adaptive streams requiring FFmpeg merge
    if (payload.requiresFfmpegMerge || payload.manifestUrl || (downloadUrl && (downloadUrl.includes(".m3u8") || downloadUrl.includes(".mpd")))) {
        return {
            success: false,
            errorCode: "EDM_UNAVAILABLE",
            error: "This media stream requires EDM Desktop (adaptive merge / HLS stream). Please start EDM to download."
        };
    }

    // Rule: Never fallback to HTML watch page URLs
    if (downloadUrl && (downloadUrl.includes("youtube.com/watch") || downloadUrl.includes("youtu.be/"))) {
        return {
            success: false,
            errorCode: "INVALID_MEDIA_URL",
            error: "Cannot download web page HTML as media. Please start EDM Desktop to resolve stream formats."
        };
    }

    if (chrome && chrome.downloads && chrome.downloads.download) {
        const filename = payload.filename || payload.fileName || "download.mp4";

        return new Promise((resolve, reject) => {
            chrome.downloads.download({
                url: downloadUrl,
                filename: filename,
                saveAs: false,
                conflictAction: "uniquify"
            }, (downloadId) => {
                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    console.warn(`[EDM] Emergency browser fallback invoked for ${filename}. Reason: ${reason}`);
                    resolve({ success: true, via: "browser_emergency_fallback", downloadId: downloadId, fallbackReason: reason });
                }
            });
        });
    }

    return {
        success: false,
        errorCode: "EDM_UNAVAILABLE",
        error: "EDM is not running and browser download is unavailable."
    };
}

// Stdio Native Messaging Helper
function sendNativeMessageWithTimeout(message, timeoutMs) {
    return new Promise((resolve, reject) => {
        if (!chrome.runtime || !chrome.runtime.sendNativeMessage) {
            return reject(new Error("Native messaging not supported"));
        }

        let completed = false;
        const timer = setTimeout(() => {
            if (!completed) {
                completed = true;
                reject(new Error("Native messaging timed out"));
            }
        }, timeoutMs || 5000);

        try {
            chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, message, (response) => {
                if (completed) return;
                completed = true;
                clearTimeout(timer);

                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    resolve(response || { success: true });
                }
            });
        } catch (err) {
            if (!completed) {
                completed = true;
                clearTimeout(timer);
                reject(err);
            }
        }
    });
}

function sendNativePing() {
    return sendNativeMessageWithTimeout({ action: "PING" }, 2000);
}

// =============================================================================
// 5. SAFE BROWSER DOWNLOADS INTERCEPTION & BYPASS PROTECTION (IDM STYLE)
// =============================================================================
const bypassNextUrls = new Set();

function bypassNextUrl(url) {
    if (!url) return;
    bypassNextUrls.add(url);
    setTimeout(() => bypassNextUrls.delete(url), 15000);
}

function isInterceptableFile(filename, url, mime) {
    if (url && (url.startsWith("blob:") || url.startsWith("data:"))) return false;

    // Check filename extension
    const nameToCheck = (filename || url || "").split("?")[0].split("#")[0].toLowerCase();
    const dotIdx = nameToCheck.lastIndexOf(".");
    if (dotIdx !== -1) {
        const ext = nameToCheck.substring(dotIdx + 1);
        if (DOWNLOAD_EXTENSIONS.has(ext)) return true;
    }

    // Check MIME type against downloadable dictionary
    if (mime) {
        const lowerMime = mime.toLowerCase();
        for (const pattern of DOWNLOAD_MIME_PATTERNS) {
            if (lowerMime.includes(pattern)) return true;
        }
    }

    return false;
}

if (typeof chrome !== "undefined" && chrome.downloads) {
    // Primary Interception Hook: fires when filename is determined, before file write begins
    if (chrome.downloads.onDeterminingFilename) {
        chrome.downloads.onDeterminingFilename.addListener((downloadItem, suggest) => {
            if (!downloadItem || !downloadItem.url) {
                suggest();
                return;
            }

            const rawUrl = downloadItem.finalUrl || downloadItem.url;
            if (bypassNextUrls.has(rawUrl) || bypassNextUrls.has(downloadItem.url)) {
                bypassNextUrls.delete(rawUrl);
                bypassNextUrls.delete(downloadItem.url);
                suggest();
                return;
            }

            const filename = downloadItem.filename || "";
            const mime = downloadItem.mime || "";

            if (!isInterceptableFile(filename, rawUrl, mime)) {
                // Not an intercepted file format -> allow standard browser download
                suggest();
                return;
            }

            console.info("[EDM] Intercepting browser file download:", filename, rawUrl);

            // Cancel Chrome's download immediately
            chrome.downloads.cancel(downloadItem.id, () => {
                if (chrome.downloads.erase) {
                    chrome.downloads.erase({ id: downloadItem.id });
                }
            });

            // Hand off to EDM Desktop asynchronously
            (async () => {
                let cookiesStr = "";
                try {
                    if (chrome.cookies && chrome.cookies.getAll) {
                        const cookieList = await chrome.cookies.getAll({ url: rawUrl });
                        if (cookieList && cookieList.length > 0) {
                            cookiesStr = cookieList.map(c => `${c.name}=${c.value}`).join("; ");
                        }
                    }
                } catch (e) {}

                const correlationId = "browser_dl_" + downloadItem.id + "_" + Date.now();

                await handoffDownloadToEdm({
                    url: rawUrl,
                    videoUrl: rawUrl,
                    filename: filename,
                    referer: downloadItem.referrer || rawUrl,
                    pageUrl: downloadItem.referrer || "",
                    userAgent: typeof navigator !== "undefined" ? navigator.userAgent : "",
                    mime: mime,
                    fileSize: downloadItem.fileSize || -1,
                    cookies: cookiesStr,
                    correlationId: correlationId,
                    source: "BrowserDownloadInterception"
                });
            })();
        });
    } else if (chrome.downloads.onCreated) {
        // Fallback for browsers supporting onCreated
        chrome.downloads.onCreated.addListener(async (downloadItem) => {
            if (!downloadItem || !downloadItem.url) return;
            if (downloadItem.url.startsWith("blob:") || downloadItem.url.startsWith("data:")) return;

            const url = downloadItem.url;
            if (bypassNextUrls.has(url)) {
                bypassNextUrls.delete(url);
                return;
            }

            if (!isInterceptableFile(downloadItem.filename, url, downloadItem.mime)) return;

            try {
                chrome.downloads.cancel(downloadItem.id);
                if (chrome.downloads.erase) chrome.downloads.erase({ id: downloadItem.id });
            } catch (err) {}

            let cookiesStr = "";
            try {
                if (chrome.cookies && chrome.cookies.getAll) {
                    const cookieList = await chrome.cookies.getAll({ url: url });
                    if (cookieList && cookieList.length > 0) {
                        cookiesStr = cookieList.map(c => `${c.name}=${c.value}`).join("; ");
                    }
                }
            } catch (e) {}

            await handoffDownloadToEdm({
                url: url,
                videoUrl: url,
                filename: downloadItem.filename || "",
                referer: downloadItem.referrer || downloadItem.finalUrl || "",
                pageUrl: downloadItem.referrer || "",
                userAgent: typeof navigator !== "undefined" ? navigator.userAgent : "",
                mime: downloadItem.mime || "",
                fileSize: downloadItem.fileSize || -1,
                cookies: cookiesStr,
                correlationId: "browser_dl_" + downloadItem.id,
                source: "BrowserDownloadInterception"
            });
        });
    }
}

// =============================================================================
// 6. ON INSTALL / RELOAD: AUTOMATIC TAB REFRESH (IDM-GRADE BEHAVIOR)
// =============================================================================
if (typeof chrome !== "undefined" && chrome.runtime && chrome.runtime.onInstalled) {
    chrome.runtime.onInstalled.addListener(async (details) => {
        console.log("[EDM] Extension installed/updated, reason:", details.reason);
        
        // Auto-refresh existing tabs so content scripts and floating button immediately activate
        try {
            if (chrome.tabs && chrome.tabs.query) {
                const tabs = await chrome.tabs.query({ url: ["http://*/*", "https://*/*"] });
                for (const tab of tabs) {
                    if (tab.id && !tab.url.startsWith("chrome://") && !tab.url.startsWith("edge://")) {
                        try {
                            chrome.tabs.reload(tab.id);
                        } catch (e) {}
                    }
                }
            }
        } catch (e) {
            console.warn("[EDM] Tab reload on install error:", e);
        }

        // Setup Context Menus
        setupContextMenus();
    });
}

function setupContextMenus() {
    if (typeof chrome === "undefined" || !chrome.contextMenus) return;
    try {
        chrome.contextMenus.removeAll(() => {
            chrome.contextMenus.create({
                id: "edm-download-link",
                title: "Download with EDM",
                contexts: ["link"]
            });
            chrome.contextMenus.create({
                id: "edm-download-media",
                title: "Download Media with EDM",
                contexts: ["video", "audio", "image"]
            });
        });
    } catch (e) {}
}

if (typeof chrome !== "undefined" && chrome.contextMenus && chrome.contextMenus.onClicked) {
    chrome.contextMenus.onClicked.addListener(async (info, tab) => {
        const targetUrl = info.srcUrl || info.linkUrl || "";
        if (!targetUrl) return;

        await handoffDownloadToEdm({
            url: targetUrl,
            videoUrl: targetUrl,
            referer: (tab && tab.url) ? tab.url : "",
            pageUrl: (tab && tab.url) ? tab.url : "",
            correlationId: "ctx_" + Date.now(),
            source: "ContextMenu"
        });
    });
}

