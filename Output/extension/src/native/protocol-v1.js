/**
 * EDM Extension - Native Messaging Protocol Contracts (Version: v1)
 * Strictly conforms to EDM.NativeHost C# Contracts (NativeMessageContracts.cs)
 */

import { PROTOCOL_VERSION, EXTENSION_VERSION, ActionNames } from '../core/constants.js';

export class NativeProtocolV1 {
    static createPingRequest() {
        return {
            action: ActionNames.PING,
            protocolVersion: PROTOCOL_VERSION,
            extensionVersion: EXTENSION_VERSION,
            timestamp: new Date().toISOString()
        };
    }

    static createVariantResolutionRequest(url, cookies) {
        return {
            action: ActionNames.GET_MEDIA_VARIANTS,
            protocolVersion: PROTOCOL_VERSION,
            extensionVersion: EXTENSION_VERSION,
            url: url || "",
            cookies: cookies || "",
            timestamp: new Date().toISOString()
        };
    }

    static createDownloadRequest(payload) {
        const correlationId = payload.correlationId || (`edm_corr_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`);

        return {
            action: ActionNames.DOWNLOAD_REQUEST,
            protocolVersion: PROTOCOL_VERSION,
            extensionVersion: EXTENSION_VERSION,
            url: payload.url || "",
            videoUrl: payload.videoUrl || payload.url || "",
            audioUrl: payload.audioUrl || "",
            manifestUrl: payload.manifestUrl || "",
            pageUrl: payload.pageUrl || "",
            title: payload.title || "Video Media",
            filename: payload.filename || payload.fileName || "download",
            fileName: payload.filename || payload.fileName || "download",
            quality: payload.quality || "",
            format: payload.format || "",
            formatId: payload.formatId || "",
            formatArg: payload.formatArg || "",
            width: payload.width || 0,
            height: payload.height || 0,
            fps: payload.fps || 0,
            videoCodec: payload.videoCodec || payload.codec || "",
            codec: payload.codec || payload.videoCodec || "",
            audioCodec: payload.audioCodec || "",
            container: payload.container || "",
            requiresFfmpegMerge: !!payload.requiresFfmpegMerge,
            downloadIdentity: payload.downloadIdentity || "",
            correlationId: correlationId,
            estimatedSizeBytes: payload.estimatedSizeBytes || -1,
            videoSizeBytes: payload.videoSizeBytes || -1,
            audioSizeBytes: payload.audioSizeBytes || -1,
            isAudioOnly: !!payload.isAudioOnly,
            cookies: payload.cookies || "",
            headers: payload.headers || {},
            source: payload.source || "BrowserExtension_v1.0.0",
            timestamp: new Date().toISOString()
        };
    }
}
