/**
 * EDM (Exclusive Download Manager) - Production Canonical Content Script
 * Glassmorphic Media Sniffer, Stream Extractor & Format Selector UI
 * Compatible with Chrome, Microsoft Edge, Mozilla Firefox, Brave, and Opera.
 */

(function() {
    'use strict';

    if (window.__EDM_CONTENT_SCRIPT_INITIALIZED__) return;
    window.__EDM_CONTENT_SCRIPT_INITIALIZED__ = true;

    // Configuration Constants
    const RESOLVER_TIMEOUT_MS = 6000;
    const POLL_INTERVAL_MS = 1200;

    const CandidateState = {
        DISCOVERED: 'DISCOVERED',
        ANALYZING: 'ANALYZING',
        READY: 'READY',
        SELECTOR_OPEN: 'SELECTOR_OPEN',
        HANDOFF_PENDING: 'HANDOFF_PENDING',
        HANDOFF_CONFIRMED: 'HANDOFF_CONFIRMED',
        DOWNLOADING: 'DOWNLOADING',
        COMPLETED: 'COMPLETED',
        FAILED: 'FAILED',
        DESTROYED: 'DESTROYED'
    };

    // Global in-memory state
    const variantCache = new Map();
    const activeOverlays = new Map();
    let globalActiveDropdown = null;
    let inPageYouTubeData = null;
    let scanTimer = null;

    // =========================================================================
    // 1. UTILITY & VALIDATION HELPERS
    // =========================================================================
    function formatBytes(bytes) {
        if (bytes === undefined || bytes === null || isNaN(bytes) || bytes <= 0) return 'Stream Size';
        const k = 1024;
        const dm = 1;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function generateDownloadIdentity(url, quality, filename, directUrl) {
        return `${url || ''}|${quality || ''}|${filename || ''}|${directUrl || ''}`;
    }

    class FormatValidator {
        static isValidMediaUrl(url) {
            if (!url || typeof url !== 'string') return false;
            const trimmed = url.trim();
            if (!trimmed.startsWith('http://') && !trimmed.startsWith('https://')) return false;

            try {
                const u = new URL(trimmed);
                // Reject web page HTML URLs acting as direct media streams
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
            if (!cipherStr) return '';
            try {
                const params = new URLSearchParams(cipherStr);
                const baseUrl = params.get('url');
                if (!baseUrl || !FormatValidator.isValidMediaUrl(baseUrl)) return '';

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
    }

    // =========================================================================
    // 2. MAIN-WORLD YOUTUBE STREAM EXTRACTOR BRIDGE
    // =========================================================================
    class YouTubeInPageExtractor {
        static init() {
            if (!window.location.hostname.includes('youtube.com')) return;

            // 1. Listen for player response messages from bridge
            window.addEventListener('message', (event) => {
                if (event.data && event.data.type === '__EDM_YT_PLAYER_DATA_RESPONSE__') {
                    if (event.data.playerResponse && event.data.playerResponse.streamingData) {
                        inPageYouTubeData = event.data.playerResponse;
                        const res = YouTubeInPageExtractor.processPlayerResponse(event.data.playerResponse);
                        if (res && res.variants && res.variants.length > 0) {
                            for (const [candId, overlay] of activeOverlays.entries()) {
                                overlay.updateBadgeCount(res.variants.length);
                                if (overlay.isOpen) {
                                    overlay.currentVariants = res.variants;
                                    overlay.renderVariants(res.variants, window.location.href);
                                }
                            }
                        }
                    }
                }
            });

            // 2. Inject bridge script into DOM
            this.injectBridgeScript();

            // 3. Extract from DOM scripts as immediate synchronous fallback
            this.extractFromDomScripts();

            // 4. Handle SPA navigation
            window.addEventListener('yt-navigate-finish', () => {
                inPageYouTubeData = null;
                setTimeout(() => {
                    this.requestPlayerResponseFromBridge();
                    this.extractFromDomScripts();
                }, 300);
            });
        }

        static injectBridgeScript() {
            try {
                if (document.getElementById('__edm_yt_bridge__')) return;
                const script = document.createElement('script');
                script.id = '__edm_yt_bridge__';
                script.src = chrome.runtime.getURL('yt-bridge.js');
                (document.head || document.documentElement).appendChild(script);
            } catch (e) {}
        }

        static requestPlayerResponseFromBridge() {
            window.postMessage({ type: '__EDM_REQUEST_YT_PLAYER_DATA__' }, '*');
        }

        static extractFromDomScripts() {
            try {
                const scripts = document.querySelectorAll('script');
                for (const script of scripts) {
                    const text = script.textContent || '';
                    if (text.includes('ytInitialPlayerResponse') && text.includes('streamingData')) {
                        const match = text.match(/ytInitialPlayerResponse\s*=\s*(\{.+?\});/s) ||
                                      text.match(/var\s+ytInitialPlayerResponse\s*=\s*(\{.+?\});/s);
                        if (match && match[1]) {
                            try {
                                const parsed = JSON.parse(match[1]);
                                if (parsed && parsed.streamingData) {
                                    inPageYouTubeData = parsed;
                                    this.processPlayerResponse(parsed);
                                    return;
                                }
                            } catch (e) {}
                        }
                    }
                }
            } catch (e) {}
        }

        static processPlayerResponse(playerResponse) {
            if (!playerResponse || !playerResponse.streamingData) return null;

            const streamingData = playerResponse.streamingData;
            const videoDetails = playerResponse.videoDetails || {};
            const title = videoDetails.title || MediaCandidateDetector.getPageMediaTitle() || document.title.replace(' - YouTube', '').trim();
            const durationSec = parseInt(videoDetails.lengthSeconds, 10) || 0;

            const progressiveFormats = streamingData.formats || [];
            const adaptiveFormats = streamingData.adaptiveFormats || [];
            const allRawFormats = [...progressiveFormats, ...adaptiveFormats];

            if (allRawFormats.length === 0) return null;

            // Extract real audio streams
            const audioStreams = [];
            for (const a of adaptiveFormats) {
                const mime = a.mimeType || '';
                if (!mime.startsWith('audio/')) continue;

                let audioUrl = a.url || '';
                if (!audioUrl && (a.signatureCipher || a.cipher)) {
                    audioUrl = FormatValidator.parseAndValidateCipherUrl(a.signatureCipher || a.cipher);
                }

                if (audioUrl && FormatValidator.isValidMediaUrl(audioUrl)) {
                    audioStreams.push({
                        itag: a.itag,
                        url: audioUrl,
                        mimeType: mime,
                        bitrate: a.bitrate || a.averageBitrate || 128000,
                        contentLength: parseInt(a.contentLength, 10) || 0
                    });
                }
            }

            audioStreams.sort((a, b) => b.bitrate - a.bitrate);
            const bestAudio = audioStreams[0] || null;
            const bestAudioSize = bestAudio ? (bestAudio.contentLength || (durationSec > 0 ? Math.round((bestAudio.bitrate * durationSec) / 8) : 0)) : 0;

            const variants = [];
            const seenKeys = new Set();

            // 1. Process Video Streams
            for (const f of allRawFormats) {
                const mime = f.mimeType || '';
                const isVideo = mime.startsWith('video/') || (f.height > 0);
                if (!isVideo) continue;

                const height = f.height || (f.qualityLabel ? parseInt(f.qualityLabel, 10) : 0);
                if (height <= 0) continue;

                const fps = f.fps || (f.qualityLabel && f.qualityLabel.includes('60') ? 60 : 30);
                const isWebm = mime.includes('webm');
                const container = isWebm ? 'webm' : 'mp4';
                const codec = isWebm ? 'VP9' : (mime.includes('av01') ? 'AV1' : 'H.264');
                const isAdaptive = !f.audioQuality && !mime.includes('audio');

                // Real Stream URL extraction with cipher resolution
                let streamUrl = f.url || '';
                if (!streamUrl && (f.signatureCipher || f.cipher)) {
                    streamUrl = FormatValidator.parseAndValidateCipherUrl(f.signatureCipher || f.cipher);
                }

                // Strict validation: Reject if no valid media stream URL is found
                if (!streamUrl || !FormatValidator.isValidMediaUrl(streamUrl)) {
                    continue;
                }

                // Quality Label
                let qualityLabel = f.qualityLabel || `${height}p`;
                if (height >= 2160 && !qualityLabel.includes('4K')) qualityLabel = `4K ${qualityLabel}`;
                else if (height >= 1440 && !qualityLabel.includes('2K')) qualityLabel = `2K ${qualityLabel}`;
                else if (height >= 1080 && !qualityLabel.includes('FHD') && !qualityLabel.includes('Full HD')) qualityLabel = `${qualityLabel} Full HD`;
                else if (height >= 720 && !qualityLabel.includes('HD')) qualityLabel = `${qualityLabel} HD`;

                const key = `${height}_${fps}_${container}_${codec}`;
                if (seenKeys.has(key)) continue;
                seenKeys.add(key);

                // Accurate byte size calculation
                let videoSize = parseInt(f.contentLength, 10) || 0;
                if (videoSize <= 0 && durationSec > 0 && f.bitrate) {
                    videoSize = Math.round((f.bitrate * durationSec) / 8);
                }

                let totalSize = videoSize;
                if (isAdaptive && bestAudioSize > 0) {
                    totalSize += bestAudioSize;
                }

                const matchingAudio = audioStreams.find(a => (a.mimeType && a.mimeType.includes(container))) || bestAudio;

                variants.push({
                    variantId: `yt_${f.itag || height}_${container}`,
                    qualityLabel: `${qualityLabel} (${container.toUpperCase()})`,
                    width: f.width || Math.round((height * 16) / 9),
                    height: height,
                    frameRate: fps,
                    bitrate: f.bitrate || 0,
                    codec: codec,
                    container: container,
                    audioCodec: isAdaptive ? (matchingAudio ? (container === 'webm' ? 'Opus' : 'AAC') : 'AAC') : 'AAC',
                    hasAudio: true,
                    isAudioOnly: false,
                    requiresFfmpegMerge: isAdaptive && !!matchingAudio,
                    directUrl: streamUrl,
                    audioStreamUrl: isAdaptive && matchingAudio ? matchingAudio.url : '',
                    estimatedSizeBytes: totalSize > 0 ? totalSize : -1,
                    formatArg: isAdaptive ? `-f ${f.itag || 'bestvideo'}+bestaudio/best` : `-f ${f.itag || 'best'}`
                });
            }

            // 2. Process Audio-Only Streams
            const seenAudioKeys = new Set();
            for (const a of audioStreams) {
                const mime = a.mimeType || '';
                const isWebm = mime.includes('webm') || mime.includes('opus');
                const container = isWebm ? 'webm' : 'm4a';
                const codec = isWebm ? 'Opus' : 'AAC';
                const bitrateKbps = Math.round(a.bitrate / 1000);

                const audioKey = `${container}_${bitrateKbps}`;
                if (seenAudioKeys.has(audioKey)) continue;
                seenAudioKeys.add(audioKey);

                let audioSize = a.contentLength || 0;
                if (audioSize <= 0 && durationSec > 0 && a.bitrate) {
                    audioSize = Math.round((a.bitrate * durationSec) / 8);
                }

                variants.push({
                    variantId: `yt_audio_${a.itag || bitrateKbps}_${container}`,
                    qualityLabel: `${container.toUpperCase()} Audio (${bitrateKbps} kbps)`,
                    width: 0,
                    height: 0,
                    frameRate: 0,
                    bitrate: a.bitrate || 0,
                    audioBitrate: a.bitrate || 0,
                    codec: codec,
                    audioCodec: codec,
                    container: container === 'm4a' ? 'mp3' : container,
                    hasAudio: true,
                    isAudioOnly: true,
                    requiresFfmpegMerge: false,
                    directUrl: a.url,
                    audioStreamUrl: '',
                    estimatedSizeBytes: audioSize > 0 ? audioSize : -1,
                    formatArg: `-f ${a.itag || 'bestaudio'}/bestaudio`
                });
            }

            if (variants.length > 0) {
                const result = {
                    success: true,
                    title: title,
                    variants: variants
                };
                variantCache.set(window.location.href, result);
                return result;
            }

            return null;
        }

        static getCachedOrExtractedVariants(mediaUrl) {
            // Check cache
            const cached = variantCache.get(mediaUrl) || variantCache.get(window.location.href);
            if (cached && cached.variants && cached.variants.length > 0) {
                return cached;
            }

            // Extract from in-page YouTube data
            if (inPageYouTubeData) {
                const res = this.processPlayerResponse(inPageYouTubeData);
                if (res && res.variants && res.variants.length > 0) return res;
            }

            // Try extracting from DOM scripts
            this.extractFromDomScripts();
            if (inPageYouTubeData) {
                const res = this.processPlayerResponse(inPageYouTubeData);
                if (res && res.variants && res.variants.length > 0) return res;
            }

            return null;
        }
    }

    // =========================================================================
    // 3. CANDIDATE DISCOVERY ENGINE (YouTube & Generic HTML5 Players)
    // =========================================================================
    class MediaCandidateDetector {
        static getPageMediaTitle() {
            if (window.location.hostname.includes('youtube.com')) {
                const titleElem = document.querySelector('h1.ytd-watch-metadata yt-formatted-string, #title h1, h1.title');
                if (titleElem && titleElem.textContent.trim()) {
                    return titleElem.textContent.trim();
                }
            }
            const ogTitle = document.querySelector('meta[property="og:title"]');
            if (ogTitle && ogTitle.content) return ogTitle.content.trim();

            const h1 = document.querySelector('h1');
            if (h1 && h1.textContent.trim()) return h1.textContent.trim();

            return document.title.replace(' - YouTube', '').trim() || 'Video Media';
        }

        static findMediaCandidates() {
            const candidates = [];

            // 1. YouTube Player Detection
            if (window.location.hostname.includes('youtube.com')) {
                const playerContainer = document.getElementById('movie_player') ||
                                        document.querySelector('.html5-video-player') ||
                                        document.querySelector('ytd-player #container');
                if (playerContainer) {
                    candidates.push({
                        candidateId: 'yt_main_player',
                        type: 'youtube',
                        container: playerContainer,
                        url: window.location.href,
                        title: this.getPageMediaTitle(),
                        state: CandidateState.DISCOVERED
                    });
                    return candidates;
                }
            }

            // 2. Generic HTML5 Video Elements
            const videos = document.querySelectorAll('video');
            videos.forEach((v, index) => {
                const rect = v.getBoundingClientRect();
                if (rect.width < 120 || rect.height < 90) return; // ignore tiny audio players/ads

                let src = v.currentSrc || v.src || '';
                if (!src) {
                    const sourceTag = v.querySelector('source[src]');
                    if (sourceTag) src = sourceTag.src;
                }

                if (src && !src.startsWith('blob:') && FormatValidator.isValidMediaUrl(src)) {
                    candidates.push({
                        candidateId: `video_html5_${index}`,
                        type: 'html5_video',
                        container: v.parentElement || v,
                        url: src,
                        title: this.getPageMediaTitle(),
                        state: CandidateState.DISCOVERED
                    });
                }
            });

            return candidates;
        }
    }

    // =========================================================================
    // 4. FROSTED-GLASS FLOATING PILL & FORMAT SELECTOR UI OVERLAY
    // =========================================================================
    class IdmDownloadOverlay {
        constructor(candidate) {
            this.candidate = candidate;
            this.container = candidate.container;
            this.panel = null;
            this.btn = null;
            this.dropdown = null;
            this.variantsList = null;
            this.activeTab = 'all';
            this.currentVariants = [];
            this.isOpen = false;
            this.currentRequestId = 0;
            this.init();
        }

        init() {
            this.panel = document.createElement('div');
            this.panel.className = 'edm-floating-panel';
            this.panel.setAttribute('data-candidate-id', this.candidate.candidateId);

            const downloadIconSvg = `
                <svg class="edm-btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                    <polyline points="7 10 12 15 17 10"></polyline>
                    <line x1="12" y1="15" x2="12" y2="3"></line>
                </svg>
            `;

            this.panel.innerHTML = `
                <button class="edm-floating-btn" type="button" aria-label="Download this video with EDM" title="Download Video with Exclusive Download Manager">
                    <span class="edm-btn-icon-wrap">${downloadIconSvg}</span>
                    <span class="edm-btn-text">Download this video</span>
                    <span class="edm-btn-badge" style="display: none;">0</span>
                </button>
                <div class="edm-dropdown-card" style="display: none;" role="dialog" aria-label="EDM Video Quality & Format Selector">
                    <div class="edm-dropdown-header">
                        <div class="edm-dropdown-title-group">
                            <span class="edm-header-logo">EDM</span>
                            <span class="edm-header-title-text" title="${escapeHtml(this.candidate.title || 'Video Media')}">${escapeHtml(this.candidate.title || 'Video Media')}</span>
                        </div>
                        <div class="edm-dropdown-controls">
                            <button class="edm-download-all-opt" type="button" title="Fast Download Best Stream">Download Best</button>
                            <button class="edm-dropdown-close-btn" type="button" aria-label="Close" title="Close">✕</button>
                        </div>
                    </div>
                    <div class="edm-filter-tabs">
                        <button class="edm-tab-btn edm-tab-active" data-tab="all" type="button">All (<span class="edm-count-all">0</span>)</button>
                        <button class="edm-tab-btn" data-tab="video" type="button">🎬 Video (<span class="edm-count-video">0</span>)</button>
                        <button class="edm-tab-btn" data-tab="audio" type="button">🎵 Audio (<span class="edm-count-audio">0</span>)</button>
                    </div>
                    <div class="edm-variants-container"></div>
                    <div class="edm-dropdown-footer">
                        <span class="edm-footer-hint">⚡ 32-Socket Turbo Multi-Thread Download Stream</span>
                    </div>
                </div>
            `;

            this.btn = this.panel.querySelector('.edm-floating-btn');
            this.dropdown = this.panel.querySelector('.edm-dropdown-card');
            this.variantsList = this.panel.querySelector('.edm-variants-container');
            const closeBtn = this.panel.querySelector('.edm-dropdown-close-btn');
            const downloadBestBtn = this.panel.querySelector('.edm-download-all-opt');

            // Tab Buttons
            this.panel.querySelectorAll('.edm-tab-btn').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.switchTab(btn.getAttribute('data-tab'));
                });
            });

            // Toggle Dropdown on Floating Pill Click
            this.btn.addEventListener('click', (e) => {
                e.stopPropagation();
                e.preventDefault();
                if (this.isOpen) {
                    this.close();
                } else {
                    this.open();
                }
            });

            if (closeBtn) {
                closeBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.close();
                });
            }

            if (downloadBestBtn) {
                downloadBestBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    if (this.currentVariants && this.currentVariants.length > 0) {
                        const best = this.currentVariants.find(v => !v.isAudioOnly) || this.currentVariants[0];
                        this.executeDownload(best.directUrl, this.candidate.title, best.qualityLabel, best);
                    }
                });
            }

            // Close on outside click
            document.addEventListener('click', (e) => {
                if (this.isOpen && !this.panel.contains(e.target)) {
                    this.close();
                }
            });

            // Position container relative
            const currentPos = window.getComputedStyle(this.container).position;
            if (currentPos === 'static') {
                this.container.style.position = 'relative';
            }

            this.container.appendChild(this.panel);
            this.bindHoverBehavior();
            this.checkPreloadedVariants();
            YouTubeInPageExtractor.requestPlayerResponseFromBridge();
        }

        checkPreloadedVariants() {
            const preloaded = YouTubeInPageExtractor.getCachedOrExtractedVariants(this.candidate.url);
            if (preloaded && preloaded.variants && preloaded.variants.length > 0) {
                this.updateBadgeCount(preloaded.variants.length);
            }
        }

        updateBadgeCount(count) {
            const badge = this.panel?.querySelector('.edm-btn-badge');
            if (badge && count > 0) {
                badge.textContent = count;
                badge.style.display = 'inline-block';
            }
        }

        bindHoverBehavior() {
            this.panel.style.opacity = '0.35';
            this.panel.style.transition = 'opacity 0.25s ease, transform 0.25s ease';

            const showHover = () => {
                if (this.panel) this.panel.style.opacity = '1';
            };
            const hideHover = () => {
                if (this.panel && !this.isOpen) this.panel.style.opacity = '0.35';
            };

            this.container.addEventListener('mouseenter', showHover);
            this.container.addEventListener('mouseleave', hideHover);
            this.panel.addEventListener('mouseenter', showHover);
            this.panel.addEventListener('mouseleave', hideHover);
        }

        open() {
            if (globalActiveDropdown && globalActiveDropdown !== this) {
                globalActiveDropdown.close();
            }

            this.isOpen = true;
            this.candidate.state = CandidateState.SELECTOR_OPEN;
            this.dropdown.style.display = 'flex';
            this.panel.classList.add('edm-open');
            this.panel.style.opacity = '1';
            globalActiveDropdown = this;

            const headerTitle = this.panel.querySelector('.edm-header-title-text');
            if (headerTitle) {
                headerTitle.textContent = MediaCandidateDetector.getPageMediaTitle() || this.candidate.title || 'Video Media';
            }

            this.fetchVariants();
        }

        close() {
            this.isOpen = false;
            this.candidate.state = CandidateState.READY;
            if (this.dropdown) this.dropdown.style.display = 'none';
            if (this.panel) this.panel.classList.remove('edm-open');
            if (globalActiveDropdown === this) globalActiveDropdown = null;
        }

        switchTab(tabName) {
            this.activeTab = tabName || 'all';
            this.panel.querySelectorAll('.edm-tab-btn').forEach(btn => {
                if (btn.getAttribute('data-tab') === this.activeTab) {
                    btn.classList.add('edm-tab-active');
                } else {
                    btn.classList.remove('edm-tab-active');
                }
            });
            this.renderVariantsList();
        }

        fetchVariants() {
            const mediaUrl = this.candidate.url;
            const targetCandidateId = this.candidate.candidateId;
            const requestId = ++this.currentRequestId;

            // 1. Instant Cache or In-Page Player Data Check (0ms!)
            const localData = YouTubeInPageExtractor.getCachedOrExtractedVariants(mediaUrl);
            if (localData && localData.variants && localData.variants.length > 0) {
                this.candidate.state = CandidateState.READY;
                this.currentVariants = localData.variants;
                this.renderVariants(localData.variants, mediaUrl);
                this.updateBadgeCount(localData.variants.length);
                return;
            }

            // 2. Query Background Resolver with Loading State
            this.candidate.state = CandidateState.ANALYZING;
            this.renderLoadingState();

            let timedOut = false;
            const timer = setTimeout(() => {
                timedOut = true;
                if (this.currentRequestId === requestId && this.isOpen) {
                    this.renderEmptyState("No verified downloadable format available.");
                }
            }, RESOLVER_TIMEOUT_MS);

            try {
                YouTubeInPageExtractor.requestPlayerResponseFromBridge();

                chrome.runtime.sendMessage({
                    action: "GET_MEDIA_VARIANTS",
                    url: mediaUrl,
                    cookies: document.cookie
                }, (response) => {
                    clearTimeout(timer);
                    if (timedOut) return;

                    if (this.currentRequestId !== requestId || !this.isOpen || this.candidate.candidateId !== targetCandidateId) {
                        return;
                    }

                    const liveLocal = YouTubeInPageExtractor.getCachedOrExtractedVariants(mediaUrl);
                    if (liveLocal && liveLocal.variants && liveLocal.variants.length > 0) {
                        this.candidate.state = CandidateState.READY;
                        this.currentVariants = liveLocal.variants;
                        this.renderVariants(liveLocal.variants, mediaUrl);
                        this.updateBadgeCount(liveLocal.variants.length);
                        return;
                    }

                    const variantsList = (response && (response.variants || response.data || (response.result && response.result.variants))) || [];
                    const validVariants = variantsList.filter(v => v.directUrl && FormatValidator.isValidMediaUrl(v.directUrl));

                    if (Array.isArray(validVariants) && validVariants.length > 0) {
                        this.candidate.state = CandidateState.READY;
                        variantCache.set(mediaUrl, { success: true, variants: validVariants });
                        this.currentVariants = validVariants;
                        this.renderVariants(validVariants, mediaUrl);
                        this.updateBadgeCount(validVariants.length);
                    } else {
                        this.renderEmptyState("No verified downloadable format available.");
                    }
                });
            } catch (err) {
                clearTimeout(timer);
                this.renderEmptyState("Failed to retrieve stream formats from EDM.");
            }
        }

        renderLoadingState() {
            this.variantsList.innerHTML = `
                <div class="edm-state-box">
                    <div class="edm-spinner"></div>
                    <span class="edm-state-text">Extracting verified stream formats...</span>
                </div>
            `;
        }

        renderEmptyState(message) {
            this.currentVariants = [];
            this.updateBadgeCount(0);
            this.variantsList.innerHTML = `
                <div class="edm-state-box">
                    <span class="edm-state-text">${escapeHtml(message)}</span>
                </div>
            `;
        }

        renderVariants(variants, mediaUrl) {
            this.currentVariants = variants || [];

            const totalCount = this.currentVariants.length;
            const videoCount = this.currentVariants.filter(v => !v.isAudioOnly).length;
            const audioCount = this.currentVariants.filter(v => !!v.isAudioOnly).length;

            const cAll = this.panel.querySelector('.edm-count-all');
            const cVid = this.panel.querySelector('.edm-count-video');
            const cAud = this.panel.querySelector('.edm-count-audio');

            if (cAll) cAll.textContent = totalCount;
            if (cVid) cVid.textContent = videoCount;
            if (cAud) cAud.textContent = audioCount;

            this.renderVariantsList();
        }

        renderVariantsList() {
            const videoTitle = MediaCandidateDetector.getPageMediaTitle() || this.candidate.title || 'Video Media';
            this.variantsList.innerHTML = '';

            if (!this.currentVariants || this.currentVariants.length === 0) {
                this.renderEmptyState("No verified downloadable format available.");
                return;
            }

            let filtered = [...this.currentVariants];
            if (this.activeTab === 'video') {
                filtered = filtered.filter(v => !v.isAudioOnly);
            } else if (this.activeTab === 'audio') {
                filtered = filtered.filter(v => !!v.isAudioOnly);
            }

            const sorted = filtered.sort((a, b) => {
                if (a.isAudioOnly && !b.isAudioOnly) return 1;
                if (!a.isAudioOnly && b.isAudioOnly) return -1;
                if (a.isAudioOnly && b.isAudioOnly) {
                    return (b.audioBitrate || b.bitrate || 0) - (a.audioBitrate || a.bitrate || 0);
                }
                const hA = a.height || 0;
                const hB = b.height || 0;
                if (hB !== hA) return hB - hA;
                const sA = a.estimatedSizeBytes || 0;
                const sB = b.estimatedSizeBytes || 0;
                if (sB !== sA) return sB - sA;
                return (b.bitrate || 0) - (a.bitrate || 0);
            });

            if (sorted.length === 0) {
                this.variantsList.innerHTML = `
                    <div class="edm-state-box">
                        <span class="edm-state-text">No ${this.activeTab} streams found.</span>
                    </div>
                `;
                return;
            }

            sorted.forEach((v) => {
                const item = document.createElement('div');
                item.className = 'edm-variant-row' + (v.isAudioOnly ? ' edm-audio-row' : '');
                item.setAttribute('role', 'button');
                item.setAttribute('tabindex', '0');

                const ext = (v.container || (v.isAudioOnly ? 'mp3' : 'mp4')).toUpperCase();
                const qualityLabel = v.qualityLabel || (v.height > 0 ? `${v.height}p` : 'Standard');
                const sizeText = formatBytes(v.estimatedSizeBytes);

                let badgeClass = 'edm-badge-sd';
                let badgeText = `${v.height || 480}P`;
                if (v.isAudioOnly) {
                    badgeClass = 'edm-badge-audio';
                    badgeText = 'AUDIO';
                } else if (v.height >= 2160 || qualityLabel.includes('4K')) {
                    badgeClass = 'edm-badge-4k';
                    badgeText = '4K UHD';
                } else if (v.height >= 1440 || qualityLabel.includes('2K')) {
                    badgeClass = 'edm-badge-2k';
                    badgeText = '2K QHD';
                } else if (v.height >= 1080) {
                    badgeClass = 'edm-badge-fhd';
                    badgeText = '1080P FHD';
                } else if (v.height >= 720) {
                    badgeClass = 'edm-badge-hd';
                    badgeText = '720P HD';
                }

                let metaTags = [];
                if (idx === 0 && !v.isAudioOnly) {
                    metaTags.push('★ Best Quality');
                }
                if (v.isAudioOnly) {
                    const kbps = v.audioBitrate > 0 ? `${Math.round(v.audioBitrate / 1000)} kbps` : '160 kbps';
                    metaTags.push(ext);
                    metaTags.push(v.audioCodec || 'AAC');
                    metaTags.push(kbps);
                } else {
                    metaTags.push(ext);
                    if (v.codec && v.codec !== 'none') metaTags.push(v.codec.toUpperCase());
                    if (v.frameRate > 30) metaTags.push(`${Math.round(v.frameRate)} FPS`);
                    metaTags.push(v.requiresFfmpegMerge ? 'Turbo Multi-Stream' : 'Direct Video');
                }

                item.innerHTML = `
                    <div class="edm-variant-left">
                        <span class="edm-quality-badge ${badgeClass}">${badgeText}</span>
                        <div class="edm-variant-info">
                            <div class="edm-variant-title">${escapeHtml(qualityLabel)}</div>
                            <div class="edm-variant-meta">
                                ${metaTags.map(m => `<span class="edm-meta-item">${escapeHtml(m)}</span>`).join('')}
                            </div>
                        </div>
                    </div>
                    <div class="edm-variant-right">
                        <span class="edm-variant-size">${sizeText}</span>
                        <button class="edm-row-download-btn" type="button">
                            <span>Download</span>
                        </button>
                    </div>
                `;

                const handleSelection = (e) => {
                    e.stopPropagation();
                    this.executeDownload(v.directUrl, videoTitle, qualityLabel, v);
                };

                item.addEventListener('click', handleSelection);
                item.querySelector('.edm-row-download-btn')?.addEventListener('click', handleSelection);
                item.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') handleSelection(e);
                });

                this.variantsList.appendChild(item);
            });
        }

        executeDownload(url, title, quality, variant) {
            if (!url || !FormatValidator.isValidMediaUrl(url)) {
                this.showToast('⚠️ Cannot download: Invalid media stream URL.');
                return;
            }

            const cleanTitle = (title || 'media').replace(/[/\\?%*:|"<>]/g, '_').trim();
            const isAudio = !!variant?.isAudioOnly || (quality && quality.includes('Audio'));
            const ext = variant?.container ? `.${variant.container.toLowerCase()}` : (isAudio ? '.mp3' : '.mp4');
            const filename = cleanTitle + ext;

            const downloadIdentity = generateDownloadIdentity(url, quality, filename, variant?.directUrl || url);

            this.candidate.state = CandidateState.HANDOFF_PENDING;
            this.showToast(`⚡ Sending to EDM: ${quality} (${filename})...`);

            try {
                chrome.runtime.sendMessage({
                    action: 'START_EDM_DOWNLOAD',
                    url: url,
                    videoUrl: variant?.directUrl || url,
                    audioUrl: variant?.audioStreamUrl || '',
                    manifestUrl: variant?.manifestUrl || '',
                    pageUrl: window.location.href,
                    title: title || 'Video Media',
                    filename: filename,
                    fileName: filename,
                    quality: quality || 'Original',
                    format: variant?.container || (isAudio ? 'mp3' : 'mp4'),
                    formatId: variant?.variantId || '',
                    formatArg: variant?.formatArg || '',
                    width: variant?.width || 0,
                    height: variant?.height || 0,
                    fps: variant?.frameRate || 0,
                    videoCodec: variant?.codec || '',
                    codec: variant?.codec || '',
                    audioCodec: variant?.audioCodec || '',
                    container: variant?.container || '',
                    requiresFfmpegMerge: !!variant?.requiresFfmpegMerge,
                    downloadIdentity: downloadIdentity,
                    correlationId: 'edm_req_' + Date.now(),
                    estimatedSizeBytes: variant?.estimatedSizeBytes || -1,
                    videoSizeBytes: variant?.videoSizeBytes || -1,
                    audioSizeBytes: variant?.audioSizeBytes || -1,
                    isAudioOnly: isAudio,
                    cookies: document.cookie,
                    source: 'ContentScript'
                }, (resp) => {
                    if (resp && resp.success) {
                        this.candidate.state = CandidateState.HANDOFF_CONFIRMED;
                        this.showToast(`✅ Download queued in EDM: ${filename}`);
                    } else {
                        this.candidate.state = CandidateState.FAILED;
                        this.showToast(`❌ EDM handoff failed: ${resp?.error || 'Unknown error'}`);
                    }
                });
            } catch (err) {
                this.candidate.state = CandidateState.FAILED;
                this.showToast(`❌ Error contacting EDM extension bridge.`);
            }

            this.close();
        }

        showToast(message) {
            let container = document.querySelector('.edm-toast-container');
            if (!container) {
                container = document.createElement('div');
                container.className = 'edm-toast-container';
                document.body.appendChild(container);
            }

            const toast = document.createElement('div');
            toast.className = 'edm-toast';
            toast.textContent = message;
            container.appendChild(toast);

            setTimeout(() => {
                toast.style.opacity = '0';
                toast.style.transform = 'translateY(10px)';
                toast.style.transition = 'all 0.3s ease';
                setTimeout(() => toast.remove(), 300);
            }, 3500);
        }
    }

    // =========================================================================
    // 5. MASTER CONTROLLER & DOM MUTATION WATCHER
    // =========================================================================
    function scanAndAttachOverlays() {
        const candidates = MediaCandidateDetector.findMediaCandidates();
        candidates.forEach((cand) => {
            if (!activeOverlays.has(cand.candidateId)) {
                const overlay = new IdmDownloadOverlay(cand);
                activeOverlays.set(cand.candidateId, overlay);
            }
        });
    }

    // Initialize YouTube In-Page Extractor Bridge
    YouTubeInPageExtractor.init();

    // DOM Ready and Initial Discovery
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            scanAndAttachOverlays();
            scanTimer = setInterval(scanAndAttachOverlays, POLL_INTERVAL_MS);
        });
    } else {
        scanAndAttachOverlays();
        scanTimer = setInterval(scanAndAttachOverlays, POLL_INTERVAL_MS);
    }

    // Fullscreen auto-hide handling
    function handleFullscreenChange() {
        const isFullscreen = !!(document.fullscreenElement || document.webkitFullscreenElement || document.mozFullScreenElement);
        document.querySelectorAll('.edm-floating-panel').forEach((panel) => {
            if (isFullscreen) {
                panel.style.display = 'none';
            } else {
                panel.style.display = 'block';
            }
        });
    }
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    document.addEventListener('mozfullscreenchange', handleFullscreenChange);

    // Message Bridge for Extension Popup & Background
    chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
        if (!message || !message.action) return false;

        if (message.action === 'GET_ACTIVE_MEDIA_SESSION') {
            const pageTitle = MediaCandidateDetector.getPageMediaTitle() || document.title || 'Web Video';
            let variants = [];

            for (const overlay of activeOverlays.values()) {
                if (overlay.currentVariants && overlay.currentVariants.length > 0) {
                    variants = overlay.currentVariants;
                    break;
                }
            }

            if (variants.length === 0) {
                const local = YouTubeInPageExtractor.getCachedOrExtractedVariants(window.location.href);
                if (local && local.variants) variants = local.variants;
            }

            const videoReps = variants.map((v) => ({
                formatId: v.variantId || v.itag || v.qualityLabel || 'fmt',
                qualityLabel: v.qualityLabel || (v.height ? `${v.height}p` : 'Auto'),
                container: v.container || (v.isAudioOnly ? 'mp3' : 'mp4'),
                bitrate: v.bitrate || v.audioBitrate || 0,
                height: v.height || 0,
                width: v.width || 0,
                url: v.directUrl || v.url,
                videoUrl: v.directUrl || v.url,
                audioUrl: v.audioStreamUrl || '',
                isVideoOnly: !v.isAudioOnly && !!v.requiresFfmpegMerge,
                isAudioOnly: !!v.isAudioOnly,
                estimatedSizeBytes: v.estimatedSizeBytes || -1
            }));

            sendResponse({
                success: true,
                session: {
                    title: pageTitle,
                    duration: 0,
                    videoRepresentations: videoReps,
                    maximumAvailable: videoReps.length > 0 ? videoReps[0] : null
                }
            });
            return false;
        }

        if (message.action === 'SCAN_FOR_VIDEOS') {
            scanAndAttachOverlays();
            sendResponse({ success: true, count: activeOverlays.size });
            return false;
        }

        return false;
    });
})();
