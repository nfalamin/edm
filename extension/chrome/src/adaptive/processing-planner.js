/**
 * EDM Extension - Media Processing Planner & Codec Compatibility Engine
 * Version: 1.0.0
 * Evaluates video/audio stream compatibility, selects optimal processing profile (DIRECT, MERGE, REMUX),
 * and avoids unnecessary transcoding.
 */

import { Logger } from '../core/logger.js';

export const ProcessingProfile = Object.freeze({
    DIRECT: 'DIRECT',                         // Progressive MP4/WebM -> zero processing required
    REMUX: 'REMUX',                           // HLS TS segments -> Remux into single MP4 container without re-encoding
    MERGE: 'MERGE',                           // DASH video-only (AVC/VP9) + audio (AAC/Opus) -> ffmpeg multiplex
    TRANSCODE_REQUIRED: 'TRANSCODE_REQUIRED', // Incompatible codecs or exotic containers requiring re-encoding
    UNSUPPORTED: 'UNSUPPORTED'                // DRM or unparseable codecs
});

export const ProcessingJobState = Object.freeze({
    CREATED: 'CREATED',
    QUEUED: 'QUEUED',
    PROCESSING: 'PROCESSING',
    MERGING: 'MERGING',
    FINALIZING: 'FINALIZING',
    COMPLETED: 'COMPLETED',
    FAILED: 'FAILED',
    CANCELLED: 'CANCELLED'
});

export class ProcessingPlanner {
    /**
     * Evaluates video representation, audio representation, and container to determine optimal processing profile.
     * @param {Object} videoRep 
     * @param {Object} audioRep 
     * @param {string} targetContainer e.g. "mp4", "mkv"
     * @returns {string} ProcessingProfile
     */
    static evaluateProcessingProfile(videoRep, audioRep = null, targetContainer = 'mp4') {
        if (!videoRep) return ProcessingProfile.UNSUPPORTED;

        // 1. Direct Progressive Stream
        if (!videoRep.isVideoOnly && !audioRep && videoRep.mediaType !== 'ADAPTIVE') {
            return ProcessingProfile.DIRECT;
        }

        // 2. HLS Segmented Stream -> REMUX (Lossless concatenation without transcoding)
        if (videoRep.container === 'm3u8' && !audioRep) {
            return ProcessingProfile.REMUX;
        }

        // 3. DASH Video + Audio Pairing -> Check codec compatibility for lossless MERGE
        if (videoRep.isVideoOnly && audioRep) {
            const vCodec = (videoRep.codec || '').toLowerCase();
            const aCodec = (audioRep.audioCodec || audioRep.codec || '').toLowerCase();

            // AVC (H.264) + AAC in MP4 container -> Pure Lossless Multiplex (MERGE)
            if ((vCodec.includes('avc') || vCodec.includes('h264') || vCodec === '') &&
                (aCodec.includes('mp4a') || aCodec.includes('aac') || aCodec === '')) {
                return ProcessingProfile.MERGE;
            }

            // VP9 + Opus in WebM container -> Pure Lossless Multiplex (MERGE)
            if ((vCodec.includes('vp9') || vCodec.includes('vp09')) &&
                (aCodec.includes('opus') || aCodec.includes('vorbis'))) {
                return ProcessingProfile.MERGE;
            }

            // AV1 + AAC/Opus in MP4/MKV -> Lossless Multiplex (MERGE)
            if (vCodec.includes('av01') || vCodec.includes('av1')) {
                return ProcessingProfile.MERGE;
            }

            // Mixed container cross-multiplex (e.g. VP9 in MP4) -> REMUX/MERGE supported by ffmpeg
            return ProcessingProfile.MERGE;
        }

        return ProcessingProfile.MERGE;
    }
}

export class MediaProcessingJob {
    constructor(options = {}) {
        this.processingId = options.processingId || `proc_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.downloadId = options.downloadId || '';
        this.videoRepresentationId = options.videoRepresentationId || '';
        this.audioRepresentationId = options.audioRepresentationId || '';
        this.profile = options.profile || ProcessingProfile.DIRECT;
        this.state = ProcessingJobState.CREATED;
        this.outputFilename = options.outputFilename || 'output.mp4';
        this.createdAt = Date.now();
        this.lastUpdated = Date.now();
    }

    transitionTo(newState, reason = "") {
        const oldState = this.state;
        if (oldState === newState) return true;

        if ([ProcessingJobState.COMPLETED, ProcessingJobState.CANCELLED].includes(oldState)) {
            Logger.warn(`[MediaProcessingJob] Illegal transition from terminal state ${oldState} to ${newState}`);
            return false;
        }

        this.state = newState;
        this.lastUpdated = Date.now();
        Logger.debug(`[MediaProcessingJob] ${this.processingId} state: ${oldState} -> ${newState} ${reason ? '(' + reason + ')' : ''}`);
        return true;
    }
}
