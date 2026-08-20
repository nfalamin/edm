/**
 * EDM Extension - Quality Normalizer & Video/Audio Pairing Engine
 * Version: 1.0.0
 * Derives human-readable quality labels strictly from actual observed dimensions.
 * Pairs adaptive video-only streams with high-bitrate audio streams for EDM Desktop ffmpeg assembly.
 */

import { MediaRepresentation, Downloadability } from './representation-model.js';

export class QualityNormalizer {
    /**
     * Normalizes numerical height and width into a standard quality label.
     * @param {number} width 
     * @param {number} height 
     * @param {number} fps 
     * @returns {string} e.g. "1080p60", "2160p (4K)", "720p"
     */
    static normalizeQualityLabel(width, height, fps = 0) {
        const h = Number(height) || 0;
        const w = Number(width) || 0;
        const effectiveHeight = h > 0 ? h : (w > 0 ? Math.round(w * (9 / 16)) : 0);

        if (effectiveHeight <= 0) return 'Audio / Unknown';

        let label = '';
        if (effectiveHeight >= 4320) {
            label = '4320p (8K)';
        } else if (effectiveHeight >= 2160) {
            label = '2160p (4K UHD)';
        } else if (effectiveHeight >= 1440) {
            label = '1440p (2K QHD)';
        } else if (effectiveHeight >= 1080) {
            label = '1080p (Full HD)';
        } else if (effectiveHeight >= 720) {
            label = '720p (HD)';
        } else if (effectiveHeight >= 480) {
            label = '480p';
        } else if (effectiveHeight >= 360) {
            label = '360p';
        } else if (effectiveHeight >= 240) {
            label = '240p';
        } else if (effectiveHeight >= 144) {
            label = '144p';
        } else {
            label = `${effectiveHeight}p`;
        }

        if (fps >= 50) {
            label = label.replace(/(\d+p)/, `$1${Math.round(fps)}`);
        }

        return label;
    }

    /**
     * Calculates the maximum available video representation from a list of discovered representations.
     * @param {Array<MediaRepresentation>} representations 
     * @returns {MediaRepresentation|null}
     */
    static calculateMaximumAvailableQuality(representations) {
        if (!Array.isArray(representations) || representations.length === 0) return null;

        const videoReps = representations.filter(r => (r.mediaType === 'VIDEO' || r.mediaType === 'MUXED' || r.mediaType === 'ADAPTIVE' || r.isVideoOnly) && r.height > 0);
        if (videoReps.length === 0) return null;

        // Sort descending by height, then width, then bitrate, then fps
        videoReps.sort((a, b) => {
            if (b.height !== a.height) return b.height - a.height;
            if (b.width !== a.width) return b.width - a.width;
            if (b.bitrate !== a.bitrate) return b.bitrate - a.bitrate;
            return (b.fps || 0) - (a.fps || 0);
        });

        return videoReps[0];
    }

    /**
     * Finds the highest bitrate audio representation to pair with a video-only stream.
     * @param {Array<MediaRepresentation>} audioRepresentations 
     * @param {string} preferredContainer e.g. "mp4", "webm"
     * @returns {MediaRepresentation|null}
     */
    static selectBestAudioStream(audioRepresentations, preferredContainer = 'mp4') {
        if (!Array.isArray(audioRepresentations) || audioRepresentations.length === 0) return null;

        const audios = audioRepresentations.filter(a => a.isAudioOnly || a.mediaType === 'AUDIO');
        if (audios.length === 0) return null;

        // Prioritize matching container if available, then highest bitrate
        const sorted = [...audios].sort((a, b) => {
            const matchA = a.container === preferredContainer ? 1 : 0;
            const matchB = b.container === preferredContainer ? 1 : 0;
            if (matchB !== matchA) return matchB - matchA;
            return (b.audioBitrate || b.bitrate || 0) - (a.audioBitrate || a.bitrate || 0);
        });

        return sorted[0];
    }

    /**
     * Constructs a paired download descriptor for EDM desktop processing.
     * @param {MediaRepresentation} videoRep 
     * @param {MediaRepresentation} audioRep 
     * @returns {Object}
     */
    static createPairedDownloadPackage(videoRep, audioRep = null) {
        if (!videoRep) throw new Error("Video representation is required.");

        const requiresMerge = videoRep.isVideoOnly && !!audioRep;
        const totalSize = (videoRep.estimatedSizeBytes > 0 ? videoRep.estimatedSizeBytes : 0) +
                          (audioRep && audioRep.estimatedSizeBytes > 0 ? audioRep.estimatedSizeBytes : 0);

        return {
            quality: QualityNormalizer.normalizeQualityLabel(videoRep.width, videoRep.height, videoRep.fps),
            format: videoRep.container,
            formatId: videoRep.formatId,
            videoUrl: videoRep.url,
            audioUrl: audioRep ? audioRep.url : '',
            manifestUrl: videoRep.manifestUrl || '',
            requiresFfmpegMerge: requiresMerge,
            width: videoRep.width,
            height: videoRep.height,
            fps: videoRep.fps,
            videoCodec: videoRep.codec,
            audioCodec: audioRep ? audioRep.audioCodec || audioRep.codec : (videoRep.audioCodec || ''),
            estimatedSizeBytes: totalSize > 0 ? totalSize : -1,
            videoSizeBytes: videoRep.estimatedSizeBytes,
            audioSizeBytes: audioRep ? audioRep.estimatedSizeBytes : -1,
            isAudioOnly: false,
            downloadability: requiresMerge ? Downloadability.REQUIRES_MERGE : videoRep.downloadability
        };
    }
}
