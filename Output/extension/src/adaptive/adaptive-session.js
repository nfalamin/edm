/**
 * EDM Extension - Adaptive Media Session
 * Version: 1.0.0
 * Tracks adaptive manifest state, live vs VOD classification, and expiration intervals.
 */

import { DiscoveryStatus } from '../media/representation-model.js';

export class AdaptiveMediaSession {
    constructor(options = {}) {
        this.sessionId = options.sessionId || `adp_sess_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.mediaSessionId = options.mediaSessionId || '';
        this.manifestType = options.manifestType || 'HLS'; // 'HLS', 'DASH', 'UNKNOWN'
        this.manifestUrl = options.manifestUrl || '';
        this.duration = Number(options.duration) || 0;
        this.isLive = !!options.isLive;
        this.isDrmProtected = !!options.isDrmProtected;
        this.videoRepresentations = options.videoRepresentations || [];
        this.audioRepresentations = options.audioRepresentations || [];
        this.currentPlayback = options.currentPlayback || null;
        this.maximumAvailable = options.maximumAvailable || null;
        this.lastRefresh = Date.now();
        this.expiresAt = Number(options.expiresAt) || 0;
        this.discoveryStatus = options.discoveryStatus || DiscoveryStatus.DISCOVERING;
    }

    isExpired() {
        return this.expiresAt > 0 && Date.now() >= this.expiresAt;
    }

    requiresRefresh() {
        if (this.isExpired()) return true;
        if (this.isLive && (Date.now() - this.lastRefresh) > 10000) return true; // 10s live sliding window
        return false;
    }
}
