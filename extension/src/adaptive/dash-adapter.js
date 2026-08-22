/**
 * EDM Extension - DASH Adapter (MPD Manifest Parser)
 * Version: 1.0.0
 * Parses DASH MPD adaptation sets, representations, segment templates, and detects DRM schemes (Widevine/PlayReady).
 */

import { MediaRepresentation, Downloadability } from '../media/representation-model.js';
import { QualityNormalizer } from '../media/quality-normalizer.js';
import { Logger } from '../core/logger.js';

export class DashAdapter {
    static detect(url, text = '') {
        if (typeof url === 'string' && url.includes('.mpd')) return true;
        if (typeof text === 'string' && (text.includes('<MPD') || text.includes('urn:mpeg:dash:schema:mpd:2011'))) return true;
        return false;
    }

    /**
     * Parses DASH MPD manifest XML text.
     */
    static parseMpd(mpdXmlText, mpdUrl = '') {
        if (!mpdXmlText || typeof mpdXmlText !== 'string') return { videoVariants: [], audioVariants: [], isDrm: false, duration: 0 };

        const videoVariants = [];
        const audioVariants = [];
        let isDrm = false;

        // 1. Detect DRM Protection
        if (mpdXmlText.includes('ContentProtection') ||
            mpdXmlText.includes('urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed') || // Widevine
            mpdXmlText.includes('urn:uuid:9a04f079-9840-4286-ab92-e65be0885f95') || // PlayReady
            mpdXmlText.includes('cenc:default_KID')) {
            isDrm = true;
            Logger.warn("[DashAdapter] Detected DRM (CENC / Widevine / PlayReady) protected DASH stream.");
        }

        // 2. Extract mediaPresentationDuration="PT1H2M30S"
        let durationSeconds = 0;
        const durMatch = mpdXmlText.match(/mediaPresentationDuration="PT([^"]+)"/);
        if (durMatch) {
            durationSeconds = DashAdapter.parseIsoDuration(durMatch[1]);
        }

        // 3. Extract AdaptationSets and Representations
        const adaptSetRegex = /<AdaptationSet\s+([^>]*)>([\s\S]*?)<\/AdaptationSet>/g;
        let adaptMatch;

        while ((adaptMatch = adaptSetRegex.exec(mpdXmlText)) !== null) {
            const setAttrs = adaptMatch[1];
            const setBody = adaptMatch[2];

            const setMimeMatch = setAttrs.match(/mimeType="([^"]+)"/);
            const setCodecsMatch = setAttrs.match(/codecs="([^"]+)"/);
            const setContentTypeMatch = setAttrs.match(/contentType="([^"]+)"/);

            const setMime = setMimeMatch ? setMimeMatch[1] : '';
            const setCodecs = setCodecsMatch ? setCodecsMatch[1] : '';
            const setContentType = setContentTypeMatch ? setContentTypeMatch[1] : '';

            const repRegex = /<Representation\s+([^>]+)(?:\/?>|>([\s\S]*?)<\/Representation>)/g;
            let repMatch;

            while ((repMatch = repRegex.exec(setBody)) !== null) {
                const attrStr = repMatch[1];
                const idMatch = attrStr.match(/id="([^"]+)"/);
                const bandwidthMatch = attrStr.match(/bandwidth="([^"]+)"/);
                const widthMatch = attrStr.match(/width="([^"]+)"/);
                const heightMatch = attrStr.match(/height="([^"]+)"/);
                const mimeMatch = attrStr.match(/mimeType="([^"]+)"/);
                const codecsMatch = attrStr.match(/codecs="([^"]+)"/);

                const formatId = idMatch ? idMatch[1] : `dash_${Date.now()}`;
                const bandwidth = bandwidthMatch ? parseInt(bandwidthMatch[1], 10) : 0;
                const width = widthMatch ? parseInt(widthMatch[1], 10) : 0;
                const height = heightMatch ? parseInt(heightMatch[1], 10) : 0;
                const mimeType = mimeMatch ? mimeMatch[1] : setMime;
                const codecs = codecsMatch ? codecsMatch[1] : setCodecs;

                const isAudio = setContentType === 'audio' ||
                                mimeType.startsWith('audio/') ||
                                codecs.startsWith('mp4a') ||
                                codecs.startsWith('opus') ||
                                codecs.startsWith('vorbis') ||
                                (!height && !width && mimeType.includes('audio'));

                const isVideo = setContentType === 'video' ||
                                mimeType.startsWith('video/') ||
                                height > 0 || width > 0;

                if (isVideo && !isAudio) {
                    videoVariants.push(new MediaRepresentation({
                        formatId: `dash_vid_${formatId}`,
                        mediaType: 'ADAPTIVE',
                        container: mimeType.includes('webm') ? 'webm' : 'mp4',
                        mimeType,
                        codec: codecs,
                        width,
                        height,
                        bitrate: bandwidth,
                        manifestUrl: mpdUrl,
                        isVideoOnly: true,
                        isAudioOnly: false,
                        isAdaptive: true,
                        duration: durationSeconds,
                        downloadability: isDrm ? Downloadability.UNAVAILABLE : Downloadability.REQUIRES_MERGE,
                        qualityLabel: QualityNormalizer.normalizeQualityLabel(width, height),
                        source: 'DASH_MPD_REPRESENTATION'
                    }));
                } else if (isAudio) {
                    audioVariants.push(new MediaRepresentation({
                        formatId: `dash_aud_${formatId}`,
                        mediaType: 'AUDIO',
                        container: mimeType.includes('webm') ? 'opus' : 'm4a',
                        mimeType,
                        audioCodec: codecs,
                        audioBitrate: bandwidth,
                        manifestUrl: mpdUrl,
                        isVideoOnly: false,
                        isAudioOnly: true,
                        isAdaptive: true,
                        duration: durationSeconds,
                        downloadability: isDrm ? Downloadability.UNAVAILABLE : Downloadability.REQUIRES_PROCESSING,
                        qualityLabel: `Audio (${Math.round(bandwidth / 1000)} kbps)`,
                        source: 'DASH_MPD_AUDIO'
                    }));
                }
            }
        }

        return { videoVariants, audioVariants, isDrm, duration: durationSeconds };
    }

    static parseIsoDuration(isoDuration) {
        let total = 0;
        const hours = isoDuration.match(/(\d+)H/);
        const mins = isoDuration.match(/(\d+)M/);
        const secs = isoDuration.match(/([\d.]+)S/);

        if (hours) total += parseInt(hours[1], 10) * 3600;
        if (mins) total += parseInt(mins[1], 10) * 60;
        if (secs) total += parseFloat(secs[1]);

        return total;
    }
}
