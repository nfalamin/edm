/**
 * EDM Extension - Generic HTML5 & OpenGraph Media Extractor
 * Version: 1.0.0
 * Extracts direct HTML5 video/audio elements, source tags, OpenGraph video meta tags, and HLS/DASH links.
 */

import { BaseExtractor } from './base-extractor.js';
import { MediaRepresentation, Downloadability } from '../media/representation-model.js';
import { QualityNormalizer } from '../media/quality-normalizer.js';
import { Logger } from '../core/logger.js';

export class GenericExtractor extends BaseExtractor {
    constructor() {
        super('GenericExtractor');
    }

    canHandle(url, context = {}) {
        return true; // Universal fallback extractor
    }

    /**
     * Extracts representations from HTML5 media elements or candidate URLs.
     * @param {Object} context { url, videoWidth, videoHeight, duration, title, srcList, currentSrc }
     * @returns {Promise<Object>}
     */
    async extract(context = {}) {
        const url = context.currentSrc || context.url || '';
        const title = context.title || 'Web Video';
        const duration = Number(context.duration) || 0;
        const width = Number(context.videoWidth) || 0;
        const height = Number(context.videoHeight) || 0;

        const videoRepresentations = [];
        const audioRepresentations = [];

        if (!url) {
            throw new Error("No media URL found in generic extraction context.");
        }

        const isHls = url.includes('.m3u8');
        const isDash = url.includes('.mpd');
        const isAudio = url.includes('.mp3') || url.includes('.m4a') || url.includes('.flac') || url.includes('.wav') || context.mediaType === 'AUDIO';

        if (isHls) {
            videoRepresentations.push(new MediaRepresentation({
                formatId: 'generic_hls_stream',
                mediaType: 'ADAPTIVE',
                container: 'm3u8',
                mimeType: 'application/x-mpegURL',
                url,
                manifestUrl: url,
                duration,
                width,
                height,
                isAdaptive: true,
                downloadability: Downloadability.REQUIRES_PROCESSING,
                qualityLabel: height > 0 ? QualityNormalizer.normalizeQualityLabel(width, height) : 'HLS Stream',
                source: 'GENERIC_HLS'
            }));
        } else if (isDash) {
            videoRepresentations.push(new MediaRepresentation({
                formatId: 'generic_dash_stream',
                mediaType: 'ADAPTIVE',
                container: 'mpd',
                mimeType: 'application/dash+xml',
                url,
                manifestUrl: url,
                duration,
                width,
                height,
                isAdaptive: true,
                downloadability: Downloadability.REQUIRES_PROCESSING,
                qualityLabel: height > 0 ? QualityNormalizer.normalizeQualityLabel(width, height) : 'DASH Stream',
                source: 'GENERIC_DASH'
            }));
        } else if (isAudio) {
            audioRepresentations.push(new MediaRepresentation({
                formatId: 'generic_audio_stream',
                mediaType: 'AUDIO',
                container: url.split('?')[0].split('.').pop() || 'mp3',
                url,
                duration,
                isAudioOnly: true,
                downloadability: Downloadability.DIRECT,
                qualityLabel: 'Audio Stream',
                source: 'GENERIC_AUDIO'
            }));
        } else {
            // Direct progressive MP4 / WebM
            const container = url.split('?')[0].split('.').pop() || 'mp4';
            const rep = new MediaRepresentation({
                formatId: `generic_prog_${height || 'direct'}`,
                mediaType: 'MUXED',
                container,
                url,
                duration,
                width,
                height,
                isVideoOnly: false,
                isAudioOnly: false,
                isMuxed: true,
                downloadability: Downloadability.DIRECT,
                qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height),
                source: 'GENERIC_HTML5'
            });
            videoRepresentations.push(rep);
        }

        return {
            title,
            duration,
            videoRepresentations,
            audioRepresentations,
            maximumAvailable: QualityNormalizer.calculateMaximumAvailableQuality(videoRepresentations)
        };
    }
}
