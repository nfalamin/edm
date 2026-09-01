/**
 * EDM Extension - Media Extractor Registry & Router
 * Version: 1.0.0
 * Matches incoming URLs or DOM contexts to specialized site extractors or the generic fallback.
 */

import { YouTubeExtractor } from './youtube-extractor.js';
import { VimeoExtractor } from './vimeo-extractor.js';
import { GenericExtractor } from './generic-extractor.js';
import { Logger } from '../core/logger.js';

export class ExtractorRegistry {
    constructor() {
        this.extractors = [
            new YouTubeExtractor(),
            new VimeoExtractor(),
            new GenericExtractor() // Fallback is always last
        ];
    }

    /**
     * Resolves the matching extractor for a URL.
     * @param {string} url 
     * @param {Object} context 
     * @returns {BaseExtractor}
     */
    findExtractor(url, context = {}) {
        for (const ext of this.extractors) {
            if (ext.canHandle(url, context)) {
                return ext;
            }
        }
        return this.extractors[this.extractors.length - 1]; // GenericExtractor
    }

    /**
     * Executes extraction using the best matched extractor.
     * @param {Object} context { url, html, playerResponse, config, domElement }
     * @returns {Promise<Object>} Discovered representations & metadata
     */
    async extract(context = {}) {
        const url = context.url || '';
        const extractor = this.findExtractor(url, context);
        Logger.info(`[ExtractorRegistry] Selected extractor '${extractor.name}' for URL: ${url}`);
        return extractor.extract(context);
    }
}
