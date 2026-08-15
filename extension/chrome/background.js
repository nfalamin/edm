// EDM Chrome & Chromium Browser Extension Background Service Worker
// Version: 2.0.0
// Manages Native Messaging communication with EDM Desktop Application

const NATIVE_HOST = 'com.edm.downloader';
let bypassNextUrl = null;

// 1. Intercept Standard Browser Downloads
chrome.downloads?.onCreated?.addListener((downloadItem) => {
    if (bypassNextUrl && (downloadItem.url === bypassNextUrl || downloadItem.finalUrl === bypassNextUrl)) {
        bypassNextUrl = null;
        return; // User held Alt to bypass EDM
    }

    // Capture cookies for authenticated file transfer
    getCookiesForUrl(downloadItem.url, (cookieHeader) => {
        chrome.runtime.sendNativeMessage(NATIVE_HOST, {
            action: 'intercept',
            url: downloadItem.url,
            filename: downloadItem.filename,
            fileSize: downloadItem.fileSize,
            mime: downloadItem.mime,
            cookies: cookieHeader
        }, (response) => {
            if (chrome.runtime.lastError) {
                console.warn('[EDM Background] Native host not connected:', chrome.runtime.lastError.message);
                return;
            }
            if (response && (response.status === 'handed_off' || response.success)) {
                chrome.downloads.cancel(downloadItem.id);
            }
        });
    });
});

// 2. Handle Messages from Content Scripts (Media Sniffing & Floating Download Control)
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (!request || !request.action) return false;

    if (request.action === 'set_bypass_next_download') {
        bypassNextUrl = request.url;
        sendResponse({ success: true });
        return false;
    }

    if (request.action === 'GET_MEDIA_VARIANTS' || request.action === 'resolve_media_variants') {
        const targetUrl = request.url || request.pageUrl;
        const pageUrl = request.pageUrl || (sender.tab ? sender.tab.url : targetUrl);

        getCookiesForUrl(pageUrl, (cookieHeader) => {
            chrome.runtime.sendNativeMessage(NATIVE_HOST, {
                action: 'GET_MEDIA_VARIANTS',
                url: targetUrl,
                pageUrl: pageUrl,
                cookies: cookieHeader
            }, (response) => {
                if (chrome.runtime.lastError) {
                    console.warn('[EDM Background] GET_MEDIA_VARIANTS native error:', chrome.runtime.lastError.message);
                    sendResponse({
                        success: false,
                        error: 'EDM is not running or Native Host is not registered.'
                    });
                    return;
                }

                if (response && response.result) {
                    sendResponse({ success: true, result: response.result });
                } else if (response && response.success && response.data) {
                    sendResponse({ success: true, result: response.data });
                } else {
                    sendResponse({ success: false, error: response?.error || 'No stream variants found.' });
                }
            });
        });
        return true; // Asynchronous sendResponse
    }

    if (request.action === 'download_url' || request.action === 'START_DOWNLOAD') {
        const downloadUrl = request.url;
        const pageUrl = request.pageUrl || (sender.tab ? sender.tab.url : downloadUrl);

        getCookiesForUrl(pageUrl, (cookieHeader) => {
            chrome.runtime.sendNativeMessage(NATIVE_HOST, {
                action: 'download_url',
                url: downloadUrl,
                filename: request.fileName || request.filename || '',
                cookies: cookieHeader,
                quality: request.quality || '',
                format: request.format || '',
                pageUrl: pageUrl
            }, (response) => {
                if (chrome.runtime.lastError) {
                    console.warn('[EDM Background] START_DOWNLOAD native error:', chrome.runtime.lastError.message);
                    sendResponse({ success: false, error: chrome.runtime.lastError.message });
                    return;
                }
                sendResponse({ success: true, response: response });
            });
        });
        return true; // Asynchronous sendResponse
    }

    return false;
});

// Helper: Extract domain cookies as a standard Cookie header string
function getCookiesForUrl(url, callback) {
    if (!url || !chrome.cookies || !chrome.cookies.getAll) {
        callback('');
        return;
    }

    try {
        chrome.cookies.getAll({ url: url }, (cookies) => {
            if (chrome.runtime.lastError || !cookies || cookies.length === 0) {
                callback('');
                return;
            }
            const cookieString = cookies.map(c => `${c.name}=${c.value}`).join('; ');
            callback(cookieString);
        });
    } catch (e) {
        callback('');
    }
}
