/**
 * EDM Advanced In-Page Video Sniffer & Floating Download Control Panel (v2.0)
 * 
 * Workflow:
 * 1. Hover video player -> Shows sleek, compact "▶ Download this video" button.
 * 2. Click button -> Opens translucent glassmorphic Format/Resolution Selection Popup.
 * 3. Analyzes real video formats (1080p HD, 720p HD, 480p, 360p, 240p, 144p, Audio).
 * 4. User clicks resolution -> Direct handoff to EDM Desktop engine.
 * 5. EDM opens Download Progress Window with real-time speed, segments, and ETA.
 */

(function () {
    'use strict';

    if (window.__EDM_SNIFFER_INITIALIZED__) return;
    window.__EDM_SNIFFER_INITIALIZED__ = true;

    const detectedMedia = new WeakMap();
    let globalActiveDropdown = null;

    // 1. Key Modifier Listener for Link Clicks
    document.addEventListener('click', function (e) {
        const link = e.target.closest('a');
        if (!link || !link.href) return;

        // If user holds ALT key -> Bypass EDM (let browser handle natively)
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

    // 2. Scan & Monitor Video / Audio Elements
    function scanMediaElements() {
        const videos = Array.from(document.querySelectorAll('video'));
        const audios = Array.from(document.querySelectorAll('audio'));

        videos.forEach(video => processMediaElement(video, 'video'));
        audios.forEach(audio => processMediaElement(audio, 'audio'));
    }

    function processMediaElement(mediaEl, type) {
        if (!mediaEl) return;

        // Skip micro-players or invisible tracking elements (less than 80px wide/60px high)
        const rect = mediaEl.getBoundingClientRect();
        if (type === 'video' && (rect.width > 0 && rect.width < 80) && (rect.height > 0 && rect.height < 60)) {
            return;
        }

        if (detectedMedia.has(mediaEl) || mediaEl.dataset.edmOverlayAttached === 'true') {
            // Update positioning if player moved or resized
            const existingPanel = detectedMedia.get(mediaEl);
            if (existingPanel) updatePanelPosition(mediaEl, existingPanel);
            return;
        }

        attachVideoOverlay(mediaEl, type);
    }

    // 3. Inject Floating Download Control Overlay
    function attachVideoOverlay(mediaEl, type) {
        mediaEl.dataset.edmOverlayAttached = 'true';
        const isDrm = mediaEl.mediaKeys != null;

        const panel = document.createElement('div');
        panel.className = 'edm-floating-panel';

        if (isDrm) {
            panel.innerHTML = `
                <div class="edm-drm-badge" title="Content is DRM-protected (Widevine / PlayReady)">
                    <span class="edm-drm-icon">🔒</span>
                    <span>DRM Protected</span>
                </div>
            `;
        } else {
            panel.innerHTML = `
                <button class="edm-floating-btn edm-video-overlay-btn" type="button" title="Download this video with EDM">
                    <span class="edm-btn-icon">▶</span>
                    <span class="edm-btn-text">Download this video</span>
                    <span class="edm-btn-arrow">▾</span>
                </button>
                <div class="edm-dropdown-card" style="display: none;">
                    <div class="edm-dropdown-header">
                        <div class="edm-dropdown-header-left">
                            <span class="edm-header-icon">⚡</span>
                            <span class="edm-dropdown-title">Download video with EDM</span>
                        </div>
                        <button class="edm-dropdown-close" type="button" title="Close">✕</button>
                    </div>
                    <div class="edm-variants-container">
                        <div class="edm-loading-box">
                            <div class="edm-loading-spinner"></div>
                            <span>Analyzing available video qualities...</span>
                        </div>
                    </div>
                </div>
            `;

            const btn = panel.querySelector('.edm-floating-btn');
            const dropdown = panel.querySelector('.edm-dropdown-card');
            const variantsContainer = panel.querySelector('.edm-variants-container');
            const closeBtn = panel.querySelector('.edm-dropdown-close');

            let variantsLoaded = false;

            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                e.preventDefault();

                const isOpened = dropdown.style.display !== 'none';
                if (isOpened) {
                    closeDropdown(panel, dropdown);
                } else {
                    openDropdown(panel, dropdown);

                    if (!variantsLoaded) {
                        fetchRealVariants(mediaEl, variantsContainer, () => {
                            variantsLoaded = true;
                        });
                    }
                }
            });

            if (closeBtn) {
                closeBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    closeDropdown(panel, dropdown);
                });
            }
        }

        // Attach to DOM body (to avoid container overflow/clipping issues)
        document.body.appendChild(panel);
        detectedMedia.set(mediaEl, panel);

        // Position panel over top-right of media element
        updatePanelPosition(mediaEl, panel);

        // Auto-fade behavior on playback
        let fadeTimer = null;
        const resetFade = () => {
            panel.style.opacity = '0.96';
            clearTimeout(fadeTimer);
            if (!mediaEl.paused && !panel.classList.contains('edm-open')) {
                fadeTimer = setTimeout(() => {
                    if (!mediaEl.paused && !panel.matches(':hover') && !panel.classList.contains('edm-open')) {
                        panel.style.opacity = '0.25';
                    }
                }, 2800);
            }
        };

        mediaEl.addEventListener('play', resetFade);
        mediaEl.addEventListener('playing', resetFade);
        mediaEl.addEventListener('pause', resetFade);
        mediaEl.addEventListener('mousemove', resetFade);
        panel.addEventListener('mouseenter', () => { panel.style.opacity = '1'; });
        panel.addEventListener('mouseleave', resetFade);
    }

    function openDropdown(panel, dropdown) {
        if (globalActiveDropdown && globalActiveDropdown !== dropdown) {
            globalActiveDropdown.style.display = 'none';
            globalActiveDropdown.parentElement?.classList.remove('edm-open');
        }
        dropdown.style.display = 'flex';
        panel.classList.add('edm-open');
        globalActiveDropdown = dropdown;
    }

    function closeDropdown(panel, dropdown) {
        dropdown.style.display = 'none';
        panel.classList.remove('edm-open');
        if (globalActiveDropdown === dropdown) globalActiveDropdown = null;
    }

    document.addEventListener('click', (e) => {
        if (globalActiveDropdown && !e.target.closest('.edm-floating-panel')) {
            globalActiveDropdown.style.display = 'none';
            globalActiveDropdown.parentElement?.classList.remove('edm-open');
            globalActiveDropdown = null;
        }
    });

    // 4. Update Panel Positioning (Anchors cleanly to top-right of media player)
    function updatePanelPosition(mediaEl, panel) {
        if (!mediaEl || !panel) return;

        // If media element was removed from DOM, clean up floating panel
        if (!document.body.contains(mediaEl)) {
            panel.remove();
            detectedMedia.delete(mediaEl);
            mediaEl.dataset.edmOverlayAttached = 'false';
            return;
        }

        const rect = mediaEl.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0 || rect.bottom < 0 || rect.top > window.innerHeight) {
            panel.style.display = 'none';
            return;
        }

        panel.style.display = 'block';
        const scrollX = window.scrollX || window.pageXOffset || 0;
        const scrollY = window.scrollY || window.pageYOffset || 0;

        const top = rect.top + scrollY + 12;
        const right = (window.innerWidth - (rect.right + scrollX)) + 12;

        panel.style.top = `${Math.max(8, top)}px`;
        panel.style.right = `${Math.max(8, right)}px`;
    }

    // 5. Fetch Real Stream Variants from EDM Native Host
    function fetchRealVariants(mediaEl, container, onComplete) {
        let mediaUrl = mediaEl.currentSrc || mediaEl.src || '';

        // If direct src is empty, check <source> tags
        if (!mediaUrl) {
            const srcTag = mediaEl.querySelector('source[src]');
            if (srcTag) mediaUrl = srcTag.src;
        }

        const pageUrl = window.location.href;
        const isBlobOrStreaming = !mediaUrl || mediaUrl.startsWith('blob:') || isKnownStreamingSite(pageUrl);

        const targetUrl = isBlobOrStreaming ? pageUrl : mediaUrl;

        chrome.runtime.sendMessage({
            action: 'GET_MEDIA_VARIANTS',
            url: targetUrl,
            pageUrl: pageUrl
        }, (response) => {
            onComplete();
            container.innerHTML = '';

            if (chrome.runtime.lastError || !response || !response.success || !response.result) {
                // Fallback: direct download link if URL is accessible
                renderFallbackOption(container, mediaUrl, targetUrl);
                return;
            }

            const res = response.result;
            const variants = res.variants || [];

            if (variants.length === 0) {
                renderFallbackOption(container, mediaUrl, targetUrl);
                return;
            }

            const videoTitle = res.title || document.title || 'Video';

            // Render Numbered IDM-style Quality Items
            variants.forEach((v, index) => {
                const item = document.createElement('div');
                item.className = 'edm-variant-item' + (v.isAudioOnly ? ' edm-variant-audio' : '');

                const num = index + 1;
                const resLabel = v.qualityLabel || v.resolution || (v.isAudioOnly ? 'Audio Only' : 'Standard Stream');
                const fileType = v.isAudioOnly ? 'Audio file' : 'MP4 file';
                const qualityText = `quality ${resLabel}`;
                const sizeLabel = v.estimatedSizeBytes > 0 ? formatBytes(v.estimatedSizeBytes) : (v.isAudioOnly ? 'Audio' : 'Direct');

                item.innerHTML = `
                    <div class="edm-variant-left">
                        <span class="edm-variant-num">${num}.</span>
                        <div class="edm-variant-info">
                            <span class="edm-variant-title">${escapeHtml(truncateTitle(videoTitle, 36))}</span>
                            <span class="edm-variant-meta">${fileType}, ${qualityText}</span>
                        </div>
                    </div>
                    <span class="edm-variant-size">${escapeHtml(sizeLabel)}</span>
                `;

                item.addEventListener('click', (e) => {
                    e.stopPropagation();
                    triggerDownload(v.directUrl || targetUrl, pageUrl, videoTitle, v.qualityLabel, v.formatArg);
                });

                container.appendChild(item);
            });
        });
    }

    function renderFallbackOption(container, mediaUrl, pageUrl) {
        const item = document.createElement('div');
        item.className = 'edm-empty-box';

        const canDirectDownload = mediaUrl && !mediaUrl.startsWith('blob:');

        if (canDirectDownload) {
            item.innerHTML = `
                <span>Direct Media Stream Detected</span>
                <button class="edm-floating-btn edm-video-overlay-btn" style="margin-top: 6px;" type="button">⬇ Download Direct URL</button>
            `;
            item.querySelector('button').addEventListener('click', () => {
                triggerDownload(mediaUrl, pageUrl, document.title, 'Direct', '');
            });
        } else {
            item.innerHTML = `
                <span>Adaptive stream detected</span>
                <button class="edm-floating-btn edm-video-overlay-btn" style="margin-top: 6px;" type="button">⚡ Download with EDM</button>
            `;
            item.querySelector('button').addEventListener('click', () => {
                triggerDownload(pageUrl, pageUrl, document.title, 'Best', '');
            });
        }

        container.appendChild(item);
    }

    function triggerDownload(url, pageUrl, title, quality, format) {
        const cleanTitle = (title || document.title || 'video').replace(/[/\\?%*:|"<>]/g, '_');
        chrome.runtime.sendMessage({
            action: 'download_url',
            url: url,
            pageUrl: pageUrl,
            fileName: cleanTitle,
            quality: quality,
            format: format
        }, (res) => {
            if (globalActiveDropdown) {
                globalActiveDropdown.parentElement?.classList.remove('edm-open');
                globalActiveDropdown.style.display = 'none';
                globalActiveDropdown = null;
            }
        });
    }

    function isKnownStreamingSite(url) {
        const u = url.toLowerCase();
        return u.includes('youtube.com') || u.includes('youtu.be') ||
               u.includes('vimeo.com') || u.includes('twitch.tv') ||
               u.includes('dailymotion.com') || u.includes('twitter.com') ||
               u.includes('x.com') || u.includes('facebook.com');
    }

    function formatBytes(bytes) {
        if (!bytes || bytes <= 0) return 'Variable';
        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let i = 0;
        let b = bytes;
        while (b >= 1024 && i < units.length - 1) {
            b /= 1024;
            i++;
        }
        return `${b.toFixed(1)} ${units[i]}`;
    }

    function truncateTitle(str, maxLen) {
        if (!str) return 'Video';
        return str.length > maxLen ? str.substring(0, maxLen) + '...' : str;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    // 6. Polling, SPA Hooks & Debounced MutationObserver for Dynamic Video Ingestion
    scanMediaElements();

    let mutationDebounceTimer = null;
    const observer = new MutationObserver((mutations) => {
        let shouldScan = false;
        for (const mutation of mutations) {
            if (mutation.addedNodes.length > 0 || mutation.removedNodes.length > 0) {
                shouldScan = true;
                break;
            }
        }
        if (shouldScan) {
            clearTimeout(mutationDebounceTimer);
            mutationDebounceTimer = setTimeout(scanMediaElements, 150);
        }
    });

    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    } else {
        document.addEventListener('DOMContentLoaded', () => {
            observer.observe(document.body, { childList: true, subtree: true });
        });
    }

    // SPA Navigation Events (YouTube, Vimeo, Twitch, etc.)
    window.addEventListener('yt-navigate-finish', () => setTimeout(scanMediaElements, 250));
    window.addEventListener('popstate', () => setTimeout(scanMediaElements, 200));

    try {
        const originalPushState = history.pushState;
        history.pushState = function () {
            originalPushState.apply(this, arguments);
            setTimeout(scanMediaElements, 200);
        };
        const originalReplaceState = history.replaceState;
        history.replaceState = function () {
            originalReplaceState.apply(this, arguments);
            setTimeout(scanMediaElements, 200);
        };
    } catch (e) {}

    window.addEventListener('resize', () => {
        const videos = Array.from(document.querySelectorAll('video, audio'));
        videos.forEach(el => {
            const panel = detectedMedia.get(el);
            if (panel) updatePanelPosition(el, panel);
        });
    });

    window.addEventListener('scroll', () => {
        const videos = Array.from(document.querySelectorAll('video, audio'));
        videos.forEach(el => {
            const panel = detectedMedia.get(el);
            if (panel) updatePanelPosition(el, panel);
        });
    }, { passive: true });

    setInterval(scanMediaElements, 1200);

})();
