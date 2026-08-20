/**
 * EDM Extension - Media Representation & Discovery Contracts
 * Version: 1.0.0
 * Strict data structures for discovered video, audio, muxed, and adaptive media streams.
 */

export const Downloadability = Object.freeze({
    DIRECT: 'DIRECT',                         // Standalone MP4/WebM with audio + video
    REQUIRES_MERGE: 'REQUIRES_MERGE',         // Video-only stream requiring audio merge via ffmpeg in EDM Desktop
    REQUIRES_PROCESSING: 'REQUIRES_PROCESSING',// Segmented HLS/DASH stream requiring stream stitcher
    UNAVAILABLE: 'UNAVAILABLE',               // Encrypted/DRM protected or expired stream
    UNKNOWN: 'UNKNOWN'                        // Unclassified stream format
});

export const DiscoveryStatus = Object.freeze({
    NOT_STARTED: 'NOT_STARTED',
    DISCOVERING: 'DISCOVERING',
    PARTIAL: 'PARTIAL',
    COMPLETE: 'COMPLETE',
    FAILED: 'FAILED',
    EXPIRED: 'EXPIRED'
});

export class MediaRepresentation {
    constructor(options = {}) {
        this.formatId = options.formatId || `fmt_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.mediaType = options.mediaType || 'VIDEO'; // 'VIDEO', 'AUDIO', 'MUXED', 'ADAPTIVE'
        this.container = options.container || 'mp4';   // 'mp4', 'webm', 'm3u8', 'mpd', 'm4a', 'opus'
        this.mimeType = options.mimeType || '';
        this.codec = options.codec || '';
        this.width = Number(options.width) || 0;
        this.height = Number(options.height) || 0;
        this.fps = Number(options.fps) || 0;
        this.bitrate = Number(options.bitrate) || 0;
        
        // Audio specific metadata
        this.audioCodec = options.audioCodec || '';
        this.audioBitrate = Number(options.audioBitrate) || 0;
        this.sampleRate = Number(options.sampleRate) || 0;
        this.channels = Number(options.channels) || 2;
        
        // Sizing & duration
        this.bitrate = Number(options.bitrate) || Number(options.audioBitrate) || 0;
        this.estimatedSizeBytes = Number(options.estimatedSizeBytes) || -1;
        this.duration = Number(options.duration) || 0;
        
        // Stream Classification
        this.isVideoOnly = !!options.isVideoOnly;
        this.isAudioOnly = !!options.isAudioOnly;
        this.isMuxed = !!options.isMuxed || (!options.isVideoOnly && !options.isAudioOnly && this.mediaType === 'VIDEO');
        this.isAdaptive = !!options.isAdaptive;
        this.downloadability = options.downloadability || (this.isVideoOnly ? Downloadability.REQUIRES_MERGE : Downloadability.DIRECT);
        
        // URL & Origin
        this.url = options.url || '';
        this.manifestUrl = options.manifestUrl || '';
        this.source = options.source || 'METADATA_DISCOVERY';
        this.qualityLabel = options.qualityLabel || (this.height > 0 ? (
            this.height >= 4320 ? '4320p (8K)' :
            this.height >= 2160 ? (this.fps >= 50 ? `2160p${Math.round(this.fps)} (4K UHD)` : '2160p (4K UHD)') :
            this.height >= 1440 ? (this.fps >= 50 ? `1440p${Math.round(this.fps)} (2K QHD)` : '1440p (2K QHD)') :
            this.height >= 1080 ? (this.fps >= 50 ? `1080p${Math.round(this.fps)} (Full HD)` : '1080p (Full HD)') :
            this.height >= 720 ? (this.fps >= 50 ? `720p${Math.round(this.fps)} (HD)` : '720p (HD)') :
            this.height >= 480 ? '480p' :
            this.height >= 360 ? '360p' :
            this.height >= 240 ? '240p' :
            this.height >= 144 ? '144p' : `${this.height}p`
        ) : (this.isAudioOnly ? 'Audio Only' : ''));
        
        // Expiration & Lifecycle
        this.discoveredAt = options.discoveredAt || Date.now();
        this.expiresAt = options.expiresAt || 0; // 0 = unknown / permanent
        this.confidence = options.confidence || 'HIGH';
    }

    isExpired() {
        if (!this.expiresAt || this.expiresAt <= 0) return false;
        return Date.now() >= this.expiresAt;
    }
}
