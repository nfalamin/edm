/**
 * EDM Extension - Unified Media Detection Engine
 * Version: 1.0.0
 * Discovers media candidates, monitors playback events, computes active scores, and notifies listeners.
 */

import { MediaCandidate, MediaType, SessionState } from './media-candidate.js';
import { ActivePlayerScorer } from './active-player-scorer.js';
import { PlayerSession } from './player-session.js';
import { SpaWatcher } from './spa-watcher.js';
import { Logger } from '../core/logger.js';

export class MediaDetector {
    constructor(options = {}) {
        this.onActiveCandidateChanged = options.onActiveCandidateChanged || null;
        this.candidates = new Map(); // element -> MediaCandidate
        this.sessions = new Map();   // candidateId -> PlayerSession
        this.activeCandidate = null;
        this.mutationObserver = null;
        this.mutationDebounceTimer = null;
        this.intersectionObserver = null;
        this.spaWatcher = null;
        this.isDestroyed = false;

        this.init();
    }

    init() {
        if (typeof document === 'undefined') return;

        // 1. Initialize IntersectionObserver for Viewport Prominence
        if (typeof IntersectionObserver !== 'undefined') {
            this.intersectionObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    const cand = this.candidates.get(entry.target);
                    if (cand) {
                        cand.isVisible = entry.isIntersecting && entry.intersectionRatio >= 0.2;
                        cand.viewportRatio = entry.intersectionRatio;
                        this.recalculateActivePlayer();
                    }
                });
            }, { threshold: [0.0, 0.2, 0.5, 0.8] });
        }

        // 2. Initialize SPA Watcher
        this.spaWatcher = new SpaWatcher((newUrl, source) => {
            this.handleSpaNavigation(newUrl, source);
        });

        // 3. Initialize Debounced MutationObserver
        this.mutationObserver = new MutationObserver((mutations) => {
            this.handleDomMutations(mutations);
        });

        const targetNode = document.body || document.documentElement;
        if (targetNode) {
            this.mutationObserver.observe(targetNode, {
                childList: true,
                subtree: true
            });
        }

        // Initial scan
        this.scanDocumentForMedia();
    }

    scanDocumentForMedia() {
        if (this.isDestroyed || typeof document === 'undefined') return;

        // 1. Discover all HTML5 <video> elements
        const videos = document.querySelectorAll('video');
        videos.forEach(v => this.registerVideoElement(v));

        // 2. Discover all HTML5 <audio> elements
        const audios = document.querySelectorAll('audio');
        audios.forEach(a => this.registerAudioElement(a));

        // 3. Discover embedded iframes (e.g. Vimeo / Dailymotion / Embeds)
        const iframes = document.querySelectorAll('iframe');
        iframes.forEach(f => this.inspectIframeElement(f));

        this.recalculateActivePlayer();
    }

    registerVideoElement(video) {
        if (!video || this.candidates.has(video)) return;

        const rect = video.getBoundingClientRect();
        const candidate = new MediaCandidate({
            element: video,
            container: video.parentElement || video,
            mediaType: MediaType.VIDEO,
            sourceUrl: video.currentSrc || video.src || '',
            dimensions: { width: Math.round(rect.width), height: Math.round(rect.height) },
            duration: video.duration || 0,
            currentTime: video.currentTime || 0,
            isPlaying: !video.paused && video.currentTime > 0 && !video.ended,
            isMuted: video.muted || video.volume === 0,
            isLooping: video.loop,
            volume: video.volume,
            isAutoplay: video.autoplay,
            detectionSource: 'DOM_HTML5_VIDEO'
        });

        const session = new PlayerSession(candidate);
        this.candidates.set(video, candidate);
        this.sessions.set(candidate.candidateId, session);

        if (this.intersectionObserver) {
            this.intersectionObserver.observe(video);
        }

        // Attach event listeners
        const updateCandidateState = (e) => {
            if (this.isDestroyed) return;
            const r = video.getBoundingClientRect();
            candidate.dimensions = { width: Math.round(r.width), height: Math.round(r.height) };
            candidate.duration = video.duration || candidate.duration || 0;
            candidate.currentTime = video.currentTime || 0;
            candidate.isPlaying = !video.paused && !video.ended;
            candidate.isMuted = video.muted || video.volume === 0;
            candidate.volume = video.volume;
            candidate.sourceUrl = video.currentSrc || video.src || candidate.sourceUrl;
            candidate.lastActivity = Date.now();

            if (e.type === 'play' || e.type === 'playing') {
                session.transitionTo(SessionState.ACTIVE, e.type);
            } else if (e.type === 'pause') {
                session.transitionTo(SessionState.PAUSED, e.type);
            } else if (e.type === 'ended') {
                session.transitionTo(SessionState.ENDED, e.type);
            }

            this.recalculateActivePlayer();
        };

        const events = ['play', 'playing', 'pause', 'ended', 'timeupdate', 'volumechange', 'loadedmetadata'];
        events.forEach(evt => video.addEventListener(evt, updateCandidateState));

        candidate._cleanupListeners = () => {
            events.forEach(evt => video.removeEventListener(evt, updateCandidateState));
            if (this.intersectionObserver) {
                this.intersectionObserver.unobserve(video);
            }
            session.destroy("Element untracked");
        };

        Logger.debug(`[MediaDetector] Registered video element #${candidate.candidateId}`);
    }

    registerAudioElement(audio) {
        if (!audio || this.candidates.has(audio)) return;

        const candidate = new MediaCandidate({
            element: audio,
            container: audio.parentElement || audio,
            mediaType: MediaType.AUDIO,
            sourceUrl: audio.currentSrc || audio.src || '',
            duration: audio.duration || 0,
            currentTime: audio.currentTime || 0,
            isPlaying: !audio.paused && !audio.ended,
            isMuted: audio.muted || audio.volume === 0,
            volume: audio.volume,
            detectionSource: 'DOM_HTML5_AUDIO'
        });

        const session = new PlayerSession(candidate);
        this.candidates.set(audio, candidate);
        this.sessions.set(candidate.candidateId, session);

        const updateAudio = () => {
            candidate.isPlaying = !audio.paused && !audio.ended;
            candidate.currentTime = audio.currentTime || 0;
            this.recalculateActivePlayer();
        };

        ['play', 'playing', 'pause', 'ended'].forEach(e => audio.addEventListener(e, updateAudio));
        candidate._cleanupListeners = () => {
            ['play', 'playing', 'pause', 'ended'].forEach(e => audio.removeEventListener(e, updateAudio));
            session.destroy();
        };
    }

    inspectIframeElement(iframe) {
        if (!iframe || this.candidates.has(iframe)) return;

        const src = iframe.src || iframe.getAttribute('data-src') || '';
        if (!src) return;

        // Detect embedded video providers
        const isEmbed = src.includes('youtube.com/embed') ||
                        src.includes('player.vimeo.com') ||
                        src.includes('dailymotion.com/embed') ||
                        src.includes('soundcloud.com/player');

        if (isEmbed) {
            const rect = iframe.getBoundingClientRect();
            const candidate = new MediaCandidate({
                element: iframe,
                container: iframe.parentElement || iframe,
                mediaType: MediaType.IFRAME_PLAYER,
                sourceUrl: src,
                dimensions: { width: Math.round(rect.width), height: Math.round(rect.height) },
                detectionSource: 'IFRAME_EMBED'
            });

            const session = new PlayerSession(candidate);
            this.candidates.set(iframe, candidate);
            this.sessions.set(candidate.candidateId, session);

            if (this.intersectionObserver) {
                this.intersectionObserver.observe(iframe);
            }

            candidate._cleanupListeners = () => {
                if (this.intersectionObserver) this.intersectionObserver.unobserve(iframe);
                session.destroy();
            };
        }
    }

    handleDomMutations(mutations) {
        // Debounce DOM scanning to at most once per 200ms
        if (this.mutationDebounceTimer) clearTimeout(this.mutationDebounceTimer);
        this.mutationDebounceTimer = setTimeout(() => {
            // Check for removed elements
            for (const [elem, candidate] of this.candidates.entries()) {
                if (!document.contains(elem)) {
                    if (candidate._cleanupListeners) candidate._cleanupListeners();
                    this.candidates.delete(elem);
                    this.sessions.delete(candidate.candidateId);
                    Logger.debug(`[MediaDetector] Pruned detached element #${candidate.candidateId}`);
                }
            }
            this.scanDocumentForMedia();
        }, 200);
    }

    handleSpaNavigation(newUrl, source) {
        Logger.info(`[MediaDetector] Purging stale media candidates on SPA navigation to: ${newUrl}`);
        for (const [elem, candidate] of this.candidates.entries()) {
            if (candidate._cleanupListeners) candidate._cleanupListeners();
        }
        this.candidates.clear();
        this.sessions.clear();
        this.activeCandidate = null;

        if (this.onActiveCandidateChanged) {
            this.onActiveCandidateChanged(null);
        }

        // Rescan new page after DOM stabilizes
        setTimeout(() => this.scanDocumentForMedia(), 300);
    }

    recalculateActivePlayer() {
        const candidateList = Array.from(this.candidates.values());
        const { selected, status } = ActivePlayerScorer.selectPrimaryCandidate(candidateList);

        if (selected !== this.activeCandidate) {
            this.activeCandidate = selected;
            Logger.info(`[MediaDetector] Active media candidate changed: ${selected ? selected.candidateId + ' (score: ' + selected.activeScore + ', ' + selected.confidence + ')' : 'None'} [${status}]`);
            if (this.onActiveCandidateChanged) {
                this.onActiveCandidateChanged(this.activeCandidate);
            }
        }
    }

    destroy() {
        this.isDestroyed = true;
        if (this.mutationObserver) this.mutationObserver.disconnect();
        if (this.intersectionObserver) this.intersectionObserver.disconnect();
        for (const [elem, candidate] of this.candidates.entries()) {
            if (candidate._cleanupListeners) candidate._cleanupListeners();
        }
        this.candidates.clear();
        this.sessions.clear();
    }
}
