/**
 * EDM Extension - Download Policy Engine
 * Version: 1.0.0
 * Evaluates whether an incoming resource or user action should be handled by EDM or fall back to browser.
 */

import { SecurityValidator } from '../security/validator.js';

export const PolicyVerdict = Object.freeze({
    HANDLE: 'HANDLE',
    BROWSER_FALLBACK: 'BROWSER_FALLBACK',
    REJECT: 'REJECT',
    USER_CONFIRMATION_REQUIRED: 'USER_CONFIRMATION_REQUIRED'
});

export class DownloadPolicy {
    static INTERCEPTABLE_EXTENSIONS = new Set([
        'exe', 'msi', 'zip', 'rar', '7z', 'tar', 'gz', 'iso', 'dmg', 'pkg',
        'bin', 'apk', 'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
        'mp4', 'mkv', 'webm', 'avi', 'mov', 'flv', 'mp3', 'flac', 'wav', 'm4a', 'aac'
    ]);

    /**
     * Evaluates a download candidate against security, extension type, and user bypass flags.
     * @param {Object} candidate 
     * @param {Object} contextOptions { isAltKeyPressed, isEdmAvailable, autoCaptureEnabled }
     * @returns {string} PolicyVerdict
     */
    static evaluate(candidate, contextOptions = {}) {
        if (!candidate || !candidate.url) return PolicyVerdict.REJECT;

        const url = candidate.url;

        // 1. Security check: Must be HTTP or HTTPS
        if (!SecurityValidator.isValidMediaUrl(url)) {
            return PolicyVerdict.REJECT;
        }

        // 2. Alt Key Bypass Check
        if (contextOptions.isAltKeyPressed) {
            return PolicyVerdict.BROWSER_FALLBACK;
        }

        // 3. EDM Availability Check
        if (contextOptions.isEdmAvailable === false) {
            return PolicyVerdict.BROWSER_FALLBACK;
        }

        // 4. Auto-capture setting disabled
        if (contextOptions.autoCaptureEnabled === false && candidate.sourceType === 'BROWSER_INTERCEPTION') {
            return PolicyVerdict.BROWSER_FALLBACK;
        }

        // 5. Media streams (explicit user intent) -> always HANDLE
        if (candidate.sourceType === 'MEDIA_STREAM' || candidate.requiresMerge) {
            return PolicyVerdict.HANDLE;
        }

        // 6. Normal browser file download: check extension or Content-Type
        const cleanUrl = url.split('?')[0].toLowerCase();
        const extMatch = cleanUrl.match(/\.([a-z0-9]{2,5})$/i);
        const ext = extMatch ? extMatch[1] : '';

        if (DownloadPolicy.INTERCEPTABLE_EXTENSIONS.has(ext)) {
            return PolicyVerdict.HANDLE;
        }

        if (candidate.mimeType && (
            candidate.mimeType.includes('application/octet-stream') ||
            candidate.mimeType.includes('application/zip') ||
            candidate.mimeType.includes('video/') ||
            candidate.mimeType.includes('audio/')
        )) {
            return PolicyVerdict.HANDLE;
        }

        // Default to browser fallback for standard HTML/text navigation
        return PolicyVerdict.BROWSER_FALLBACK;
    }
}
