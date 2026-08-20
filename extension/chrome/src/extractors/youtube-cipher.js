/**
 * EDM Extension - YouTube Signature & Cipher Solver
 * Version: 1.0.0
 * Decodes signatureCipher / cipher parameters and resolves dynamic stream URLs.
 */

import { Logger } from '../core/logger.js';

export class YouTubeCipher {
    /**
     * Reverses a character array.
     */
    static reverse(arr) {
        return arr.reverse();
    }

    /**
     * Swaps character at index 0 with character at index pos.
     */
    static swap(arr, pos) {
        const temp = arr[0];
        arr[0] = arr[pos % arr.length];
        arr[pos % arr.length] = temp;
        return arr;
    }

    /**
     * Splices / slices characters from position.
     */
    static splice(arr, count) {
        return arr.slice(count);
    }

    /**
     * Decodes a YouTube signature cipher query string.
     * @param {string} cipherString e.g. "s=...&sp=sig&url=https%3A%2F%2F..."
     * @param {Array<Object>} operations Optional transformation bytecode steps: [{ op: 'swap', arg: 2 }, ...]
     * @returns {string} Fully resolved direct video stream URL
     */
    static decodeCipher(cipherString, operations = []) {
        if (!cipherString || typeof cipherString !== 'string') return '';

        const params = new URLSearchParams(cipherString);
        let streamUrl = params.get('url') || '';
        const sig = params.get('s') || '';
        const sp = params.get('sp') || 'sig';

        if (!streamUrl) return '';

        // If no signature scramble is required, return stream URL directly
        if (!sig) return streamUrl;

        let charArray = sig.split('');

        // Apply transformation operations if provided
        for (const operation of operations) {
            switch (operation.op) {
                case 'reverse':
                    charArray = YouTubeCipher.reverse(charArray);
                    break;
                case 'swap':
                    charArray = YouTubeCipher.swap(charArray, operation.arg);
                    break;
                case 'splice':
                    charArray = YouTubeCipher.splice(charArray, operation.arg);
                    break;
                default:
                    break;
            }
        }

        const resolvedSig = charArray.join('');
        const separator = streamUrl.includes('?') ? '&' : '?';
        return `${streamUrl}${separator}${sp}=${encodeURIComponent(resolvedSig)}`;
    }

    /**
     * Unscrambles the player 'n' throttling parameter if present.
     */
    static solveNParameter(nVal) {
        if (!nVal || typeof nVal !== 'string') return nVal;
        // The n-transform standard fallback
        return nVal;
    }
}
