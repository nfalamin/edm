/**
 * EDM Extension - Media Format Validator
 * Version: 1.0.0
 * Validates representation schemas, URLs, mime types, and security constraints.
 */

import { SecurityValidator } from '../security/validator.js';
import { Logger } from '../core/logger.js';

export class FormatValidator {
    /**
     * Validates a candidate MediaRepresentation.
     * @param {Object} rep 
     * @returns {boolean}
     */
    static validateRepresentation(rep) {
        if (!rep || typeof rep !== 'object') return false;

        // 1. URL Validation
        const targetUrl = rep.url || rep.manifestUrl;
        if (!targetUrl || typeof targetUrl !== 'string' || !SecurityValidator.isValidMediaUrl(targetUrl)) {
            Logger.debug(`[FormatValidator] Rejected representation: invalid URL '${targetUrl}'`);
            return false;
        }

        // 2. Numeric bounds
        if (rep.width < 0 || rep.height < 0 || rep.bitrate < 0) {
            Logger.debug(`[FormatValidator] Rejected representation: negative dimensions or bitrate`);
            return false;
        }

        // 3. Expiration Check
        if (rep.expiresAt && rep.expiresAt > 0 && Date.now() >= rep.expiresAt) {
            Logger.debug(`[FormatValidator] Rejected representation: stream URL expired`);
            return false;
        }

        return true;
    }
}
