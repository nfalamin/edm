/**
 * EDM Advanced In-Page Video Overlay & Interception Content Script (Firefox AMO)
 */

(function () {
    'use strict';

    let knownVideos = new WeakSet();

    // 1. Key modifier listener for link clicks
    document.addEventListener('click', function (e) {
        let link = e.target.closest('a');
        if (!link || !link.href) return;

        if (e.altKey) {
            browser.runtime.sendMessage({ action: 'set_bypass_next_download', url: link.href });
            return;
        }

        if (e.ctrlKey && (link.href.startsWith('http://') || link.href.startsWith('https://'))) {
            e.preventDefault();
            browser.runtime.sendMessage({
                action: 'download_url',
                url: link.href,
                pageUrl: window.location.href,
                fileName: link.download || ''
            });
        }
    }, true);

    // 2. Scan & Inject Video Overlay Panels
    function scanForVideoElements() {
        const videos = Array.from(document.querySelectorAll('video'));

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

        const parent = video.parentElement || document.body;
        if (getComputedStyle(parent).position === 'static') {
            parent.style.position = 'relative';
        }
        parent.appendChild(overlay);

        overlay.querySelectorAll('.edm-menu-item').forEach(item => {
            item.addEventListener('click', (e) => {
                e.stopPropagation();
                const res = item.getAttribute('data-res');
                const format = item.getAttribute('data-format');

                browser.runtime.sendMessage({
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

    scanForVideoElements();
    window.addEventListener('load', scanForVideoElements);
    window.addEventListener('popstate', () => setTimeout(scanForVideoElements, 1000));
})();
