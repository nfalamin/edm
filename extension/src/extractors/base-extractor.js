/**
 * EDM Extension - Base Media Extractor Interface
 * Version: 1.0.0
 * Abstract contract for site-specific (YouTube, Vimeo) and generic media extractors.
 */

export class BaseExtractor {
    constructor(name) {
        this.name = name || 'BaseExtractor';
    }

    /**
     * Determines if this extractor can handle the current page or media candidate.
     * @param {string} url 
     * @param {Object} context 
     * @returns {boolean}
     */
    canHandle(url, context = {}) {
        throw new Error("canHandle() must be implemented by subclass.");
    }

    /**
     * Extracts all available media representations, metadata, title, and duration.
     * @param {Object} context { url, html, playerResponse, domElement }
     * @returns {Promise<Object>} { title, duration, videoRepresentations, audioRepresentations, isDrm }
     */
    async extract(context = {}) {
        throw new Error("extract() must be implemented by subclass.");
    }
}
