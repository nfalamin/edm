/**
 * EDM Extension - Vimeo Media Extractor
 * Version: 1.0.0
 * Extracts progressive MP4 variants, HLS master manifests, and video metadata from Vimeo player configs.
 */

import { BaseExtractor } from './base-extractor.js';
import { MediaRepresentation, Downloadability } from '../media/representation-model.js';
import { QualityNormalizer } from '../media/quality-normalizer.js';
import { HlsAdapter } from '../adaptive/hls-adapter.js';
import { Logger } from '../core/logger.js';

export class VimeoExtractor extends BaseExtractor {
    constructor() {
        super('VimeoExtractor');
    }

    canHandle(url, context = {}) {
        if (!url || typeof url !== 'string') return false;
        return url.includes('vimeo.com/') || url.includes('player.vimeo.com/video/');
    }

    /**
     * Extracts progressive and adaptive video representations from Vimeo config.
     * @param {Object} context { url, config, html }
     * @returns {Promise<Object>}
     */
    async extract(context = {}) {
        let config = context.config;

        if (!config && context.html) {
            config = this.extractConfigFromHtml(context.html);
        }

        if (!config || !config.request) {
            throw new Error("Could not extract Vimeo configuration object.");
        }

        const videoDetails = config.video || {};
        const title = videoDetails.title || 'Vimeo Video';
        const duration = Number(videoDetails.duration) || 0;
        const files = config.request.files || {};

        const videoRepresentations = [];
        const audioRepresentations = [];

        // 1. Extract Progressive MP4 Streams (Direct Download)
        const progressive = files.progressive || [];
        for (const file of progressive) {
            if (!file.url) continue;

            const width = Number(file.width) || 0;
            const height = Number(file.height) || 0;
            const fps = Number(file.fps) || 30;
            const qualityStr = file.quality || (height ? `${height}p` : '');

            const rep = new MediaRepresentation({
                formatId: `vimeo_prog_${file.id || height || qualityStr}`,
                mediaType: 'MUXED',
                container: 'mp4',
                mimeType: file.mime || 'video/mp4',
                width,
                height,
                fps,
                url: file.url,
                duration,
                isVideoOnly: false,
                isAudioOnly: false,
                isMuxed: true,
                downloadability: Downloadability.DIRECT,
                qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps),
                source: 'VIMEO_PROGRESSIVE'
            });

            videoRepresentations.push(rep);
        }

        // 2. Extract HLS Master Manifest if available
        if (files.hls && files.hls.cdns) {
            const cdnKeys = Object.keys(files.hls.cdns);
            if (cdnKeys.length > 0) {
                const hlsUrl = files.hls.cdns[cdnKeys[0]].url;
                if (hlsUrl) {
                    const rep = new MediaRepresentation({
                        formatId: 'vimeo_hls_master',
                        mediaType: 'ADAPTIVE',
                        container: 'm3u8',
                        mimeType: 'application/x-mpegURL',
                        url: hlsUrl,
                        manifestUrl: hlsUrl,
                        duration,
                        isAdaptive: true,
                        downloadability: Downloadability.REQUIRES_PROCESSING,
                        qualityLabel: 'Adaptive Stream (HLS)',
                        source: 'VIMEO_HLS'
                    });
                    videoRepresentations.push(rep);
                }
            }
        }

        Logger.info(`[VimeoExtractor] Discovered ${videoRepresentations.length} representations for: "${title}"`);

        return {
            title,
            duration,
            videoRepresentations,
            audioRepresentations,
            maximumAvailable: QualityNormalizer.calculateMaximumAvailableQuality(videoRepresentations)
        };
    }

    extractConfigFromHtml(html) {
        if (!html || typeof html !== 'string') return null;

        // Extract window.vimeo.clip_page_config or window.playerConfig = {...}
        const match = html.match(/(?:window\.playerConfig|clip_page_config)\s*=\s*({.+?});/s);
        if (match) {
            try {
                return JSON.parse(match[1]);
            } catch (e) {
                Logger.warn("[VimeoExtractor] Failed to parse Vimeo JSON config:", e.message);
            }
        }
        return null;
    }
}
