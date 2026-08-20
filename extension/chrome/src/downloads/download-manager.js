/**
 * EDM Extension - Production Download Pipeline Manager
 * Version: 1.0.0
 * Connects candidate validation, policy evaluation, NativeHost handoff, and job lifecycle.
 */

import { DownloadCandidate } from './download-candidate.js';
import { DownloadJob, DownloadJobState } from './download-job.js';
import { DownloadPolicy, PolicyVerdict } from './download-policy.js';
import { NativeProtocolV1 } from '../native/protocol-v1.js';
import { NativeConnectionManager } from '../native/connection-manager.js';
import { ErrorCodes, EdmError } from '../core/errors.js';
import { Logger } from '../core/logger.js';

export class DownloadPipelineManager {
    constructor(options = {}) {
        this.nativeConnection = options.nativeConnection || new NativeConnectionManager();
        this.activeJobs = new Map(); // downloadId -> DownloadJob
        this.dedupWindow = new Map(); // hash -> timestamp (3000ms sliding TTL)
        this.emergencyBrowserFallbackHandler = options.emergencyBrowserFallbackHandler || null;
    }

    /**
     * Initiates a download job from a candidate payload.
     * @param {Object} rawCandidate 
     * @param {Object} contextOptions { isAltKeyPressed, autoCaptureEnabled }
     * @returns {Promise<Object>}
     */
    async initiateDownload(rawCandidate, contextOptions = {}) {
        const candidate = new DownloadCandidate(rawCandidate);
        const downloadId = candidate.downloadId;

        Logger.info(`[DownloadPipeline] Initiating download job #${downloadId} for: ${candidate.filename}`);

        // 1. Check Duplicate Request (3000ms window)
        const dedupKey = `${candidate.url}_${candidate.formatId || ''}_${candidate.quality || ''}`;
        const lastSubmitted = this.dedupWindow.get(dedupKey);
        if (lastSubmitted && (Date.now() - lastSubmitted) < 3000) {
            Logger.warn(`[DownloadPipeline] Duplicate download submission suppressed for: ${candidate.filename}`);
            return {
                success: true,
                status: "DUPLICATE_SUPPRESSED",
                downloadId,
                message: "A download for this exact stream is already in progress."
            };
        }
        this.dedupWindow.set(dedupKey, Date.now());

        // 2. Validate Candidate & URL Expiration
        if (!candidate.isValid()) {
            if (candidate.isExpired()) {
                Logger.warn(`[DownloadPipeline] Rejected download: Candidate stream URL is expired.`);
                return { success: false, errorCode: ErrorCodes.REQUEST_CANCELLED, error: "Stream URL has expired. Please refresh the page.", downloadId };
            }
            Logger.warn(`[DownloadPipeline] Rejected download: Candidate URL is invalid or malformed.`);
            return { success: false, errorCode: ErrorCodes.INVALID_MEDIA_URL, error: "Invalid media URL.", downloadId };
        }

        const job = new DownloadJob(candidate);
        this.activeJobs.set(downloadId, job);

        job.transitionTo(DownloadJobState.VALIDATING, "Validating policy");

        // 3. Evaluate Download Policy
        const verdict = DownloadPolicy.evaluate(candidate, contextOptions);

        if (verdict === PolicyVerdict.REJECT) {
            job.transitionTo(DownloadJobState.FAILED, "Rejected by policy");
            return { success: false, errorCode: ErrorCodes.INVALID_PAYLOAD, error: "Download rejected by security policy.", downloadId };
        }

        if (verdict === PolicyVerdict.BROWSER_FALLBACK) {
            job.transitionTo(DownloadJobState.HANDOFF_PENDING, "Routing to browser fallback");
            return this.executeBrowserFallback(job, "Policy requested browser fallback");
        }

        job.transitionTo(DownloadJobState.READY, "Policy approved for EDM Native Handoff");

        // 4. Dispatch to EDM Native Messaging Host
        return this.handoffToNativeHost(job);
    }

    /**
     * Dispatches the job to EDM.NativeHost.exe.
     */
    async handoffToNativeHost(job) {
        job.transitionTo(DownloadJobState.HANDOFF_PENDING, "Sending request to NativeHost");
        const candidate = job.candidate;

        const requestEnvelope = NativeProtocolV1.createDownloadRequest({
            downloadIdentity: candidate.downloadId,
            correlationId: candidate.downloadId,
            url: candidate.url,
            videoUrl: candidate.videoUrl,
            audioUrl: candidate.audioUrl,
            manifestUrl: candidate.manifestUrl,
            filename: candidate.filename,
            quality: candidate.quality,
            container: candidate.container,
            videoCodec: candidate.videoCodec,
            audioCodec: candidate.audioCodec,
            width: candidate.width,
            height: candidate.height,
            fps: candidate.fps,
            estimatedSizeBytes: candidate.size,
            requiresFfmpegMerge: candidate.requiresMerge,
            isAudioOnly: candidate.mediaType === 'AUDIO',
            source: candidate.sourceType
        });

        try {
            const response = await this.nativeConnection.sendNativeRequest(requestEnvelope, 6000);
            job.transitionTo(DownloadJobState.HANDED_OFF, "NativeHost accepted request");
            job.transitionTo(DownloadJobState.QUEUED, "Queued in EDM Desktop");

            return {
                success: true,
                status: "HANDED_OFF_TO_EDM",
                downloadId: job.downloadId,
                response
            };
        } catch (err) {
            Logger.warn(`[DownloadPipeline] NativeHost handoff failed for #${job.downloadId}:`, err.message);

            // Attempt Emergency Browser Fallback if applicable
            if (this.emergencyBrowserFallbackHandler && !candidate.requiresMerge) {
                Logger.info(`[DownloadPipeline] Attempting emergency browser fallback for #${job.downloadId}`);
                return this.executeBrowserFallback(job, err.message);
            }

            job.transitionTo(DownloadJobState.FAILED, err.message);
            return {
                success: false,
                errorCode: ErrorCodes.NATIVE_HOST_UNAVAILABLE,
                error: `Could not hand off to EDM: ${err.message}`,
                downloadId: job.downloadId
            };
        }
    }

    /**
     * Executes browser native fallback download.
     */
    async executeBrowserFallback(job, reason) {
        if (!this.emergencyBrowserFallbackHandler) {
            job.transitionTo(DownloadJobState.FAILED, "No fallback handler configured");
            return { success: false, error: "EDM is unavailable and browser fallback is not configured.", downloadId: job.downloadId };
        }

        try {
            await this.emergencyBrowserFallbackHandler(job.candidate);
            job.transitionTo(DownloadJobState.COMPLETED, `Browser fallback completed (${reason})`);
            return { success: true, status: "BROWSER_FALLBACK_EXECUTED", downloadId: job.downloadId };
        } catch (err) {
            job.transitionTo(DownloadJobState.FAILED, `Fallback error: ${err.message}`);
            return { success: false, error: `Browser fallback failed: ${err.message}`, downloadId: job.downloadId };
        }
    }

    /**
     * Commands: Pause, Resume, Cancel
     */
    async pauseDownload(downloadId) {
        const job = this.activeJobs.get(downloadId);
        if (!job) return { success: false, error: "Download job not found." };

        if (!job.transitionTo(DownloadJobState.PAUSED, "User pause command")) {
            return { success: false, error: `Cannot pause job in state ${job.getState()}` };
        }

        return { success: true, status: "PAUSED", downloadId };
    }

    async resumeDownload(downloadId) {
        const job = this.activeJobs.get(downloadId);
        if (!job) return { success: false, error: "Download job not found." };

        if (!job.transitionTo(DownloadJobState.RESUMED, "User resume command")) {
            return { success: false, error: `Cannot resume job in state ${job.getState()}` };
        }

        return { success: true, status: "RESUMED", downloadId };
    }

    async cancelDownload(downloadId, reason = "User cancellation") {
        const job = this.activeJobs.get(downloadId);
        if (!job) return { success: false, error: "Download job not found." };

        job.transitionTo(DownloadJobState.CANCELLED, reason);
        return { success: true, status: "CANCELLED", downloadId };
    }
}
