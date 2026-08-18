/**
 * EDM Extension - Central Adaptive Media Processing Pipeline
 * Version: 1.0.0
 * Coordinates manifest analysis, segment download planning, bounded concurrency, and recovery.
 */

import { DownloadPlan, Segment, SegmentStatus } from './segment-model.js';
import { HlsAdapter } from './hls-adapter.js';
import { DashAdapter } from './dash-adapter.js';
import { ProcessingPlanner, ProcessingProfile } from './processing-planner.js';
import { Logger } from '../core/logger.js';

export class AdaptivePipelineManager {
    constructor(options = {}) {
        this.maxConcurrentSegments = options.maxConcurrentSegments || 4;
        this.activePlans = new Map(); // planId -> DownloadPlan
    }

    /**
     * Constructs a deterministic DownloadPlan from manifest data.
     * @param {Object} options { manifestType, manifestUrl, videoRep, audioRep, rawManifestText }
     * @returns {DownloadPlan}
     */
    createDownloadPlan(options = {}) {
        const { manifestType, manifestUrl, videoRep, audioRep, rawManifestText } = options;
        Logger.info(`[AdaptivePipeline] Building download plan for ${manifestType || 'ADAPTIVE'} stream: ${manifestUrl || 'direct'}`);

        let videoSegments = [];
        let videoInitSegment = null;
        let isLive = false;
        let duration = videoRep?.duration || 0;

        if (manifestType === 'HLS' && rawManifestText) {
            const parsed = HlsAdapter.parseMediaPlaylist(rawManifestText, manifestUrl, videoRep?.formatId);
            videoSegments = parsed.segments;
            videoInitSegment = parsed.initSegment;
            isLive = parsed.isLive;
            duration = parsed.totalDuration || duration;
        }

        const profile = ProcessingPlanner.evaluateProcessingProfile(videoRep, audioRep);

        const plan = new DownloadPlan({
            mediaSessionId: options.mediaSessionId || '',
            videoRepresentation: videoRep,
            audioRepresentation: audioRep,
            isLive,
            duration,
            processingProfile: profile,
            videoInitSegment,
            videoSegments,
            maxConcurrentSegments: this.maxConcurrentSegments
        });

        this.activePlans.set(plan.planId, plan);
        Logger.info(`[AdaptivePipeline] Created plan #${plan.planId} with ${plan.getTotalSegmentCount()} total segments. Profile: ${profile}`);
        return plan;
    }

    /**
     * Marks a segment complete and checks overall plan completion.
     */
    markSegmentComplete(planId, sequenceNumber, isInit = false) {
        const plan = this.activePlans.get(planId);
        if (!plan) return false;

        if (isInit && plan.videoInitSegment) {
            plan.videoInitSegment.status = SegmentStatus.COMPLETED;
            return true;
        }

        const seg = plan.videoSegments.find(s => s.sequenceNumber === sequenceNumber);
        if (seg) {
            seg.status = SegmentStatus.COMPLETED;
            return true;
        }
        return false;
    }

    /**
     * Handles segment expiration by triggering manifest re-fetch.
     */
    handleSegmentExpiration(planId, sequenceNumber, newSegmentUrl) {
        const plan = this.activePlans.get(planId);
        if (!plan) return false;

        const seg = plan.videoSegments.find(s => s.sequenceNumber === sequenceNumber);
        if (seg) {
            seg.url = newSegmentUrl;
            seg.status = SegmentStatus.QUEUED;
            seg.discoveredAt = Date.now();
            Logger.info(`[AdaptivePipeline] Refreshed expired segment #${sequenceNumber} for plan #${planId}`);
            return true;
        }
        return false;
    }
}
