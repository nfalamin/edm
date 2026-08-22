/**
 * EDM (Exclusive Download Manager) - Universal Generic Media Stream Interceptor
 * Runs in the page's MAIN execution world at document_start.
 * Intercepts XMLHttpRequest and window.fetch for HLS (.m3u8), MPEG-DASH (.mpd),
 * SmoothStreaming (.f4m, .ism), and raw media stream chunks across all websites.
 */

(function() {
    'use strict';

    if (window.__EDM_STREAM_INTERCEPTOR_INITIALIZED__) return;
    window.__EDM_STREAM_INTERCEPTOR_INITIALIZED__ = true;

    const STREAM_PATTERNS = [
        /\.m3u8($|\?)/i,
        /\.mpd($|\?)/i,
        /\.f4m($|\?)/i,
        /\.ism\/manifest/i,
        /\.mp4($|\?)/i,
        /\.webm($|\?)/i,
        /\.mkv($|\?)/i,
        /\.m4s($|\?)/i,
        /\.ts($|\?)/i,
        /\.mp3($|\?)/i,
        /\.m4a($|\?)/i,
        /\.aac($|\?)/i,
        /\.flac($|\?)/i,
        /\.ogg($|\?)/i,
        /\.opus($|\?)/i
    ];

    const MEDIA_MIMES = [
        'video/',
        'audio/',
        'application/vnd.apple.mpegurl',
        'application/x-mpegurl',
        'application/dash+xml',
        'application/vnd.ms-sstr+xml'
    ];

    const capturedUrls = new Set();

    function reportStream(url, mimeType, contentLength, method) {
        if (!url || typeof url !== 'string') return;
        if (url.startsWith('blob:') || url.startsWith('data:') || url.startsWith('javascript:')) return;

        let absoluteUrl = url;
        try {
            absoluteUrl = new URL(url, window.location.href).href;
        } catch (e) {
            return;
        }

        const urlWithoutQuery = absoluteUrl.split('?')[0].toLowerCase();
        if (capturedUrls.has(urlWithoutQuery)) return;
        capturedUrls.add(urlWithoutQuery);

        const isManifest = absoluteUrl.includes('.m3u8') || absoluteUrl.includes('.mpd') ||
                           absoluteUrl.includes('.f4m') || (mimeType && (mimeType.includes('mpegurl') || mimeType.includes('dash+xml')));

        window.postMessage({
            type: '__EDM_GENERIC_STREAM_DETECTED__',
            stream: {
                url: absoluteUrl,
                mimeType: mimeType || '',
                contentLength: contentLength || 0,
                isManifest: isManifest,
                method: method || 'GET',
                pageUrl: window.location.href,
                pageTitle: document.title || '',
                timestamp: Date.now()
            }
        }, '*');
    }

    function isStreamUrl(url) {
        if (!url || typeof url !== 'string') return false;
        for (let i = 0; i < STREAM_PATTERNS.length; i++) {
            if (STREAM_PATTERNS[i].test(url)) return true;
        }
        return false;
    }

    // 1. Hook window.fetch
    if (typeof window.fetch === 'function') {
        const originalFetch = window.fetch;
        window.fetch = async function(...args) {
            const input = args[0];
            const url = (typeof input === 'string') ? input : (input && input.url ? input.url : '');

            if (isStreamUrl(url)) {
                reportStream(url, '', 0, 'fetch');
            }

            try {
                const response = await originalFetch.apply(this, args);
                if (response && response.headers) {
                    const cType = response.headers.get('content-type') || '';
                    const cLen = parseInt(response.headers.get('content-length'), 10) || 0;

                    let matchMime = false;
                    for (let i = 0; i < MEDIA_MIMES.length; i++) {
                        if (cType.toLowerCase().includes(MEDIA_MIMES[i])) {
                            matchMime = true;
                            break;
                        }
                    }

                    if (matchMime || isStreamUrl(response.url || url)) {
                        reportStream(response.url || url, cType, cLen, 'fetch');
                    }
                }
                return response;
            } catch (err) {
                return originalFetch.apply(this, args);
            }
        };
    }

    // 2. Hook window.XMLHttpRequest
    if (typeof window.XMLHttpRequest === 'function') {
        const originalOpen = XMLHttpRequest.prototype.open;
        const originalSend = XMLHttpRequest.prototype.send;

        XMLHttpRequest.prototype.open = function(method, url, ...rest) {
            this.__edm_url = url;
            this.__edm_method = method;
            if (isStreamUrl(url)) {
                reportStream(url, '', 0, method);
            }
            return originalOpen.apply(this, [method, url, ...rest]);
        };

        XMLHttpRequest.prototype.send = function(...args) {
            this.addEventListener('load', () => {
                try {
                    const cType = this.getResponseHeader('Content-Type') || '';
                    const cLen = parseInt(this.getResponseHeader('Content-Length'), 10) || 0;

                    let matchMime = false;
                    for (let i = 0; i < MEDIA_MIMES.length; i++) {
                        if (cType.toLowerCase().includes(MEDIA_MIMES[i])) {
                            matchMime = true;
                            break;
                        }
                    }

                    if (matchMime || isStreamUrl(this.__edm_url || this.responseURL)) {
                        reportStream(this.responseURL || this.__edm_url, cType, cLen, this.__edm_method || 'GET');
                    }
                } catch (e) {}
            });
            return originalSend.apply(this, args);
        };
    }

    // 3. Hook HTMLMediaElement.prototype.src descriptor
    try {
        const mediaProto = window.HTMLMediaElement ? window.HTMLMediaElement.prototype : null;
        if (mediaProto) {
            const originalSrcDescriptor = Object.getOwnPropertyDescriptor(mediaProto, 'src');
            if (originalSrcDescriptor && originalSrcDescriptor.set) {
                Object.defineProperty(mediaProto, 'src', {
                    set: function(val) {
                        if (val && isStreamUrl(val)) {
                            reportStream(val, '', 0, 'media_element_src');
                        }
                        return originalSrcDescriptor.set.call(this, val);
                    },
                    get: originalSrcDescriptor.get,
                    configurable: true,
                    enumerable: true
                });
            }
        }
    } catch (e) {}
})();
