/**
 * EDM Extension - Download Candidate Model
 * Version: 1.0.0
 * Represents a validated candidate ready for policy evaluation and native handoff.
 */

import { SecurityValidator } from '../security/validator.js';
import { FilenameSanitizer } from './filename-sanitizer.js';

export class DownloadCandidate {
    constructor(options = {}) {
        this.downloadId = options.downloadId || `dl_${Date.now()}_${Math.random().toString(36).substr(2, 7)}`;
        this.requestId = options.requestId || `req_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.mediaSessionId = options.mediaSessionId || '';
        this.formatId = options.formatId || '';
        this.sourceType = options.sourceType || 'MEDIA_STREAM'; // 'MEDIA_STREAM', 'BROWSER_INTERCEPTION', 'PAGE_URL'
        
        // URLs & Origin
        this.url = options.url || options.videoUrl || '';
        this.videoUrl = options.videoUrl || options.url || '';
        this.audioUrl = options.audioUrl || '';
        this.manifestUrl = options.manifestUrl || '';
        this.pageUrl = options.pageUrl || '';
        
        // Metadata & Files
        this.filename = FilenameSanitizer.sanitize(options.filename || options.title || 'download', options.container || 'mp4');
        this.container = options.container || 'mp4';
        this.mimeType = options.mimeType || '';
        this.mediaType = options.mediaType || 'VIDEO';
        this.quality = options.quality || '';
        this.width = Number(options.width) || 0;
        this.height = Number(options.height) || 0;
        this.fps = Number(options.fps) || 0;
        this.videoCodec = options.videoCodec || '';
        this.audioCodec = options.audioCodec || '';
        
        // Sizing & Segmentation
        this.size = Number(options.size) || Number(options.estimatedSizeBytes) || -1;
        this.duration = Number(options.duration) || 0;
        this.requiresMerge = !!options.requiresMerge;
        this.videoFormatId = options.videoFormatId || options.formatId || '';
        this.audioFormatId = options.audioFormatId || '';
        
        // Lifecycle & Expiration
        this.createdAt = options.createdAt || Date.now();
        this.expiresAt = Number(options.expiresAt) || 0;
        this.downloadability = options.downloadability || 'DIRECT';
        this.confidence = options.confidence || 'HIGH';
    }

    isValid() {
        if (!this.url || !SecurityValidator.isValidMediaUrl(this.url)) return false;
        if (this.requiresMerge && (!this.audioUrl || !SecurityValidator.isValidMediaUrl(this.audioUrl))) return false;
        if (this.expiresAt > 0 && Date.now() >= this.expiresAt) return false;
        return true;
    }

    isExpired() {
        return this.expiresAt > 0 && Date.now() >= this.expiresAt;
    }
}
