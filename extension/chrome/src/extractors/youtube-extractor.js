/**
 * EDM Extension - YouTube Media Extractor
 * Version: 1.0.0
 * Extracts adaptive formats (144p - 4K/8K, 60fps, Opus/AAC audio), decodes signatures, and extracts metadata.
 */

import { BaseExtractor } from './base-extractor.js';
import { MediaRepresentation, Downloadability } from '../media/representation-model.js';
import { QualityNormalizer } from '../media/quality-normalizer.js';
import { YouTubeCipher } from './youtube-cipher.js';
import { Logger } from '../core/logger.js';

export class YouTubeExtractor extends BaseExtractor {
    constructor() {
        super('YouTubeExtractor');
    }

    canHandle(url, context = {}) {
        if (!url || typeof url !== 'string') return false;
        return url.includes('youtube.com/watch') ||
               url.includes('youtube.com/shorts') ||
               url.includes('youtube.com/embed') ||
               url.includes('youtu.be/');
    }

    /**
     * Extracts all streams and metadata from YouTube context.
     * @param {Object} context { url, playerResponse, html, cipherOps }
     * @returns {Promise<Object>}
     */
    async extract(context = {}) {
        let playerResponse = context.playerResponse;

        // If playerResponse not provided as object, extract from raw HTML or window object
        if (!playerResponse && context.html) {
            playerResponse = this.extractPlayerResponseFromHtml(context.html);
        }

        if (!playerResponse) {
            throw new Error("Could not find ytInitialPlayerResponse in YouTube context.");
        }

        const videoDetails = playerResponse.videoDetails || {};
        const streamingData = playerResponse.streamingData || {};
        const title = videoDetails.title || 'YouTube Video';
        const duration = Number(videoDetails.lengthSeconds) || 0;
        const author = videoDetails.author || '';
        const isLive = !!videoDetails.isLiveContent;

        const videoRepresentations = [];
        const audioRepresentations = [];
        const muxedRepresentations = [];

        const cipherOps = context.cipherOps || [];

        // 1. Process Legacy Muxed Formats (360p, 720p progressive)
        const muxedFormats = streamingData.formats || [];
        for (const fmt of muxedFormats) {
            const resolvedUrl = this.resolveStreamUrl(fmt, cipherOps);
            if (!resolvedUrl) continue;

            const width = fmt.width || 0;
            const height = fmt.height || 0;
            const fps = fmt.fps || 30;
            const bitrate = fmt.bitrate || fmt.averageBitrate || 0;

            const rep = new MediaRepresentation({
                formatId: `yt_mux_${fmt.itag || height}`,
                mediaType: 'MUXED',
                container: 'mp4',
                mimeType: fmt.mimeType || 'video/mp4',
                codec: fmt.mimeType || '',
                width,
                height,
                fps,
                bitrate,
                estimatedSizeBytes: fmt.contentLength ? parseInt(fmt.contentLength, 10) : -1,
                duration,
                url: resolvedUrl,
                isVideoOnly: false,
                isAudioOnly: false,
                isMuxed: true,
                downloadability: Downloadability.DIRECT,
                qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps),
                source: 'YOUTUBE_MUXED'
            });

            muxedRepresentations.push(rep);
            videoRepresentations.push(rep);
        }

        // 2. Process Adaptive Formats (144p - 4320p 8K, 60fps, HDR, Opus/AAC)
        const adaptiveFormats = streamingData.adaptiveFormats || [];
        for (const fmt of adaptiveFormats) {
            const resolvedUrl = this.resolveStreamUrl(fmt, cipherOps);
            if (!resolvedUrl) continue;

            const mimeType = (fmt.mimeType || '').toLowerCase();
            const width = fmt.width || 0;
            const height = fmt.height || 0;
            const fps = fmt.fps || 0;
            const bitrate = fmt.bitrate || fmt.averageBitrate || 0;
            const isAudio = mimeType.startsWith('audio/');
            const isVideo = mimeType.startsWith('video/') || height > 0;

            if (isVideo) {
                const rep = new MediaRepresentation({
                    formatId: `yt_vid_${fmt.itag || height}_${fps}`,
                    mediaType: 'VIDEO',
                    container: mimeType.includes('webm') ? 'webm' : 'mp4',
                    mimeType: fmt.mimeType || '',
                    codec: fmt.mimeType || '',
                    width,
                    height,
                    fps,
                    bitrate,
                    estimatedSizeBytes: fmt.contentLength ? parseInt(fmt.contentLength, 10) : -1,
                    duration,
                    url: resolvedUrl,
                    videoUrl: resolvedUrl,
                    isVideoOnly: true,
                    isAudioOnly: false,
                    isAdaptive: true,
                    downloadability: Downloadability.REQUIRES_MERGE,
                    qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps),
                    source: 'YOUTUBE_ADAPTIVE_VIDEO'
                });
                videoRepresentations.push(rep);
            } else if (isAudio) {
                const rep = new MediaRepresentation({
                    formatId: `yt_aud_${fmt.itag || bitrate}`,
                    mediaType: 'AUDIO',
                    container: mimeType.includes('webm') ? 'opus' : 'm4a',
                    mimeType: fmt.mimeType || '',
                    audioCodec: fmt.mimeType || '',
                    audioBitrate: bitrate,
                    estimatedSizeBytes: fmt.contentLength ? parseInt(fmt.contentLength, 10) : -1,
                    duration,
                    url: resolvedUrl,
                    audioUrl: resolvedUrl,
                    isVideoOnly: false,
                    isAudioOnly: true,
                    isAdaptive: true,
                    downloadability: Downloadability.REQUIRES_PROCESSING,
                    qualityLabel: `Audio (${Math.round(bitrate / 1000)} kbps)`,
                    source: 'YOUTUBE_ADAPTIVE_AUDIO'
                });
                audioRepresentations.push(rep);
            }
        }

        Logger.info(`[YouTubeExtractor] Discovered ${videoRepresentations.length} video streams and ${audioRepresentations.length} audio tracks for: "${title}"`);

        return {
            title,
            duration,
            author,
            isLive,
            videoRepresentations,
            audioRepresentations,
            muxedRepresentations,
            maximumAvailable: QualityNormalizer.calculateMaximumAvailableQuality(videoRepresentations)
        };
    }

    resolveStreamUrl(formatObj, cipherOps = []) {
        if (formatObj.url) return formatObj.url;

        const cipherStr = formatObj.signatureCipher || formatObj.cipher;
        if (cipherStr) {
            return YouTubeCipher.decodeCipher(cipherStr, cipherOps);
        }
        return '';
    }

    extractPlayerResponseFromHtml(html) {
        if (!html || typeof html !== 'string') return null;

        // Extract JSON from ytInitialPlayerResponse = {...};
        const match = html.match(/ytInitialPlayerResponse\s*=\s*({.+?});/s);
        if (match) {
            try {
                return JSON.parse(match[1]);
            } catch (e) {
                Logger.warn("[YouTubeExtractor] Failed to parse ytInitialPlayerResponse JSON:", e.message);
            }
        }
        return null;
    }
}
