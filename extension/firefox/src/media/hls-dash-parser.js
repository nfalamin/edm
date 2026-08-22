/**
 * EDM Extension - HLS M3U8 & DASH MPD Manifest Parser
 * Version: 1.0.0
 * Parses adaptive master playlists without dynamic code execution.
 */

import { MediaRepresentation, Downloadability } from './representation-model.js';
import { QualityNormalizer } from './quality-normalizer.js';
import { Logger } from '../core/logger.js';

export class AdaptiveManifestParser {
    /**
     * Parses an HLS Master Playlist (M3U8) text content.
     * @param {string} m3u8Text 
     * @param {string} baseUrl 
     * @returns {Array<MediaRepresentation>}
     */
    static parseHlsMasterPlaylist(m3u8Text, baseUrl = '') {
        if (!m3u8Text || typeof m3u8Text !== 'string') return [];
        const lines = m3u8Text.split(/\r?\n/);
        const representations = [];

        let currentStreamInfo = null;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();
            if (!line) continue;

            if (line.startsWith('#EXT-X-STREAM-INF:')) {
                currentStreamInfo = AdaptiveManifestParser.parseStreamInfAttributes(line.substring(18));
            } else if (currentStreamInfo && !line.startsWith('#')) {
                // Next non-comment line is the variant URI
                const variantUrl = AdaptiveManifestParser.resolveRelativeUrl(line, baseUrl);
                const width = currentStreamInfo.width || 0;
                const height = currentStreamInfo.height || 0;
                const bitrate = currentStreamInfo.bandwidth || 0;
                const fps = currentStreamInfo.frameRate || 0;
                const codecs = currentStreamInfo.codecs || '';

                representations.push(new MediaRepresentation({
                    formatId: `hls_${height}p_${bitrate}`,
                    mediaType: 'ADAPTIVE',
                    container: 'm3u8',
                    mimeType: 'application/x-mpegURL',
                    codec: codecs,
                    width,
                    height,
                    fps,
                    bitrate,
                    url: variantUrl,
                    manifestUrl: baseUrl,
                    isAdaptive: true,
                    downloadability: Downloadability.REQUIRES_PROCESSING,
                    qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps),
                    source: 'HLS_MASTER_PLAYLIST'
                }));

                currentStreamInfo = null;
            }
        }

        Logger.info(`[AdaptiveManifestParser] Discovered ${representations.length} HLS stream variants from master playlist.`);
        return representations;
    }

    /**
     * Parses standard STREAM-INF attributes: BANDWIDTH=1280000,RESOLUTION=1280x720,CODECS="avc1.4d401f,mp4a.40.2"
     */
    static parseStreamInfAttributes(attrStr) {
        const result = {};
        const regex = /([A-Z0-9-]+)=(?:"([^"]*)"|([^,]*))/g;
        let match;

        while ((match = regex.exec(attrStr)) !== null) {
            const key = match[1];
            const val = match[2] !== undefined ? match[2] : match[3];

            if (key === 'BANDWIDTH' || key === 'AVERAGE-BANDWIDTH') {
                result.bandwidth = parseInt(val, 10);
            } else if (key === 'RESOLUTION') {
                const parts = val.split('x');
                if (parts.length === 2) {
                    result.width = parseInt(parts[0], 10);
                    result.height = parseInt(parts[1], 10);
                }
            } else if (key === 'FRAME-RATE') {
                result.frameRate = parseFloat(val);
            } else if (key === 'CODECS') {
                result.codecs = val;
            }
        }

        return result;
    }

    /**
     * Resolves a relative playlist URL against the base master manifest URL.
     */
    static resolveRelativeUrl(relativeOrAbsolute, baseUrl) {
        if (!baseUrl) return relativeOrAbsolute;
        try {
            return new URL(relativeOrAbsolute, baseUrl).href;
        } catch (e) {
            return relativeOrAbsolute;
        }
    }
}
