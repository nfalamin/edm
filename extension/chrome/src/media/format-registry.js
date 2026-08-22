/**
 * EDM Extension - Central Format Registry
 * Version: 1.0.0
 * Organizes discovered formats by media session identity.
 * Explicitly isolates currentPlayback from availableRepresentations and computes maximumAvailableQuality.
 */

import { DiscoveryStatus } from './representation-model.js';
import { QualityNormalizer } from './quality-normalizer.js';
import { FormatValidator } from './format-validator.js';
import { Logger } from '../core/logger.js';

export class FormatRegistry {
    constructor() {
        this.sessionRegistries = new Map(); // sessionId / mediaKey -> SessionFormatRecord
    }

    /**
     * Initializes or gets the format record for a media session.
     */
    getOrCreateSessionRecord(mediaKey) {
        if (!mediaKey) throw new Error("mediaKey is required for format registration.");

        if (!this.sessionRegistries.has(mediaKey)) {
            this.sessionRegistries.set(mediaKey, {
                mediaKey,
                currentPlayback: null, // e.g. { height: 720, qualityLabel: "720p" }
                videoRepresentations: [],
                audioRepresentations: [],
                muxedRepresentations: [],
                maximumAvailable: null,
                discoveryStatus: DiscoveryStatus.NOT_STARTED,
                lastUpdated: Date.now()
            });
        }
        return this.sessionRegistries.get(mediaKey);
    }

    /**
     * Records the active playback quality (without restricting the available formats).
     */
    setCurrentPlayback(mediaKey, height, width = 0, fps = 0) {
        const record = this.getOrCreateSessionRecord(mediaKey);
        record.currentPlayback = {
            height: Number(height) || 0,
            width: Number(width) || 0,
            fps: Number(fps) || 0,
            qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps)
        };
        record.lastUpdated = Date.now();
        Logger.info(`[FormatRegistry] Current playback set for '${mediaKey}': ${record.currentPlayback.qualityLabel}`);
    }

    /**
     * Registers a batch of discovered representations for a media session with deduplication and validation.
     */
    registerRepresentations(mediaKey, newRepresentations = []) {
        const record = this.getOrCreateSessionRecord(mediaKey);
        record.discoveryStatus = DiscoveryStatus.DISCOVERING;

        const validReps = newRepresentations.filter(r => FormatValidator.validateRepresentation(r));

        validReps.forEach(rep => {
            if (rep.isAudioOnly || rep.mediaType === 'AUDIO') {
                this.insertDeduplicated(record.audioRepresentations, rep);
            } else if (rep.isMuxed) {
                this.insertDeduplicated(record.muxedRepresentations, rep);
                this.insertDeduplicated(record.videoRepresentations, rep);
            } else {
                this.insertDeduplicated(record.videoRepresentations, rep);
            }
        });

        // Compute maximum available quality strictly from discovered representations
        const allVideos = [...record.videoRepresentations, ...record.muxedRepresentations];
        record.maximumAvailable = QualityNormalizer.calculateMaximumAvailableQuality(allVideos);
        record.discoveryStatus = allVideos.length > 0 ? DiscoveryStatus.COMPLETE : DiscoveryStatus.FAILED;
        record.lastUpdated = Date.now();

        Logger.info(`[FormatRegistry] Registered ${validReps.length} representations for '${mediaKey}'. Max available: ${record.maximumAvailable ? record.maximumAvailable.qualityLabel : 'None'}`);
        return record;
    }

    /**
     * Inserts a representation avoiding duplicates based on container, resolution, bitrate, and codec.
     */
    insertDeduplicated(list, rep) {
        const isDup = list.some(existing => {
            if (rep.isAudioOnly || rep.mediaType === 'AUDIO') {
                return existing.container === rep.container &&
                       (existing.audioBitrate || existing.bitrate) === (rep.audioBitrate || rep.bitrate) &&
                       existing.audioCodec === rep.audioCodec;
            }
            return existing.container === rep.container &&
                   existing.height === rep.height &&
                   existing.width === rep.width &&
                   existing.bitrate === rep.bitrate &&
                   existing.codec === rep.codec;
        });

        if (!isDup) {
            list.push(rep);
        }
    }

    getSessionRecord(mediaKey) {
        return this.sessionRegistries.get(mediaKey) || null;
    }

    clearSession(mediaKey) {
        this.sessionRegistries.delete(mediaKey);
    }

    clearAll() {
        this.sessionRegistries.clear();
    }
}
