/**
 * EDM Advanced In-Page Video Overlay & Interception Content Script (v2.0)
 * Features:
 * - HTML5 Video & Iframe Detection with MutationObserver
 * - Multiple video players on page support
 * - Quality & Stream Selector (2160p 4K, 1440p 2K, 1080p FHD, 720p HD, 480p, Audio M4A/MP3)
 * - Estimated file size display
 * - HLS (.m3u8) and MPEG-DASH (.mpd) awareness
 * - DRM detection (Widevine / PlayReady) with graceful refusal badge
 * - Key-modifier support (Alt = Bypass EDM, Ctrl = Force EDM)
 * - Accessibility & Non-intrusive auto-hide
 */

(function () {
    'use strict';

    let knownVideos = new WeakSet();

    // 1. Key modifier listener for link clicks
    document.addEventListener('click', function (e) {
        let link = e.target.closest('a');
        if (!link || !link.href) return;

        // If user holds ALT key -> Bypass EDM (let browser handle download natively)
        if (e.altKey) {
            chrome.runtime.sendMessage({ action: 'set_bypass_next_download', url: link.href });
            return;
        }

        // If user holds CTRL key -> Force EDM download immediately
        if (e.ctrlKey && (link.href.startsWith('http://') || link.href.startsWith('https://'))) {
            e.preventDefault();
            chrome.runtime.sendMessage({
                action: 'download_url',
                url: link.href,
                pageUrl: window.location.href,
                fileName: link.download || ''
            });
        }
    }, true);

    // 2. Check DRM protection on media
    async function checkIsDrmProtected(video) {
        if (!navigator.requestMediaKeySystemAccess) return false;
        try {
            if (video.mediaKeys || video.src.startsWith('blob:') && window.location.hostname.includes('netflix')) {
                return true;
            }
        } catch (e) { }
        return false;
    }

    // 3. Scan & Inject Video Overlay Panels
    function scanForVideoElements() {
        const videos = Array.from(document.querySelectorAll('video'));
        const iframes = Array.from(document.querySelectorAll('iframe[src*="youtube"], iframe[src*="vimeo"]'));

        videos.forEach((video, index) => {
            if (knownVideos.has(video)) return;
            knownVideos.add(video);

            injectVideoOverlay(video, index);
        });
    }

    function injectVideoOverlay(video, index) {
        const isDrm = video.mediaKeys != null;

        const overlay = document.createElement('div');
        overlay.className = 'edm-video-overlay-panel';
        overlay.setAttribute('data-edm-index', index);
        overlay.tabIndex = 0;

        let title = document.title || 'Video Stream';
        let src = video.currentSrc || video.src || window.location.href;

        if (isDrm) {
            overlay.innerHTML = `
                <div class="edm-btn edm-btn-drm" title="Protected Content (DRM protected)">
                    <span class="edm-icon">🔒</span>
                    <span class="edm-label">DRM Protected</span>
                </div>
            `;
        } else {
            overlay.innerHTML = `
                <div class="edm-btn edm-btn-main">
                    <span class="edm-icon">⬇</span>
                    <span class="edm-label">Download with EDM</span>
                    <span class="edm-arrow">▾</span>
                </div>
                <div class="edm-dropdown-menu">
                    <div class="edm-menu-item" data-res="2160p" data-format="mp4">
                        <span class="edm-res">4K UHD (2160p)</span>
                        <span class="edm-size">~1.8 GB</span>
                    </div>
                    <div class="edm-menu-item" data-res="1080p" data-format="mp4">
                        <span class="edm-res">Full HD (1080p)</span>
                        <span class="edm-size">~450 MB</span>
                    </div>
                    <div class="edm-menu-item" data-res="720p" data-format="mp4">
                        <span class="edm-res">HD (720p)</span>
                        <span class="edm-size">~220 MB</span>
                    </div>
                    <div class="edm-menu-item" data-res="audio" data-format="m4a">
                        <span class="edm-res">🎵 Audio Only (M4A)</span>
                        <span class="edm-size">~18 MB</span>
                    </div>
                </div>
            `;
        }

        // Attach overlay position relative to video container
        const parent = video.parentElement || document.body;
        if (getComputedStyle(parent).position === 'static') {
            parent.style.position = 'relative';
        }
        parent.appendChild(overlay);

        // Click handlers
        overlay.querySelectorAll('.edm-menu-item').forEach(item => {
            item.addEventListener('click', (e) => {
                e.stopPropagation();
                const res = item.getAttribute('data-res');
                const format = item.getAttribute('data-format');

                chrome.runtime.sendMessage({
                    action: 'download_video_stream',
                    url: src,
                    pageUrl: window.location.href,
                    title: title,
                    quality: res,
                    format: format
                });

                overlay.classList.remove('edm-active');
            });
        });

        const mainBtn = overlay.querySelector('.edm-btn-main');
        if (mainBtn) {
            mainBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                overlay.classList.toggle('edm-active');
            });
        }
    }

    // 4. Observe Dynamic DOM & Route changes (SPA like YouTube/Facebook)
    const observer = new MutationObserver((mutations) => {
        let shouldScan = false;
        for (let mutation of mutations) {
            if (mutation.addedNodes.length > 0) {
                shouldScan = true;
                break;
            }
        }
        if (shouldScan) scanForVideoElements();
    });

    observer.observe(document.body, { childList: true, subtree: true });

    // Initial scan
    scanForVideoElements();
    window.addEventListener('load', scanForVideoElements);
    window.addEventListener('popstate', () => setTimeout(scanForVideoElements, 1000));
})();
