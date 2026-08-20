/**
 * EDM Extension - Normal File Download Interceptor
 * Intercepts standard browser downloads (.exe, .zip, .pdf, .iso, .msi, etc.) and hands them off to EDM Desktop.
 */

import { NativeProtocolV1 } from '../native/protocol-v1.js';
import { Logger } from '../core/logger.js';

export class DownloadInterceptor {
    constructor(handoffHandler) {
        this.handoffHandler = handoffHandler;
        this.bypassNextUrls = new Set();
        this.interceptableExtensions = new Set([
            'exe', 'msi', 'zip', 'rar', '7z', 'tar', 'gz', 'iso', 'dmg', 'pkg',
            'bin', 'apk', 'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
            'mp4', 'mkv', 'webm', 'avi', 'mov', 'flv', 'mp3', 'flac', 'wav', 'm4a', 'aac'
        ]);
        this.init();
    }

    init() {
        if (typeof chrome !== 'undefined' && chrome.downloads && chrome.downloads.onCreated) {
            chrome.downloads.onCreated.addListener((downloadItem) => this.handleDownloadCreated(downloadItem));
        }
    }

    bypassUrl(url) {
        if (!url) return;
        this.bypassNextUrls.add(url);
        setTimeout(() => this.bypassNextUrls.delete(url), 10000);
    }

    async handleDownloadCreated(downloadItem) {
        if (!downloadItem || !downloadItem.url) return;
        const url = downloadItem.url;

        // Skip blob, data, and loopback URLs
        if (url.startsWith('blob:') || url.startsWith('data:')) return;

        // Check if explicitly bypassed (e.g. browser fallback in progress)
        if (this.bypassNextUrls.has(url)) {
            this.bypassNextUrls.delete(url);
            return;
        }

        const cleanUrl = url.split('?')[0].toLowerCase();
        const extMatch = cleanUrl.match(/\.([a-z0-9]{2,5})$/i);
        const ext = extMatch ? extMatch[1] : '';

        // Only intercept if matching known downloadable extension or attachment
        const isMatchedExt = this.interceptableExtensions.has(ext);
        const isMimeMatch = downloadItem.mime && (
            downloadItem.mime.includes('application/octet-stream') ||
            downloadItem.mime.includes('application/zip') ||
            downloadItem.mime.includes('application/x-')
        );

        if (!isMatchedExt && !isMimeMatch && downloadItem.fileSize <= 0) {
            return; // Allow normal browser navigation
        }

        const correlationId = `browser_dl_${downloadItem.id}_${Date.now()}`;
        Logger.info(`[DownloadInterceptor] Intercepting browser download #${downloadItem.id}: ${url}`);

        const requestPayload = NativeProtocolV1.createDownloadRequest({
            url: url,
            videoUrl: url,
            filename: downloadItem.filename || `download.${ext || 'dat'}`,
            estimatedSizeBytes: downloadItem.fileSize > 0 ? downloadItem.fileSize : -1,
            correlationId: correlationId,
            source: "BrowserDownloadInterception_v1.0.0"
        });

        const result = await this.handoffHandler(requestPayload);

        // Transactional: ONLY cancel browser download if EDM explicitly acknowledged/accepted
        if (result && result.success) {
            try {
                chrome.downloads.cancel(downloadItem.id, () => {
                    if (chrome.downloads.erase) {
                        chrome.downloads.erase({ id: downloadItem.id });
                    }
                });
                Logger.info(`[DownloadInterceptor] Successfully handed off download #${downloadItem.id} to EDM.`);
            } catch (err) {
                Logger.warn(`[DownloadInterceptor] Error cancelling browser download:`, err);
            }
        } else {
            Logger.info(`[DownloadInterceptor] EDM handoff rejected or unavailable; allowing browser download to proceed.`);
        }
    }
}
