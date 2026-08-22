/**
 * EDM Extension - Media Candidate & Types Model
 * Version: 1.0.0
 * Defines the structured internal representation of media candidates, types, and confidence tiers.
 */

export const MediaType = Object.freeze({
    VIDEO: 'VIDEO',
    AUDIO: 'AUDIO',
    MEDIA_CONTAINER: 'MEDIA_CONTAINER',
    IFRAME_PLAYER: 'IFRAME_PLAYER',
    UNKNOWN: 'UNKNOWN'
});

export const ConfidenceLevel = Object.freeze({
    HIGH: 'HIGH',         // Active playback + fully visible in viewport + standard player size
    MEDIUM: 'MEDIUM',     // Visible large player with loaded metadata, currently paused or buffering
    LOW: 'LOW',           // Inactive, off-screen, or unconfirmed source
    AMBIGUOUS: 'AMBIGUOUS' // Multiple identical candidates without clear primary engagement
});

export const SessionState = Object.freeze({
    DISCOVERED: 'DISCOVERED',
    ACTIVE: 'ACTIVE',
    PAUSED: 'PAUSED',
    INACTIVE: 'INACTIVE',
    ENDED: 'ENDED',
    DESTROYED: 'DESTROYED'
});

export class MediaCandidate {
    constructor(options = {}) {
        this.candidateId = options.candidateId || `cand_${Date.now()}_${Math.random().toString(36).substr(2, 7)}`;
        this.elementId = options.elementId || null;
        this.element = options.element || null;
        this.container = options.container || null;
        this.mediaType = options.mediaType || MediaType.UNKNOWN;
        this.sourceUrl = options.sourceUrl || '';
        this.pageUrl = options.pageUrl || (typeof window !== 'undefined' ? window.location.href : '');
        this.tabId = options.tabId || -1;
        this.frameId = options.frameId || 0;
        
        // Geometric & Viewport Signals
        this.dimensions = options.dimensions || { width: 0, height: 0 };
        this.viewportRatio = options.viewportRatio || 0.0;
        this.isVisible = !!options.isVisible;
        this.isOffscreen = !options.isVisible;

        // Playback Signals
        this.duration = options.duration || 0;
        this.currentTime = options.currentTime || 0;
        this.playState = options.playState || 'paused'; // 'playing', 'paused', 'buffering', 'ended'
        this.isPlaying = !!options.isPlaying;
        this.isMuted = !!options.isMuted;
        this.isLooping = !!options.isLooping;
        this.volume = options.volume !== undefined ? options.volume : 1.0;
        this.isAutoplay = !!options.isAutoplay;
        
        // Behavioral & Scoring Signals
        this.activeScore = 0;
        this.confidence = ConfidenceLevel.LOW;
        this.lastActivity = Date.now();
        this.createdAt = Date.now();
        this.detectionSource = options.detectionSource || 'DOM_HTML5'; // 'DOM_HTML5', 'YOUTUBE_PLAYER', 'IFRAME_EMBED'
    }

    toSummary() {
        return {
            candidateId: this.candidateId,
            mediaType: this.mediaType,
            dimensions: `${this.dimensions.width}x${this.dimensions.height}`,
            isPlaying: this.isPlaying,
            isMuted: this.isMuted,
            duration: Math.round(this.duration),
            score: this.activeScore,
            confidence: this.confidence,
            detectionSource: this.detectionSource
        };
    }
}
