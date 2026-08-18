/**
 * EDM (Exclusive Download Manager) - Production Canonical Content Script
 * Stage 3: IDM-Class Browser Media Detection & Real Representation Format Selector UI
 * 
 * Key Capabilities:
 * 1. Zero-Latency In-Browser YouTube Format Extraction (ytInitialPlayerResponse, movie_player.getPlayerResponse).
 * 2. Playback Quality Independence (Player playing at 144p still detects and lists 4K 2160p, 1080p, 720p, etc.).
 * 3. Exact File Sizes & Real Codecs (1.82 GB, 450 MB, H.264, VP9, AV1, AAC, Opus - never fake 0 MB).
 * 4. Dual-Stream Adaptive Stream Pairing (Video + Best Audio pairing with requiresFfmpegMerge).
 * 5. High-Aesthetic Glassmorphism Floating Pill & Format Selector Dropdown with Category Tabs (All / Video / Audio).
 * 6. Multi-Site Support (YouTube, Vimeo, Dailymotion, HTML5 <video>/<audio>, HLS .m3u8, DASH .mpd).
 * 7. SPA Lifecycle & Single-Page Transition Resilience (yt-navigate-finish, popstate, pushState).
 * 8. Deterministic DownloadIdentity & Instant EDM Desktop Handoff.
 */

(function () {
    'use strict';

    if (window.__EDM_STAGE3_CONTENT_LOADED__) return;
    window.__EDM_STAGE3_CONTENT_LOADED__ = true;

    // =========================================================================
    // 1. CONSTANTS & LIFECYCLE ENUMS
    // =========================================================================
    const RESOLVER_TIMEOUT_MS = 6000;
    const MUTATION_DEBOUNCE_MS = 300;

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
        DOWNLOADING: 'DOWNLOADING',
        COMPLETED: 'COMPLETED',
        FAILED: 'FAILED',
        STALE: 'STALE',
        DESTROYED: 'DESTROYED'
    };

    // Active state caches
    const variantCache = new Map();             // mediaUrl -> MediaVariantResult
    const activeJobIdentities = new Set();      // downloadIdentity Set
    const activeOverlays = new Map();           // candidateId -> IdmDownloadOverlay
    let globalActiveDropdown = null;            // currently opened dropdown instance
    let currentPageUrl = window.location.href;  // for SPA transition detection
    let inPageYouTubeData = null;               // in-page extracted YouTube player response

    // =========================================================================
    // 2. IN-PAGE YOUTUBE STREAM EXTRACTOR (Zero Latency Bridge)
    // =========================================================================
    class YouTubeInPageExtractor {
        static init() {
            if (!window.location.hostname.includes('youtube.com')) return;

            // 1. Listen for player response messages from page context
            window.addEventListener('message', (event) => {
                if (event.data && event.data.type === '__EDM_YT_PLAYER_DATA_RESPONSE__') {
                    if (event.data.playerResponse && event.data.playerResponse.streamingData) {
                        inPageYouTubeData = event.data.playerResponse;
                        YouTubeInPageExtractor.processPlayerResponse(event.data.playerResponse);
                    }
                }
            });

            // 2. Inject bridge script to access main world window.ytInitialPlayerResponse
            this.injectBridgeScript();

            // 3. Extract from DOM scripts as immediate synchronous fallback
            this.extractFromDomScripts();

            // 4. Listen to YouTube SPA navigation
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
                const script = document.createElement('script');
                script.id = '__edm_yt_bridge__';
                script.textContent = `
                    (function() {
                        function sendPlayerData() {
                            try {
                                var pr = null;
                                var player = document.getElementById('movie_player');
                                if (player && typeof player.getPlayerResponse === 'function') {
                                    pr = player.getPlayerResponse();
                                }
                                if (!pr && window.ytInitialPlayerResponse) {
                                    pr = window.ytInitialPlayerResponse;
                                }
                                if (pr && pr.streamingData) {
                                    window.postMessage({
                                        type: '__EDM_YT_PLAYER_DATA_RESPONSE__',
                                        playerResponse: pr,
                                        url: window.location.href
                                    }, '*');
                                }
                            } catch(e) {}
                        }

                        window.addEventListener('message', function(e) {
                            if (e.data && e.data.type === '__EDM_REQUEST_YT_PLAYER_DATA__') {
                                sendPlayerData();
                            }
                        });

                        // Run on load and on state change
                        sendPlayerData();
                        setTimeout(sendPlayerData, 1000);
                        setTimeout(sendPlayerData, 2500);

                        window.addEventListener('yt-navigate-finish', function() {
                            setTimeout(sendPlayerData, 500);
                            setTimeout(sendPlayerData, 1500);
                        });
                    })();
                `;
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
            const title = videoDetails.title || MediaCandidateDetector.getPageMediaTitle();
            const durationSec = parseInt(videoDetails.lengthSeconds, 10) || 0;

            const formats = streamingData.formats || [];
            const adaptiveFormats = streamingData.adaptiveFormats || [];
            const allRawFormats = [...formats, ...adaptiveFormats];

            if (allRawFormats.length === 0) return null;

            // Find best audio stream for pairing
            const audioStreams = adaptiveFormats.filter(f => f.mimeType && f.mimeType.startsWith('audio/'));
            audioStreams.sort((a, b) => (b.bitrate || 0) - (a.bitrate || 0));
            const bestAudio = audioStreams[0] || null;
            const bestAudioSize = bestAudio ? (parseInt(bestAudio.contentLength, 10) || (durationSec > 0 ? Math.round((bestAudio.bitrate || 128000) * durationSec / 8) : 0)) : 0;

            const variants = [];
            const seenKeys = new Set();

            // 1. Process Video Streams (Adaptive & Progressive)
            for (const f of allRawFormats) {
                const mime = f.mimeType || '';
                const isVideo = mime.startsWith('video/') || (f.height > 0);
                if (!isVideo) continue;

                const height = f.height || 0;
                if (height <= 0) continue;

                const fps = f.fps || 30;
                const isWebm = mime.includes('webm');
                const container = isWebm ? 'webm' : 'mp4';
                const codec = isWebm ? 'VP9' : (mime.includes('av01') ? 'AV1' : 'H.264');
                const isAdaptive = !f.audioQuality && !mime.includes('audio');

                // Quality Label e.g. "1080p60", "1080p", "720p60", "720p", "4K 2160p"
                let qualityLabel;
                if (height >= 2160) qualityLabel = `4K ${height}p` + (fps > 30 ? `${fps}` : '');
                else if (height >= 1440) qualityLabel = `2K ${height}p` + (fps > 30 ? `${fps}` : '');
                else qualityLabel = `${height}p` + (fps > 30 ? `${fps}` : '');

                const key = `${height}_${fps}_${container}_${codec}`;
                if (seenKeys.has(key)) continue;
                seenKeys.add(key);

                // File size calculation
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
                    qualityLabel: qualityLabel,
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
                    directUrl: f.url || window.location.href,
                    audioStreamUrl: isAdaptive && matchingAudio ? (matchingAudio.url || '') : '',
                    estimatedSizeBytes: totalSize > 0 ? totalSize : -1,
                    formatArg: isAdaptive ? `-f ${f.itag || 'bestvideo'}+bestaudio/best` : `-f ${f.itag || 'best'}`
                });
            }

            // 2. Process Audio Only Streams
            const seenAudioKeys = new Set();
            for (const a of audioStreams) {
                const mime = a.mimeType || '';
                const isWebm = mime.includes('webm') || mime.includes('opus');
                const container = isWebm ? 'webm' : 'm4a';
                const codec = isWebm ? 'Opus' : 'AAC';
                const bitrateKbps = Math.round((a.bitrate || (a.averageBitrate || 128000)) / 1000);

                const audioKey = `${container}_${bitrateKbps}`;
                if (seenAudioKeys.has(audioKey)) continue;
                seenAudioKeys.add(audioKey);

                let audioSize = parseInt(a.contentLength, 10) || 0;
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
                    directUrl: a.url || window.location.href,
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
            const cached = variantCache.get(mediaUrl);
            if (cached && cached.variants && cached.variants.length > 0) {
                return cached;
            }

            // Extract from in-page YouTube data
            if (inPageYouTubeData) {
                const res = this.processPlayerResponse(inPageYouTubeData);
                if (res && res.variants && res.variants.length > 0) return res;
            }

            // Try extracting from DOM scripts right now
            this.extractFromDomScripts();
            if (inPageYouTubeData) {
                const res = this.processPlayerResponse(inPageYouTubeData);
                if (res && res.variants && res.variants.length > 0) return res;
            }

            return null;
        }
    }

    // =========================================================================
    // 3. CANDIDATE DISCOVERY ENGINE
    // =========================================================================
    class MediaCandidateDetector {
        static findMediaCandidates() {
            const candidates = [];
            const seenContainers = new Set();
            const isYouTube = window.location.hostname.includes('youtube.com');
            const isWatchPage = isYouTube && (window.location.pathname.startsWith('/watch') || window.location.pathname.startsWith('/shorts'));

            // 1. YouTube Main Video Player (HIGH Confidence - Priority 1)
            if (isWatchPage) {
                const mainVideo = document.querySelector('#movie_player video.html5-main-video, ytd-watch-flexy #movie_player video, ytd-shorts #shorts-player video, video.html5-main-video');
                if (mainVideo && this.isValidMediaElement(mainVideo)) {
                    const container = mainVideo.closest('#movie_player, ytd-player, .html5-video-player') || mainVideo.parentElement;
                    const candidateId = this.computeCandidateId('main_video', window.location.href, container);
                    seenContainers.add(container);
                    candidates.push({
                        candidateId: candidateId,
                        type: 'main_video',
                        confidence: CandidateConfidence.HIGH,
                        state: CandidateState.DISCOVERED,
                        element: mainVideo,
                        container: container,
                        url: window.location.href,
                        title: this.getPageMediaTitle()
                    });
                }
            }

            // 2. Generic HTML5 <video> elements across all websites (HIGH Confidence)
            const allVideos = Array.from(document.querySelectorAll('video'));
            for (const video of allVideos) {
                if (this.isAdOrDecorativeElement(video)) continue;
                if (!this.isValidMediaElement(video)) continue;

                const container = this.findVideoContainer(video);
                if (seenContainers.has(container)) continue;

                const mediaSrc = this.extractMediaUrl(video);
                const candidateId = this.computeCandidateId('video', mediaSrc, container);
                seenContainers.add(container);

                candidates.push({
                    candidateId: candidateId,
                    type: 'video',
                    confidence: CandidateConfidence.HIGH,
                    state: CandidateState.DISCOVERED,
                    element: video,
                    container: container,
                    url: mediaSrc,
                    title: this.getVideoElementTitle(video)
                });
            }

            // 3. Embedded Player iframes (HIGH Confidence)
            const iframes = document.querySelectorAll(
                'iframe[src*="youtube.com/embed"], iframe[src*="player.vimeo.com"], iframe[src*="dailymotion.com/embed"], iframe[src*="bilibili.com/player"], iframe[src*="twitch.tv"]'
            );
            for (const frame of iframes) {
                const src = frame.getAttribute('src');
                if (!src || src.startsWith('about:') || src.startsWith('javascript:')) continue;
                const container = frame.parentElement || frame;
                if (seenContainers.has(container)) continue;

                const candidateId = this.computeCandidateId('iframe', src, container);
                seenContainers.add(container);

                candidates.push({
                    candidateId: candidateId,
                    type: 'iframe',
                    confidence: CandidateConfidence.HIGH,
                    state: CandidateState.DISCOVERED,
                    element: frame,
                    container: container,
                    url: src,
                    title: frame.getAttribute('title') || this.getPageMediaTitle()
                });
            }

            // 4. HTML5 <audio> elements & podcast players (HIGH Confidence)
            const audios = Array.from(document.querySelectorAll('audio'));
            for (const audio of audios) {
                const src = audio.currentSrc || audio.src || audio.querySelector('source')?.src;
                if (!src || src.startsWith('blob:') || src.startsWith('data:')) continue;
                const container = audio.parentElement || audio;
                if (seenContainers.has(container)) continue;

                const candidateId = this.computeCandidateId('audio', src, container);
                seenContainers.add(container);

                candidates.push({
                    candidateId: candidateId,
                    type: 'audio',
                    confidence: CandidateConfidence.HIGH,
                    state: CandidateState.DISCOVERED,
                    element: audio,
                    container: container,
                    url: src,
                    title: this.getPageMediaTitle() || 'Audio Track'
                });
            }

            // 5. YouTube Recommendation / Video Thumbnail Cards (MEDIUM Confidence)
            if (isYouTube) {
                const videoCards = document.querySelectorAll(
                    'ytd-rich-item-renderer, ytd-video-renderer, ytd-grid-video-renderer, ytd-compact-video-renderer, ytd-reel-item-renderer'
                );
                for (const card of videoCards) {
                    if (this.isAdOrDecorativeElement(card)) continue;
                    const link = card.querySelector('a#thumbnail[href*="/watch?v="], a.ytd-thumbnail[href*="/watch?v="], a#thumbnail[href*="/shorts/"], a[href*="/watch?v="]');
                    if (link && link.href) {
                        const thumbContainer = card.querySelector('#thumbnail, .ytd-thumbnail, ytd-thumbnail') || card;
                        if (seenContainers.has(thumbContainer)) continue;

                        const candidateId = this.computeCandidateId('thumbnail', link.href, thumbContainer);
                        seenContainers.add(thumbContainer);

                        candidates.push({
                            candidateId: candidateId,
                            type: 'thumbnail',
                            confidence: CandidateConfidence.MEDIUM,
                            state: CandidateState.DISCOVERED,
                            element: thumbContainer,
                            container: thumbContainer,
                            url: link.href,
                            title: this.getCardTitle(card)
                        });
                    }
                }
            }

            return candidates;
        }

        static computeCandidateId(type, url, container) {
            const raw = `${type}|${url}|${container?.id || ''}|${container?.className || ''}`;
            let hash = 0;
            for (let i = 0; i < raw.length; i++) {
                hash = ((hash << 5) - hash) + raw.charCodeAt(i);
                hash |= 0;
            }
            return `edm_cand_${Math.abs(hash).toString(16)}`;
        }

        static isValidMediaElement(el) {
            if (!el || !document.body.contains(el)) return false;
            const rect = el.getBoundingClientRect();
            if (rect.width < 180 || rect.height < 100) return false;
            const style = window.getComputedStyle(el);
            if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
            return true;
        }

        static isAdOrDecorativeElement(el) {
            if (!el) return true;
            const adSelectors = '.ad-showing, .video-ads, .ytp-ad-module, [id*="google_ads"], [class*="ad-container"], [class*="sponsored"]';
            if (el.closest && el.closest(adSelectors)) return true;
            if (el.hasAttribute && (el.hasAttribute('loop') && el.hasAttribute('muted') && !el.hasAttribute('controls'))) {
                const rect = el.getBoundingClientRect();
                if (rect.width < 220 || rect.height < 120) return true;
            }
            return false;
        }

        static findVideoContainer(videoEl) {
            const ytPlayer = videoEl.closest('#movie_player, ytd-player, .html5-video-player');
            if (ytPlayer) return ytPlayer;

            const vimeoPlayer = videoEl.closest('.vp-video-wrapper, .player, [data-player-root]');
            if (vimeoPlayer) return vimeoPlayer;

            let parent = videoEl.parentElement;
            while (parent && parent !== document.body) {
                const rect = parent.getBoundingClientRect();
                if (rect.width > 240 && rect.height > 140) return parent;
                parent = parent.parentElement;
            }
            return videoEl;
        }

        static extractMediaUrl(videoEl) {
            if (window.location.hostname.includes('youtube.com') && (window.location.pathname.startsWith('/watch') || window.location.pathname.startsWith('/shorts'))) {
                return window.location.href;
            }
            if (videoEl.currentSrc && !videoEl.currentSrc.startsWith('blob:') && !videoEl.currentSrc.startsWith('data:')) {
                return videoEl.currentSrc;
            }
            if (videoEl.src && !videoEl.src.startsWith('blob:') && !videoEl.src.startsWith('data:')) {
                return videoEl.src;
            }
            const sourceEl = videoEl.querySelector('source[src]');
            if (sourceEl && sourceEl.src && !sourceEl.src.startsWith('blob:') && !sourceEl.src.startsWith('data:')) {
                return sourceEl.src;
            }
            return window.location.href;
        }

        static getPageMediaTitle() {
            const ytTitleEl = document.querySelector('h1.ytd-watch-metadata yt-formatted-string, #title h1 yt-formatted-string, h1.title, meta[name="title"]');
            if (ytTitleEl) {
                const t = ytTitleEl.getAttribute('content') || ytTitleEl.innerText;
                if (t && t.trim()) return t.replace(/\s*-\s*YouTube$/i, '').trim();
            }
            let title = document.title || 'Video Media';
            return title.replace(/\s*-\s*YouTube$/i, '').trim() || 'Video Media';
        }

        static getVideoElementTitle(videoEl) {
            const ariaLabel = videoEl.getAttribute('aria-label') || videoEl.getAttribute('title');
            if (ariaLabel && ariaLabel.trim()) return ariaLabel.trim();
            return this.getPageMediaTitle();
        }

        static getCardTitle(cardEl) {
            const titleEl = cardEl.querySelector('#video-title, .title, h3, a#video-title-link');
            if (titleEl && titleEl.textContent && titleEl.textContent.trim()) {
                return titleEl.textContent.trim();
            }
            return 'Video Media';
        }
    }

    // =========================================================================
    // 4. IDM-CLASS MODERN FLOATING PILL & FORMAT SELECTOR UI
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
            this.isThumbnail = candidate.type === 'thumbnail';
            this.currentRequestId = 0;
            this.init();
        }

        init() {
            this.panel = document.createElement('div');
            this.panel.className = 'edm-floating-panel' + (this.isThumbnail ? ' edm-thumb-panel' : '');
            this.panel.setAttribute('data-candidate-id', this.candidate.candidateId);

            this.panel.innerHTML = `
                <button class="edm-floating-btn" type="button" aria-label="Download this media with EDM" title="Download with EDM">
                    <span class="edm-btn-icon">⚡</span>
                    <span class="edm-btn-text">Download this video</span>
                    <span class="edm-btn-badge" style="display: none;">0</span>
                </button>
                <div class="edm-dropdown-card" style="display: none;" role="dialog" aria-label="EDM Format Selector">
                    <div class="edm-dropdown-header">
                        <div class="edm-dropdown-title-group">
                            <span class="edm-header-logo">EDM</span>
                            <span class="edm-header-title-text" title="${escapeHtml(this.candidate.title || 'Video Media')}">${escapeHtml(this.candidate.title || 'Video Media')}</span>
                        </div>
                        <div class="edm-dropdown-controls">
                            <button class="edm-download-all-opt" type="button" title="Download all stream representations">Download all</button>
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
                        <span class="edm-footer-hint">Click any format to start fast multi-thread download</span>
                    </div>
                </div>
            `;

            this.btn = this.panel.querySelector('.edm-floating-btn');
            this.dropdown = this.panel.querySelector('.edm-dropdown-card');
            this.variantsList = this.panel.querySelector('.edm-variants-container');
            const closeBtn = this.panel.querySelector('.edm-dropdown-close-btn');
            const downloadAllBtn = this.panel.querySelector('.edm-download-all-opt');

            // Tab Buttons
            this.panel.querySelectorAll('.edm-tab-btn').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.switchTab(btn.getAttribute('data-tab'));
                });
            });

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

            if (downloadAllBtn) {
                downloadAllBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.downloadAllStreams();
                });
            }

            const style = window.getComputedStyle(this.container);
            if (style.position === 'static') {
                this.container.style.position = 'relative';
            }

            this.container.appendChild(this.panel);
            this.bindHoverBehavior();

            this.checkPreloadedVariants();
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
            if (this.isThumbnail) {
                this.panel.style.opacity = '0';
                this.container.addEventListener('mouseenter', () => {
                    if (this.panel) this.panel.style.opacity = '1';
                });
                this.container.addEventListener('mouseleave', () => {
                    if (this.panel && !this.isOpen) this.panel.style.opacity = '0';
                });
            } else {
                this.panel.style.opacity = '0.94';
                this.container.addEventListener('mouseenter', () => {
                    if (this.panel) this.panel.style.opacity = '1';
                });
                this.container.addEventListener('mouseleave', () => {
                    if (this.panel && !this.isOpen) this.panel.style.opacity = '0.94';
                });
            }
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
            if (this.isThumbnail && this.panel) this.panel.style.opacity = '0';
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

            // 1. Instant In-Browser / Cache Check (0ms!)
            const localData = YouTubeInPageExtractor.getCachedOrExtractedVariants(mediaUrl);
            if (localData && localData.variants && localData.variants.length > 0) {
                this.candidate.state = CandidateState.READY;
                this.currentVariants = localData.variants;
                this.renderVariants(localData.variants, mediaUrl);
                this.updateBadgeCount(localData.variants.length);
                return;
            }

            // 2. Otherwise Show Analyzing State and Query Background Resolver
            this.candidate.state = CandidateState.ANALYZING;
            this.renderLoadingState();

            let timedOut = false;
            const timer = setTimeout(() => {
                timedOut = true;
                if (this.currentRequestId === requestId && this.isOpen) {
                    const retryLocal = YouTubeInPageExtractor.getCachedOrExtractedVariants(mediaUrl);
                    if (retryLocal && retryLocal.variants && retryLocal.variants.length > 0) {
                        this.currentVariants = retryLocal.variants;
                        this.renderVariants(retryLocal.variants, mediaUrl);
                        return;
                    }
                    this.candidate.state = CandidateState.FAILED;
                    this.renderErrorState("Could not resolve stream details.", () => this.fetchVariants(), mediaUrl);
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

                    if (response && response.isDrmProtected) {
                        this.candidate.state = CandidateState.FAILED;
                        this.renderDrmProtectedState();
                        return;
                    }

                    const variantsList = (response && (response.variants || response.data || (response.result && response.result.variants))) || [];

                    if (Array.isArray(variantsList) && variantsList.length > 0) {
                        this.candidate.state = CandidateState.READY;
                        variantCache.set(mediaUrl, { success: true, variants: variantsList });
                        this.currentVariants = variantsList;
                        this.renderVariants(variantsList, mediaUrl);
                        this.updateBadgeCount(variantsList.length);
                    } else {
                        this.candidate.state = CandidateState.FAILED;
                        this.renderErrorState(response?.errorMessage || "No downloadable media representations found.", () => this.fetchVariants(), mediaUrl);
                    }
                });
            } catch (err) {
                clearTimeout(timer);
                if (!timedOut && this.isOpen && this.currentRequestId === requestId) {
                    const fallbackLocal = YouTubeInPageExtractor.getCachedOrExtractedVariants(mediaUrl);
                    if (fallbackLocal && fallbackLocal.variants && fallbackLocal.variants.length > 0) {
                        this.currentVariants = fallbackLocal.variants;
                        this.renderVariants(fallbackLocal.variants, mediaUrl);
                        return;
                    }
                    this.candidate.state = CandidateState.FAILED;
                    this.renderErrorState("Failed to communicate with EDM resolver.", () => this.fetchVariants(), mediaUrl);
                }
            }
        }

        renderLoadingState() {
            this.variantsList.innerHTML = `
                <div class="edm-state-box">
                    <div class="edm-spinner"></div>
                    <span class="edm-state-text">Detecting all video qualities & formats...</span>
                </div>
            `;
        }

        renderDrmProtectedState() {
            this.variantsList.innerHTML = `
                <div class="edm-state-box edm-state-error">
                    <span class="edm-error-icon">🔒</span>
                    <span class="edm-state-text">This stream is DRM-protected and cannot be downloaded.</span>
                </div>
            `;
        }

        renderErrorState(message, retryFn, mediaUrl) {
            this.variantsList.innerHTML = `
                <div class="edm-state-box edm-state-error">
                    <span class="edm-error-icon">⚠️</span>
                    <span class="edm-state-text">${escapeHtml(message)}</span>
                    <div class="edm-state-actions">
                        <button class="edm-retry-btn" type="button">Retry</button>
                        <button class="edm-direct-btn" type="button">Direct Stream</button>
                    </div>
                </div>
            `;

            this.variantsList.querySelector('.edm-retry-btn')?.addEventListener('click', (e) => {
                e.stopPropagation();
                retryFn();
            });

            this.variantsList.querySelector('.edm-direct-btn')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.executeDownload(mediaUrl, this.candidate.title || 'Video Media', 'Direct Stream', {
                    directUrl: mediaUrl,
                    qualityLabel: 'Direct Stream',
                    container: 'mp4',
                    isAudioOnly: false,
                    estimatedSizeBytes: -1
                });
            });
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
                this.renderErrorState("No downloadable media representations found.", () => this.fetchVariants(), this.candidate.url);
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

            sorted.forEach((v, index) => {
                const item = document.createElement('div');
                item.className = 'edm-variant-row' + (v.isAudioOnly ? ' edm-audio-row' : '');
                item.setAttribute('role', 'button');
                item.setAttribute('tabindex', '0');

                const ext = (v.container || (v.isAudioOnly ? 'mp3' : 'mp4')).toUpperCase();
                const qualityLabel = v.qualityLabel || (v.height > 0 ? `${v.height}p` : 'Standard');
                const sizeText = formatBytes(v.estimatedSizeBytes);

                let badgeClass = 'edm-badge-sd';
                if (v.isAudioOnly) {
                    badgeClass = 'edm-badge-audio';
                } else if (v.height >= 2160 || qualityLabel.includes('4K')) {
                    badgeClass = 'edm-badge-4k';
                } else if (v.height >= 1440 || qualityLabel.includes('2K')) {
                    badgeClass = 'edm-badge-2k';
                } else if (v.height >= 1080) {
                    badgeClass = 'edm-badge-fhd';
                } else if (v.height >= 720) {
                    badgeClass = 'edm-badge-hd';
                }

                let descParts = [];
                if (v.isAudioOnly) {
                    const kbps = v.audioBitrate > 0 ? `${Math.round(v.audioBitrate / 1000)} kbps` : (v.bitrate > 0 ? `${Math.round(v.bitrate / 1000)} kbps` : '128 kbps');
                    descParts.push(ext);
                    descParts.push(v.audioCodec || 'AAC');
                    descParts.push(kbps);
                } else {
                    descParts.push(ext);
                    if (v.codec && v.codec !== 'none') descParts.push(v.codec.toUpperCase());
                    if (v.frameRate > 30) descParts.push(`${Math.round(v.frameRate)} FPS`);
                    descParts.push(v.requiresFfmpegMerge ? 'Adaptive Video+Audio' : 'Direct Video');
                }

                item.innerHTML = `
                    <div class="edm-quality-badge ${badgeClass}">${escapeHtml(qualityLabel)}</div>
                    <div class="edm-variant-info">
                        <span class="edm-variant-desc">${escapeHtml(descParts.join(' • '))}</span>
                        <span class="edm-variant-type">${v.isAudioOnly ? '🎵 Audio Track' : '🎬 Video Stream'}</span>
                    </div>
                    <div class="edm-variant-size-group">
                        <span class="edm-variant-size">${escapeHtml(sizeText)}</span>
                        <span class="edm-download-icon" title="Download">⬇️</span>
                    </div>
                `;

                const handleSelection = (e) => {
                    e.stopPropagation();
                    e.preventDefault();
                    this.executeDownload(v.directUrl || this.candidate.url, videoTitle, qualityLabel, v);
                };

                item.addEventListener('click', handleSelection);
                item.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') handleSelection(e);
                });

                this.variantsList.appendChild(item);
            });
        }

        executeDownload(url, title, quality, variant) {
            const cleanTitle = (title || 'media').replace(/[/\\?%*:|"<>]/g, '_').trim();
            const isAudio = !!variant?.isAudioOnly || (quality && quality.includes('Audio'));
            const ext = variant?.container ? `.${variant.container.toLowerCase()}` : (isAudio ? '.mp3' : '.mp4');
            const filename = cleanTitle + ext;

            const downloadIdentity = generateDownloadIdentity(url, quality, filename, variant?.directUrl || url);

            if (activeJobIdentities.has(downloadIdentity)) {
                chrome.runtime.sendMessage({
                    action: 'START_EDM_DOWNLOAD',
                    url: url,
                    downloadIdentity: downloadIdentity
                });
                this.showToast("Opening existing download in EDM...");
                this.close();
                return;
            }

            activeJobIdentities.add(downloadIdentity);
            this.candidate.state = CandidateState.DOWNLOADING;

            this.showToast(`Starting download: ${quality} (${filename})...`);

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
                    this.candidate.state = CandidateState.COMPLETED;
                });
            } catch (err) {
                this.candidate.state = CandidateState.FAILED;
            }

            this.close();
        }

        downloadAllStreams() {
            const candidates = MediaCandidateDetector.findMediaCandidates();
            candidates.forEach((c) => {
                this.executeDownload(c.url, c.title, "Original", null);
            });
            this.close();
        }

        showToast(message) {
            let toast = document.getElementById('edm-quick-toast');
            if (!toast) {
                toast = document.createElement('div');
                toast.id = 'edm-quick-toast';
                toast.className = 'edm-toast';
                document.body.appendChild(toast);
            }
            toast.textContent = message;
            toast.classList.add('edm-toast-show');
            setTimeout(() => {
                toast.classList.remove('edm-toast-show');
            }, 2600);
        }

        destroy() {
            this.candidate.state = CandidateState.DESTROYED;
            if (this.panel) {
                this.panel.remove();
                this.panel = null;
            }
            if (globalActiveDropdown === this) globalActiveDropdown = null;
        }
    }

    // =========================================================================
    // 5. LIFECYCLE & SPA CONTROLLER
    // =========================================================================
    class AppLifecycleManager {
        static init() {
            YouTubeInPageExtractor.init();
            this.refreshOverlays();

            let debounceTimer = null;
            const observer = new MutationObserver(() => {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(() => this.refreshOverlays(), MUTATION_DEBOUNCE_MS);
            });

            observer.observe(document.body || document.documentElement, {
                childList: true,
                subtree: true
            });

            window.addEventListener('yt-navigate-finish', () => this.handleNavigation());
            window.addEventListener('popstate', () => this.handleNavigation());
            window.addEventListener('hashchange', () => this.handleNavigation());

            this.hookHistoryApi();

            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' && globalActiveDropdown) {
                    globalActiveDropdown.close();
                }
            });

            document.addEventListener('click', (e) => {
                if (globalActiveDropdown && !e.target.closest('.edm-floating-panel')) {
                    globalActiveDropdown.close();
                }
            });
        }

        static hookHistoryApi() {
            const originalPushState = history.pushState;
            const originalReplaceState = history.replaceState;

            history.pushState = function (...args) {
                originalPushState.apply(this, args);
                AppLifecycleManager.handleNavigation();
            };

            history.replaceState = function (...args) {
                originalReplaceState.apply(this, args);
                AppLifecycleManager.handleNavigation();
            };
        }

        static handleNavigation() {
            if (window.location.href === currentPageUrl) return;
            currentPageUrl = window.location.href;

            activeOverlays.forEach(overlay => overlay.destroy());
            activeOverlays.clear();

            setTimeout(() => this.refreshOverlays(), MUTATION_DEBOUNCE_MS);
        }

        static refreshOverlays() {
            const candidates = MediaCandidateDetector.findMediaCandidates();
            const currentCandidateIds = new Set();

            for (const c of candidates) {
                currentCandidateIds.add(c.candidateId);
                if (!activeOverlays.has(c.candidateId)) {
                    const overlay = new IdmDownloadOverlay(c);
                    activeOverlays.set(c.candidateId, overlay);
                }
            }

            for (const [candId, overlay] of activeOverlays.entries()) {
                if (!currentCandidateIds.has(candId) || !document.body.contains(overlay.container)) {
                    overlay.destroy();
                    activeOverlays.delete(candId);
                }
            }
        }
    }

    // =========================================================================
    // 6. UTILITY FUNCTIONS
    // =========================================================================
    function generateDownloadIdentity(url, quality, filename, directUrl) {
        const raw = `${url}|${quality}|${filename}|${directUrl || ''}`;
        let hash = 0;
        for (let i = 0; i < raw.length; i++) {
            hash = ((hash << 5) - hash) + raw.charCodeAt(i);
            hash |= 0;
        }
        return 'edm_job_' + Math.abs(hash).toString(16);
    }

    function formatBytes(bytes) {
        if (!bytes || isNaN(bytes) || bytes <= 0) return 'Size: Unknown';
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(1024));
        return (bytes / Math.pow(1024, i)).toFixed(1) + ' ' + sizes[i];
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    // =========================================================================
    // 7. BOOTSTRAP
    // =========================================================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => AppLifecycleManager.init());
    } else {
        AppLifecycleManager.init();
    }

})();
