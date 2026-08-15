// EDM Firefox Browser Extension Background Script
// Version: 2.0.0
// Manages Native Messaging communication with EDM Desktop Application

const NATIVE_HOST = 'com.edm.downloader';
let bypassNextUrl = null;
const browserApi = typeof browser !== 'undefined' ? browser : chrome;

// 1. Intercept Standard Browser Downloads
browserApi.downloads?.onCreated?.addListener((downloadItem) => {
    if (bypassNextUrl && (downloadItem.url === bypassNextUrl || downloadItem.finalUrl === bypassNextUrl)) {
        bypassNextUrl = null;
        return;
    }

    getCookiesForUrl(downloadItem.url, (cookieHeader) => {
        browserApi.runtime.sendNativeMessage(NATIVE_HOST, {
            action: 'intercept',
            url: downloadItem.url,
            filename: downloadItem.filename,
            fileSize: downloadItem.fileSize,
            mime: downloadItem.mime,
            cookies: cookieHeader
        }).then((response) => {
            if (response && (response.status === 'handed_off' || response.success)) {
                browserApi.downloads.cancel(downloadItem.id);
            }
        }).catch((err) => {
            console.warn('[EDM Firefox Background] Native host communication error:', err);
        });
    });
});

// 2. Handle Messages from Content Scripts
browserApi.runtime.onMessage.addListener((request, sender) => {
    if (!request || !request.action) return false;

    if (request.action === 'set_bypass_next_download') {
        bypassNextUrl = request.url;
        return Promise.resolve({ success: true });
    }

    if (request.action === 'GET_MEDIA_VARIANTS' || request.action === 'resolve_media_variants') {
        const targetUrl = request.url || request.pageUrl;
        const pageUrl = request.pageUrl || (sender.tab ? sender.tab.url : targetUrl);

        return new Promise((resolve) => {
            getCookiesForUrl(pageUrl, (cookieHeader) => {
                browserApi.runtime.sendNativeMessage(NATIVE_HOST, {
                    action: 'GET_MEDIA_VARIANTS',
                    url: targetUrl,
                    pageUrl: pageUrl,
                    cookies: cookieHeader
                }).then((response) => {
                    if (response && response.result) {
                        resolve({ success: true, result: response.result });
                    } else if (response && response.success && response.data) {
                        resolve({ success: true, result: response.data });
                    } else {
                        resolve({ success: false, error: response?.error || 'No stream variants found.' });
                    }
                }).catch((err) => {
                    resolve({ success: false, error: err?.message || 'Native host unavailable.' });
                });
            });
        });
    }

    if (request.action === 'download_url' || request.action === 'START_DOWNLOAD') {
        const downloadUrl = request.url;
        const pageUrl = request.pageUrl || (sender.tab ? sender.tab.url : downloadUrl);

        return new Promise((resolve) => {
            getCookiesForUrl(pageUrl, (cookieHeader) => {
                browserApi.runtime.sendNativeMessage(NATIVE_HOST, {
                    action: 'download_url',
                    url: downloadUrl,
                    filename: request.fileName || request.filename || '',
                    cookies: cookieHeader,
                    quality: request.quality || '',
                    format: request.format || '',
                    pageUrl: pageUrl
                }).then((response) => {
                    resolve({ success: true, response: response });
                }).catch((err) => {
                    resolve({ success: false, error: err?.message });
                });
            });
        });
    }

    return false;
});

function getCookiesForUrl(url, callback) {
    if (!url || !browserApi.cookies || !browserApi.cookies.getAll) {
        callback('');
        return;
    }

    try {
        browserApi.cookies.getAll({ url: url }).then((cookies) => {
            if (!cookies || cookies.length === 0) {
                callback('');
                return;
            }
            const cookieString = cookies.map(c => `${c.name}=${c.value}`).join('; ');
            callback(cookieString);
        }).catch(() => callback(''));
    } catch (e) {
        callback('');
    }
}
