/**
 * EDM Extension - Download Job Model & State Machine
 * Version: 1.0.0
 * Enforces legal state transitions and provides structured progress telemetry.
 */

import { Logger } from '../core/logger.js';

export const DownloadJobState = Object.freeze({
    DETECTED: 'DETECTED',
    VALIDATING: 'VALIDATING',
    READY: 'READY',
    HANDOFF_PENDING: 'HANDOFF_PENDING',
    HANDED_OFF: 'HANDED_OFF',
    QUEUED: 'QUEUED',
    STARTED: 'STARTED',
    PROGRESS: 'PROGRESS',
    PAUSED: 'PAUSED',
    RESUMED: 'RESUMED',
    COMPLETED: 'COMPLETED',
    FAILED: 'FAILED',
    CANCELLED: 'CANCELLED',
    EXPIRED: 'EXPIRED'
});

export class DownloadJob {
    constructor(candidate) {
        if (!candidate) throw new Error("candidate is required for DownloadJob.");
        this.downloadId = candidate.downloadId;
        this.candidate = candidate;
        this.state = DownloadJobState.DETECTED;
        this.progress = {
            percentage: 0,
            downloadedBytes: 0,
            totalBytes: candidate.size > 0 ? candidate.size : 0,
            speedBytesPerSec: 0,
            etaSeconds: -1,
            isIndeterminate: candidate.size <= 0
        };
        this.error = null;
        this.retryCount = 0;
        this.maxRetries = 2;
        this.createdAt = Date.now();
        this.lastUpdated = Date.now();
        this.listeners = new Set();
    }

    getState() {
        return this.state;
    }

    /**
     * Executes a legal state transition. Returns true if successful, false if illegal.
     */
    transitionTo(newState, reason = "") {
        const oldState = this.state;
        if (oldState === newState) return true;

        // Terminal states: COMPLETED, CANCELLED, EXPIRED cannot transition to active states
        if ([DownloadJobState.COMPLETED, DownloadJobState.CANCELLED, DownloadJobState.EXPIRED].includes(oldState)) {
            Logger.warn(`[DownloadJob] Attempted illegal transition from terminal state ${oldState} to ${newState}`);
            return false;
        }

        const isLegal = (from, to) => {
            switch (from) {
                case DownloadJobState.DETECTED:
                    return [DownloadJobState.VALIDATING, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.VALIDATING:
                    return [DownloadJobState.READY, DownloadJobState.FAILED, DownloadJobState.EXPIRED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.READY:
                    return [DownloadJobState.HANDOFF_PENDING, DownloadJobState.CANCELLED, DownloadJobState.EXPIRED].includes(to);
                case DownloadJobState.HANDOFF_PENDING:
                    return [DownloadJobState.HANDED_OFF, DownloadJobState.QUEUED, DownloadJobState.FAILED, DownloadJobState.CANCELLED, DownloadJobState.COMPLETED].includes(to);
                case DownloadJobState.HANDED_OFF:
                    return [DownloadJobState.QUEUED, DownloadJobState.STARTED, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.QUEUED:
                    return [DownloadJobState.STARTED, DownloadJobState.PAUSED, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.STARTED:
                    return [DownloadJobState.PROGRESS, DownloadJobState.PAUSED, DownloadJobState.COMPLETED, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.PROGRESS:
                    return [DownloadJobState.PROGRESS, DownloadJobState.PAUSED, DownloadJobState.COMPLETED, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.PAUSED:
                    return [DownloadJobState.RESUMED, DownloadJobState.STARTED, DownloadJobState.CANCELLED, DownloadJobState.FAILED].includes(to);
                case DownloadJobState.RESUMED:
                    return [DownloadJobState.PROGRESS, DownloadJobState.PAUSED, DownloadJobState.COMPLETED, DownloadJobState.FAILED, DownloadJobState.CANCELLED].includes(to);
                case DownloadJobState.FAILED:
                    return [DownloadJobState.VALIDATING, DownloadJobState.HANDOFF_PENDING, DownloadJobState.CANCELLED].includes(to); // Retry paths
                default:
                    return false;
            }
        };

        if (!isLegal(oldState, newState)) {
            Logger.warn(`[DownloadJob] Illegal transition: ${oldState} -> ${newState} (${reason})`);
            return false;
        }

        this.state = newState;
        this.lastUpdated = Date.now();
        Logger.debug(`[DownloadJob] ${this.downloadId} state: ${oldState} -> ${newState} ${reason ? '(' + reason + ')' : ''}`);
        this.notifyStateChanged(oldState, newState, reason);
        return true;
    }

    updateProgress(telemetry) {
        if (![DownloadJobState.STARTED, DownloadJobState.PROGRESS, DownloadJobState.RESUMED].includes(this.state)) {
            return;
        }

        if (this.state !== DownloadJobState.PROGRESS) {
            this.transitionTo(DownloadJobState.PROGRESS);
        }

        this.progress.downloadedBytes = Number(telemetry.downloadedBytes) || this.progress.downloadedBytes;
        this.progress.totalBytes = Number(telemetry.totalBytes) || this.progress.totalBytes;
        this.progress.percentage = this.progress.totalBytes > 0 
            ? Math.min(100, Math.round((this.progress.downloadedBytes / this.progress.totalBytes) * 100))
            : (Number(telemetry.percentage) || 0);
        this.progress.speedBytesPerSec = Number(telemetry.speedBytesPerSec) || 0;
        this.progress.etaSeconds = Number(telemetry.etaSeconds) || -1;
        this.progress.isIndeterminate = this.progress.totalBytes <= 0;
        this.lastUpdated = Date.now();

        this.notifyProgress();
    }

    subscribe(callback) {
        if (typeof callback === 'function') this.listeners.add(callback);
    }

    unsubscribe(callback) {
        this.listeners.delete(callback);
    }

    notifyStateChanged(oldState, newState, reason) {
        for (const cb of this.listeners) {
            try {
                cb({ type: 'STATE_CHANGED', downloadId: this.downloadId, oldState, newState, reason, job: this });
            } catch (e) {
                Logger.warn("[DownloadJob] Error in listener:", e);
            }
        }
    }

    notifyProgress() {
        for (const cb of this.listeners) {
            try {
                cb({ type: 'PROGRESS', downloadId: this.downloadId, progress: this.progress, job: this });
            } catch (e) {
                Logger.warn("[DownloadJob] Error in progress listener:", e);
            }
        }
    }
}
