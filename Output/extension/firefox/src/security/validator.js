/**
 * EDM Extension - Security & URL Validation Engine
 */

export class SecurityValidator {
    static isValidMediaUrl(url) {
        if (!url || typeof url !== 'string') return false;
        const trimmed = url.trim();
        if (!trimmed.startsWith('http://') && !trimmed.startsWith('https://')) return false;

        try {
            const u = new URL(trimmed);
            // Reject plain HTML watch page URLs pretending to be direct media streams
            if (u.hostname.includes('youtube.com') && (u.pathname === '/watch' || u.pathname.startsWith('/shorts'))) {
                return false;
            }
            if (u.hostname.includes('youtu.be')) return false;
            return true;
        } catch (e) {
            return false;
        }
    }

    static parseAndValidateCipherUrl(cipherStr) {
        if (!cipherStr || typeof cipherStr !== 'string') return '';
        try {
            const params = new URLSearchParams(cipherStr);
            const baseUrl = params.get('url');
            if (!baseUrl || !SecurityValidator.isValidMediaUrl(baseUrl)) return '';

            const s = params.get('s');
            const sp = params.get('sp') || 'sig';

            if (s) {
                const separator = baseUrl.includes('?') ? '&' : '?';
                return `${baseUrl}${separator}${sp}=${encodeURIComponent(s)}`;
            }
            return baseUrl;
        } catch (e) {
            return '';
        }
    }

    static sanitizeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    static validatePostMessageOrigin(event) {
        if (!event) return false;
        // Verify source window matches execution context
        return event.source === window;
    }
}
