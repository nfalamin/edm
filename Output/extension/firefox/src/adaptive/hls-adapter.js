/**
 * EDM Extension - HLS Adapter (Master & Media Playlist Parser)
 * Version: 1.0.0
 * Parses HLS variants, audio groups, init segments (#EXT-X-MAP), media chunks, and detects DRM encryption.
 */

import { MediaRepresentation, Downloadability } from '../media/representation-model.js';
import { QualityNormalizer } from '../media/quality-normalizer.js';
import { Segment } from './segment-model.js';
import { Logger } from '../core/logger.js';

export class HlsAdapter {
    static detect(url, text = '') {
        if (typeof url === 'string' && (url.includes('.m3u8') || url.includes('/hls/'))) return true;
        if (typeof text === 'string' && text.startsWith('#EXTM3U')) return true;
        return false;
    }

    /**
     * Parses HLS Master Playlist content.
     */
    static parseMasterPlaylist(m3u8Text, masterUrl = '') {
        if (!m3u8Text || typeof m3u8Text !== 'string') return { videoVariants: [], audioVariants: [], isDrm: false };

        const lines = m3u8Text.split(/\r?\n/);
        const videoVariants = [];
        const audioVariants = [];
        let isDrm = false;

        let currentStreamInfo = null;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();
            if (!line) continue;

            // Check DRM encryption
            if (line.startsWith('#EXT-X-KEY:') && (line.includes('METHOD=SAMPLE-AES') || line.includes('KEYFORMAT="com.apple.streamingkeydelivery"'))) {
                isDrm = true;
                Logger.warn("[HlsAdapter] Detected DRM/FairPlay protected HLS stream.");
            }

            // Audio Group: #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="English",LANGUAGE="en",URI="audio/en.m3u8"
            if (line.startsWith('#EXT-X-MEDIA:') && line.includes('TYPE=AUDIO')) {
                const attrs = HlsAdapter.parseAttributes(line.substring(13));
                if (attrs.URI) {
                    const audioUrl = HlsAdapter.resolveUrl(attrs.URI, masterUrl);
                    audioVariants.push(new MediaRepresentation({
                        formatId: `hls_audio_${attrs['GROUP-ID'] || 'default'}_${attrs.LANGUAGE || 'und'}`,
                        mediaType: 'AUDIO',
                        container: 'm4a',
                        url: audioUrl,
                        manifestUrl: masterUrl,
                        isAudioOnly: true,
                        downloadability: Downloadability.REQUIRES_PROCESSING,
                        qualityLabel: attrs.NAME ? `Audio (${attrs.NAME})` : 'Audio',
                        source: 'HLS_MEDIA_AUDIO'
                    }));
                }
            }

            // Stream Variant: #EXT-X-STREAM-INF
            if (line.startsWith('#EXT-X-STREAM-INF:')) {
                currentStreamInfo = HlsAdapter.parseAttributes(line.substring(18));
            } else if (currentStreamInfo && !line.startsWith('#')) {
                const variantUrl = HlsAdapter.resolveUrl(line, masterUrl);
                const width = currentStreamInfo.width || 0;
                const height = currentStreamInfo.height || 0;
                const bitrate = currentStreamInfo.bandwidth || 0;
                const fps = currentStreamInfo.frameRate || 0;
                const codecs = currentStreamInfo.codecs || '';

                videoVariants.push(new MediaRepresentation({
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
                    manifestUrl: masterUrl,
                    isVideoOnly: false,
                    isAdaptive: true,
                    downloadability: isDrm ? Downloadability.UNAVAILABLE : Downloadability.REQUIRES_PROCESSING,
                    qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height, fps),
                    source: 'HLS_MASTER_PLAYLIST'
                }));

                currentStreamInfo = null;
            }
        }

        return { videoVariants, audioVariants, isDrm };
    }

    /**
     * Parses HLS Media Playlist content into discrete media segments and init segments.
     */
    static parseMediaPlaylist(mediaM3u8Text, playlistUrl = '', representationId = '') {
        if (!mediaM3u8Text || typeof mediaM3u8Text !== 'string') return { segments: [], initSegment: null, isLive: false, totalDuration: 0 };

        const lines = mediaM3u8Text.split(/\r?\n/);
        const segments = [];
        let initSegment = null;
        let isLive = true;
        let totalDuration = 0;
        let currentDuration = 0;
        let sequenceNumber = 0;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();
            if (!line) continue;

            if (line.startsWith('#EXT-X-ENDLIST')) {
                isLive = false;
            }

            // Init segment for fMP4 HLS: #EXT-X-MAP:URI="init.mp4",BYTERANGE="720@0"
            if (line.startsWith('#EXT-X-MAP:')) {
                const mapAttrs = HlsAdapter.parseAttributes(line.substring(11));
                if (mapAttrs.URI) {
                    initSegment = new Segment({
                        representationId,
                        sequenceNumber: 0,
                        url: HlsAdapter.resolveUrl(mapAttrs.URI, playlistUrl),
                        isInitialization: true,
                        status: 'DISCOVERED'
                    });
                }
            }

            if (line.startsWith('#EXTINF:')) {
                const durStr = line.substring(8).split(',')[0];
                currentDuration = parseFloat(durStr) || 0;
            } else if (currentDuration > 0 && !line.startsWith('#')) {
                sequenceNumber++;
                const segUrl = HlsAdapter.resolveUrl(line, playlistUrl);
                segments.push(new Segment({
                    representationId,
                    sequenceNumber,
                    url: segUrl,
                    duration: currentDuration,
                    startTime: totalDuration,
                    isInitialization: false,
                    status: 'DISCOVERED'
                }));
                totalDuration += currentDuration;
                currentDuration = 0;
            }
        }

        return { segments, initSegment, isLive, totalDuration };
    }

    static parseAttributes(attrStr) {
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
            } else {
                result[key] = val;
            }
        }
        return result;
    }

    static resolveUrl(relativeOrAbsolute, baseUrl) {
        if (!baseUrl) return relativeOrAbsolute;
        try {
            return new URL(relativeOrAbsolute, baseUrl).href;
        } catch (e) {
            return relativeOrAbsolute;
        }
    }
}
