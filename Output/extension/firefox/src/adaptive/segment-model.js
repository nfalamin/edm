/**
 * EDM Extension - Segment & Download Plan Models
 * Version: 1.0.0
 * Represents atomic segments, deterministic sequence ordering, initialization headers, and download plans.
 */

export const SegmentStatus = Object.freeze({
    DISCOVERED: 'DISCOVERED',
    QUEUED: 'QUEUED',
    DOWNLOADING: 'DOWNLOADING',
    COMPLETED: 'COMPLETED',
    FAILED: 'FAILED',
    EXPIRED: 'EXPIRED',
    CANCELLED: 'CANCELLED'
});

export class Segment {
    constructor(options = {}) {
        this.segmentId = options.segmentId || `seg_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.representationId = options.representationId || '';
        this.sequenceNumber = Number(options.sequenceNumber) || 0;
        this.url = options.url || '';
        this.startTime = Number(options.startTime) || 0;
        this.duration = Number(options.duration) || 0;
        this.byteRange = options.byteRange || null; // { offset: number, length: number }
        this.isInitialization = !!options.isInitialization;
        this.discoveredAt = options.discoveredAt || Date.now();
        this.expiresAt = Number(options.expiresAt) || 0;
        this.status = options.status || SegmentStatus.DISCOVERED;
        this.retryCount = 0;
        this.maxRetries = 3;
    }

    isExpired() {
        return this.expiresAt > 0 && Date.now() >= this.expiresAt;
    }
}

export class DownloadPlan {
    constructor(options = {}) {
        this.planId = options.planId || `plan_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.mediaSessionId = options.mediaSessionId || '';
        this.videoRepresentation = options.videoRepresentation || null;
        this.audioRepresentation = options.audioRepresentation || null;
        this.isLive = !!options.isLive;
        this.duration = Number(options.duration) || 0;
        this.processingProfile = options.processingProfile || 'DIRECT'; // 'DIRECT', 'MERGE', 'REMUX', 'TRANSCODE_REQUIRED'
        
        // Segments organized by deterministic sequenceNumber
        this.videoInitSegment = options.videoInitSegment || null;
        this.audioInitSegment = options.audioInitSegment || null;
        this.videoSegments = options.videoSegments || []; // Array<Segment>
        this.audioSegments = options.audioSegments || []; // Array<Segment>
        
        this.maxConcurrentSegments = Number(options.maxConcurrentSegments) || 4;
        this.createdAt = Date.now();
    }

    getTotalSegmentCount() {
        let count = 0;
        if (this.videoInitSegment) count++;
        if (this.audioInitSegment) count++;
        count += this.videoSegments.length;
        count += this.audioSegments.length;
        return count;
    }

    getCompletedSegmentCount() {
        let count = 0;
        if (this.videoInitSegment && this.videoInitSegment.status === SegmentStatus.COMPLETED) count++;
        if (this.audioInitSegment && this.audioInitSegment.status === SegmentStatus.COMPLETED) count++;
        count += this.videoSegments.filter(s => s.status === SegmentStatus.COMPLETED).length;
        count += this.audioSegments.filter(s => s.status === SegmentStatus.COMPLETED).length;
        return count;
    }

    isComplete() {
        const total = this.getTotalSegmentCount();
        return total > 0 && this.getCompletedSegmentCount() === total;
    }
}
