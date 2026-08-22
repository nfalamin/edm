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

    const CandidateConfidence = {
        HIGH: 'HIGH',
        MEDIUM: 'MEDIUM',
        LOW: 'LOW'
    };

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
        STALE: 'STALE',
        DESTROYED: 'DESTROYED'
    };

    // Global in-memory state
    const variantCache = new Map();
    const activeOverlays = new Map();
    const activeJobIdentities = new Set();
    let globalActiveDropdown = null;
    let inPageYouTubeData = null;
    let scanTimer = null;

    // =========================================================================
    // 1. UTILITY & VALIDATION HELPERS
    // =========================================================================
    function formatBytes(bytes) {
        if (bytes === undefined || bytes === null || isNaN(bytes) || bytes <= 0) return 'Size unavailable';
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
        static isAdOrDecorativeElement(element) {
            if (!element) return false;
            if (element.closest('.ad-showing, .video-ads, .ytp-ad-module, [id*="google_ads"], [class*="sponsored"]')) {
                return true;
            }
            return false;
        }

        static isValidMediaElement(v) {
            if (!v) return false;
            if (this.isAdOrDecorativeElement(v)) return false;
            const rect = v.getBoundingClientRect();
            if (rect.width < 180 || rect.height < 100) return false;
            return true;
        }

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
                <div class="edm-idm-bar" role="button" tabindex="0" aria-label="Download this video with EDM" title="Download this video">
                    <div class="edm-idm-main-btn">
                        <span class="edm-idm-logo-wrap">
                            <svg class="edm-idm-icon" viewBox="0 0 24 24">
                                <polygon points="5 3 19 12 5 21 5 3"></polygon>
                            </svg>
                        </span>
                        <span class="edm-idm-text">Download this video</span>
                        <span class="edm-idm-badge" style="display: none;">0</span>
                    </div>
                    <div class="edm-idm-actions">
                        <span class="edm-idm-btn-help" title="EDM Settings & Info">?</span>
                        <span class="edm-idm-btn-close" title="Hide this button">✕</span>
                    </div>
                </div>
                <div class="edm-idm-dropdown-panel" style="display: none;" role="menu" aria-label="Download Formats">
                    <div class="edm-idm-header-opt" role="menuitem" tabindex="0" title="Download all available video streams">
                        <span>Download all</span>
                    </div>
                    <div class="edm-idm-divider"></div>
                    <div class="edm-idm-variants-list"></div>
                </div>
            `;

            this.btn = this.panel.querySelector('.edm-idm-main-btn') || this.panel.querySelector('.edm-idm-bar');
            this.dropdown = this.panel.querySelector('.edm-idm-dropdown-panel');
            this.variantsList = this.panel.querySelector('.edm-idm-variants-list');
            const closeBarBtn = this.panel.querySelector('.edm-idm-btn-close');
            const helpBarBtn = this.panel.querySelector('.edm-idm-btn-help');
            const downloadAllBtn = this.panel.querySelector('.edm-idm-header-opt');

            // Toggle Dropdown on Main Button Click
            this.btn.addEventListener('click', (e) => {
                e.stopPropagation();
                e.preventDefault();
                if (this.isOpen) {
                    this.close();
                } else {
                    this.open();
                }
            });

            if (closeBarBtn) {
                closeBarBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.panel.style.display = 'none';
                });
            }

            if (helpBarBtn) {
                helpBarBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    if (chrome?.runtime?.openOptionsPage) {
                        chrome.runtime.openOptionsPage();
                    }
                });
            }

            if (downloadAllBtn) {
                downloadAllBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    if (this.currentVariants && this.currentVariants.length > 0) {
                        const best = this.currentVariants.find(v => !v.isAudioOnly) || this.currentVariants[0];
                        this.showDownloadFileInfoDialog(best, this.candidate.title || 'Video Media');
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
            const badge = this.panel?.querySelector('.edm-idm-badge') || this.panel?.querySelector('.edm-btn-badge');
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
            if (this.dropdown) this.dropdown.style.display = 'flex';
            this.panel.classList.add('edm-open');
            this.panel.style.opacity = '1';
            globalActiveDropdown = this;

            this.fetchVariants();
        }

        close() {
            this.isOpen = false;
            this.candidate.state = CandidateState.READY;
            if (this.dropdown) this.dropdown.style.display = 'none';
            if (this.panel) this.panel.classList.remove('edm-open');
            if (globalActiveDropdown === this) globalActiveDropdown = null;
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
            if (!this.variantsList) return;
            this.variantsList.innerHTML = `
                <div class="edm-idm-state">
                    <span>Extracting available formats...</span>
                </div>
            `;
        }

        renderEmptyState(message) {
            this.currentVariants = [];
            this.updateBadgeCount(0);
            if (!this.variantsList) return;
            this.variantsList.innerHTML = `
                <div class="edm-idm-state">
                    <span>${escapeHtml(message)}</span>
                </div>
            `;
        }

        renderVariants(variants, mediaUrl) {
            this.currentVariants = variants || [];
            this.renderVariantsList();
        }

        renderVariantsList() {
            const videoTitle = MediaCandidateDetector.getPageMediaTitle() || this.candidate.title || 'Video Media';
            if (!this.variantsList) return;
            this.variantsList.innerHTML = '';

            if (!this.currentVariants || this.currentVariants.length === 0) {
                this.renderEmptyState("No verified downloadable format available.");
                return;
            }

            const sortedVariants = [...this.currentVariants].sort((a, b) => {
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

            sortedVariants.forEach((v, idx) => {
                const item = document.createElement('div');
                item.className = 'edm-idm-row';
                item.setAttribute('role', 'menuitem');
                item.setAttribute('tabindex', '0');

                const ext = (v.container || (v.isAudioOnly ? 'mp3' : 'mp4')).toUpperCase();
                let qualityDesc = '';

                if (v.isAudioOnly) {
                    const kbps = v.audioBitrate > 0 ? `${Math.round(v.audioBitrate / 1000)} kbps` : '160 kbps';
                    qualityDesc = `${ext} audio, quality ${kbps}`;
                } else {
                    const h = v.height || 480;
                    const hdTag = h >= 720 ? ' HD' : '';
                    qualityDesc = `${ext} file, quality ${h}p${hdTag}`;
                }

                // Clean and truncate title for single-line display
                let displayTitle = videoTitle;
                if (displayTitle.length > 36) {
                    displayTitle = displayTitle.substring(0, 34) + '..';
                }

                item.innerHTML = `
                    <span class="edm-idm-row-num">${idx + 1}.</span>
                    <span class="edm-idm-row-title" title="${escapeHtml(videoTitle)}">${escapeHtml(displayTitle)}</span>
                    <span class="edm-idm-row-sep">|</span>
                    <span class="edm-idm-row-desc">${escapeHtml(qualityDesc)}</span>
                `;

                const handleSelection = (e) => {
                    e.stopPropagation();
                    this.showDownloadFileInfoDialog(v, videoTitle);
                };

                item.addEventListener('click', handleSelection);
                item.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') handleSelection(e);
                });

                this.variantsList.appendChild(item);
            });
        }

        showDownloadFileInfoDialog(variant, videoTitle) {
            this.close();

            const existingDialog = document.querySelector('.edm-dialog-backdrop');
            if (existingDialog) existingDialog.remove();

            const cleanTitle = (videoTitle || 'Video_Media').replace(/[/\\?%*:|"<>]/g, '_').trim();
            const isAudio = !!variant?.isAudioOnly;
            const defaultCategory = isAudio ? 'Audio' : 'Video';
            const ext = variant?.container ? variant.container.toLowerCase() : (isAudio ? 'mp3' : 'mp4');
            const defaultFilename = `${cleanTitle}.${ext}`;
            const defaultDir = `Downloads\\${defaultCategory}\\`;
            const defaultSavePath = `${defaultDir}${defaultFilename}`;
            const sizeStr = formatBytes(variant?.estimatedSizeBytes || variant?.videoSizeBytes || -1);

            const backdrop = document.createElement('div');
            backdrop.className = 'edm-dialog-backdrop';

            backdrop.innerHTML = `
                <div class="edm-file-info-dialog" role="dialog" aria-modal="true" aria-label="Download File Info">
                    <div class="edm-dialog-titlebar">
                        <div class="edm-dialog-title">
                            <svg class="edm-dialog-title-icon" viewBox="0 0 24 24">
                                <polygon points="5 3 19 12 5 21 5 3"></polygon>
                            </svg>
                            <span>Download File Info</span>
                        </div>
                        <div class="edm-dialog-win-controls">
                            <span class="edm-dialog-close-btn" title="Close">✕</span>
                        </div>
                    </div>
                    <div class="edm-dialog-body">
                        <div class="edm-form-row">
                            <span class="edm-form-label">URL:</span>
                            <div class="edm-form-control-wrap">
                                <input class="edm-form-input edm-input-url" type="text" readonly value="${escapeHtml(variant?.directUrl || '')}" title="${escapeHtml(variant?.directUrl || '')}">
                            </div>
                        </div>
                        <div class="edm-form-row">
                            <span class="edm-form-label">Category:</span>
                            <div class="edm-form-control-wrap">
                                <select class="edm-form-select edm-select-category">
                                    <option value="Video" ${defaultCategory === 'Video' ? 'selected' : ''}>Video</option>
                                    <option value="Audio" ${defaultCategory === 'Audio' ? 'selected' : ''}>Audio</option>
                                    <option value="General">General</option>
                                    <option value="Compressed">Compressed</option>
                                    <option value="Programs">Programs</option>
                                    <option value="Documents">Documents</option>
                                </select>
                                <button class="edm-form-btn-small edm-btn-cat-add" type="button" title="Add Category">+</button>
                            </div>
                        </div>
                        <div class="edm-form-row">
                            <span class="edm-form-label">Save As:</span>
                            <div class="edm-form-control-wrap">
                                <input class="edm-form-input edm-input-saveas" type="text" value="${escapeHtml(defaultSavePath)}">
                                <button class="edm-form-btn-small edm-btn-browse" type="button" title="Browse Download Folder">...</button>
                            </div>
                            <div class="edm-file-badge-col">
                                <span class="edm-file-badge-icon">${isAudio ? '🎵' : '🎬'}</span>
                                <span class="edm-file-badge-size">${sizeStr}</span>
                            </div>
                        </div>
                        <div class="edm-form-checkbox-row">
                            <label class="edm-checkbox-label">
                                <input type="checkbox" class="edm-cb-remember" checked>
                                <span>Remember this path for "${defaultCategory}" category</span>
                            </label>
                            <input class="edm-form-input edm-input-remember-path" type="text" value="${escapeHtml(defaultDir)}">
                        </div>
                        <div class="edm-form-row">
                            <span class="edm-form-label">Description:</span>
                            <div class="edm-form-control-wrap">
                                <input class="edm-form-input edm-input-desc" type="text" placeholder="Optional description for EDM">
                            </div>
                        </div>
                    </div>
                    <div class="edm-dialog-footer">
                        <button class="edm-dialog-btn edm-btn-secondary edm-btn-dl-later" type="button">Download Later</button>
                        <button class="edm-dialog-btn edm-btn-primary edm-btn-start-dl" type="button">Start Download</button>
                        <button class="edm-dialog-btn edm-btn-secondary edm-btn-dl-cancel" type="button">Cancel</button>
                    </div>
                </div>
            `;

            const closeDialog = () => {
                backdrop.remove();
            };

            const closeBtn = backdrop.querySelector('.edm-dialog-close-btn');
            const cancelBtn = backdrop.querySelector('.edm-btn-dl-cancel');
            const startBtn = backdrop.querySelector('.edm-btn-start-dl');
            const laterBtn = backdrop.querySelector('.edm-btn-dl-later');
            const catSelect = backdrop.querySelector('.edm-select-category');
            const saveAsInput = backdrop.querySelector('.edm-input-saveas');
            const remPathInput = backdrop.querySelector('.edm-input-remember-path');
            const remCbLabel = backdrop.querySelector('.edm-checkbox-label span');
            const browseBtn = backdrop.querySelector('.edm-btn-browse');

            closeBtn?.addEventListener('click', closeDialog);
            cancelBtn?.addEventListener('click', closeDialog);

            catSelect?.addEventListener('change', (e) => {
                const newCat = e.target.value;
                const newDir = `Downloads\\${newCat}\\`;
                saveAsInput.value = `${newDir}${defaultFilename}`;
                remPathInput.value = newDir;
                if (remCbLabel) remCbLabel.textContent = `Remember this path for "${newCat}" category`;
            });

            browseBtn?.addEventListener('click', () => {
                saveAsInput.focus();
                saveAsInput.select();
            });

            startBtn?.addEventListener('click', () => {
                const customSavePath = saveAsInput?.value.trim() || defaultSavePath;
                const customDesc = backdrop.querySelector('.edm-input-desc')?.value.trim() || '';
                closeDialog();
                this.executeDownload(variant.directUrl, videoTitle, variant.qualityLabel || `${variant.height}p`, variant, {
                    savePath: customSavePath,
                    category: catSelect?.value || defaultCategory,
                    description: customDesc,
                    isPaused: false
                });
            });

            laterBtn?.addEventListener('click', () => {
                const customSavePath = saveAsInput?.value.trim() || defaultSavePath;
                const customDesc = backdrop.querySelector('.edm-input-desc')?.value.trim() || '';
                closeDialog();
                this.executeDownload(variant.directUrl, videoTitle, variant.qualityLabel || `${variant.height}p`, variant, {
                    savePath: customSavePath,
                    category: catSelect?.value || defaultCategory,
                    description: customDesc,
                    isPaused: true
                });
            });

            // Close on outside backdrop click
            backdrop.addEventListener('click', (e) => {
                if (e.target === backdrop) closeDialog();
            });

            // ESC key to close
            const keyHandler = (e) => {
                if (e.key === 'Escape') {
                    closeDialog();
                    document.removeEventListener('keydown', keyHandler);
                }
            };
            document.addEventListener('keydown', keyHandler);

            document.body.appendChild(backdrop);
        }

        executeDownload(url, title, quality, variant, options = {}) {
            if (!url || !FormatValidator.isValidMediaUrl(url)) {
                this.showToast('⚠️ Cannot download: Invalid media stream URL.');
                return;
            }

            const cleanTitle = (title || 'media').replace(/[/\\?%*:|"<>]/g, '_').trim();
            const isAudio = !!variant?.isAudioOnly || (quality && quality.includes('Audio'));
            const ext = variant?.container ? `.${variant.container.toLowerCase()}` : (isAudio ? '.mp3' : '.mp4');
            const filename = cleanTitle + ext;

            const downloadIdentity = generateDownloadIdentity(url, quality, filename, variant?.directUrl || url);
            if (activeJobIdentities.has(downloadIdentity)) {
                this.showToast(`⚠️ Download already active: ${filename}`);
                return;
            }
            activeJobIdentities.add(downloadIdentity);

            this.candidate.state = CandidateState.HANDOFF_PENDING;
            const isPaused = !!options.isPaused;
            this.showToast(isPaused ? `⏳ Queuing in EDM: ${quality} (${filename})...` : `⚡ Sending to EDM: ${quality} (${filename})...`);

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
                    savePath: options.savePath || '',
                    category: options.category || (isAudio ? 'Audio' : 'Video'),
                    description: options.description || '',
                    isPaused: isPaused,
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
                        this.showToast(isPaused ? `⏳ Download queued in EDM: ${filename}` : `✅ Download started in EDM: ${filename}`);
                    } else {
                        this.candidate.state = CandidateState.FAILED;
                        this.showToast(`❌ EDM handoff failed: ${resp?.error || 'Ensure EDM Desktop app is open.'}`);
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

    // SPA Navigation and History API Hooking
    function hookHistoryApi() {
        const pushState = history.pushState;
        const replaceState = history.replaceState;
        history.pushState = function(...args) {
            pushState.apply(this, args);
            window.dispatchEvent(new Event('popstate'));
        };
        history.replaceState = function(...args) {
            replaceState.apply(this, args);
            window.dispatchEvent(new Event('popstate'));
        };

        const handleSpaNav = () => {
            for (const overlay of activeOverlays.values()) {
                overlay.currentRequestId++;
                overlay.close();
            }
            setTimeout(scanAndAttachOverlays, 200);
        };

        window.addEventListener('popstate', handleSpaNav);
        window.addEventListener('yt-navigate-finish', handleSpaNav);
    }
    hookHistoryApi();

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

    // Universal Generic Stream Receiver from stream-interceptor.js
    window.addEventListener('message', (event) => {
        if (event.data && event.data.type === '__EDM_GENERIC_STREAM_DETECTED__' && event.data.stream) {
            const stream = event.data.stream;
            const streamUrl = stream.url;
            if (!streamUrl) return;

            let targetOverlay = null;
            for (const overlay of activeOverlays.values()) {
                targetOverlay = overlay;
                break;
            }

            if (!targetOverlay) {
                scanAndAttachOverlays();
                for (const overlay of activeOverlays.values()) {
                    targetOverlay = overlay;
                    break;
                }
            }

            if (targetOverlay) {
                const isAudio = (stream.mimeType && stream.mimeType.includes('audio')) ||
                                streamUrl.endsWith('.mp3') || streamUrl.endsWith('.m4a') || streamUrl.endsWith('.aac') || streamUrl.endsWith('.flac');
                const isManifest = !!stream.isManifest;
                const extLabel = isManifest ? (streamUrl.includes('.m3u8') ? 'HLS' : 'DASH') : (isAudio ? 'Audio' : 'Video');
                const sizeLabel = stream.contentLength > 0 ? `${(stream.contentLength / (1024 * 1024)).toFixed(1)} MB` : 'Stream';

                const genericVariant = {
                    variantId: 'generic_' + Math.abs(generateDownloadIdentity(streamUrl, 0, '')),
                    qualityLabel: isManifest ? `Live Stream (${extLabel})` : `${extLabel} • ${sizeLabel}`,
                    container: isManifest ? 'm3u8' : (isAudio ? 'mp3' : 'mp4'),
                    directUrl: streamUrl,
                    url: streamUrl,
                    estimatedSizeBytes: stream.contentLength || -1,
                    isAudioOnly: isAudio,
                    isManifest: isManifest,
                    codec: isManifest ? 'HLS/DASH' : 'Direct'
                };

                if (!targetOverlay.currentVariants) targetOverlay.currentVariants = [];
                const exists = targetOverlay.currentVariants.some(v => (v.directUrl || v.url) === streamUrl);
                if (!exists) {
                    targetOverlay.currentVariants.push(genericVariant);
                    targetOverlay.updateBadgeCount(targetOverlay.currentVariants.length);
                    if (targetOverlay.isOpen) {
                        targetOverlay.renderVariants(targetOverlay.currentVariants, window.location.href);
                    }
                }
            }
        }
    });

    // DOM Ready, Initial Discovery, and MutationObserver
    let mutationDebounceTimer = null;
    const domObserver = new MutationObserver(() => {
        if (mutationDebounceTimer) clearTimeout(mutationDebounceTimer);
        mutationDebounceTimer = setTimeout(scanAndAttachOverlays, 300);
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            scanAndAttachOverlays();
            scanTimer = setInterval(scanAndAttachOverlays, POLL_INTERVAL_MS);
            if (document.body) {
                domObserver.observe(document.body, { childList: true, subtree: true });
            }
        });
    } else {
        scanAndAttachOverlays();
        scanTimer = setInterval(scanAndAttachOverlays, POLL_INTERVAL_MS);
        if (document.body) {
            domObserver.observe(document.body, { childList: true, subtree: true });
        }
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
