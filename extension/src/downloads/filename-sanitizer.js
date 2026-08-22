/**
 * EDM Extension - Filename Security & Normalization Engine
 * Version: 1.0.0
 * Prevents path traversal, illegal Windows character injection, reserved device names, and length overflow.
 */

export class FilenameSanitizer {
    static RESERVED_NAMES = new Set([
        'con', 'prn', 'aux', 'nul',
        'com1', 'com2', 'com3', 'com4', 'com5', 'com6', 'com7', 'com8', 'com9',
        'lpt1', 'lpt2', 'lpt3', 'lpt4', 'lpt5', 'lpt6', 'lpt7', 'lpt8', 'lpt9'
    ]);

    /**
     * Sanitizes an arbitrary page-provided or URL-derived filename into a secure, valid filename.
     * @param {string} rawFilename 
     * @param {string} fallbackExtension 
     * @returns {string}
     */
    static sanitize(rawFilename, fallbackExtension = 'mp4') {
        if (!rawFilename || typeof rawFilename !== 'string') {
            return `edm_download_${Date.now()}.${fallbackExtension}`;
        }

        // 1. Remove path traversal and directory separators (../, ..\, /dir/file)
        let clean = rawFilename.replace(/^.*[\\\/]/, '');

        // 2. Strip control characters (ASCII 0-31) and illegal Windows filesystem characters (< > : " / \ | ? *)
        clean = clean.replace(/[\x00-\x1F<>:"/\\|?*]/g, '_');

        // 3. Trim whitespace and trailing dots / spaces (illegal in Windows)
        clean = clean.trim().replace(/[. ]+$/, '');

        // 4. Extract base name and extension
        let baseName = clean;
        let ext = '';
        const lastDotIndex = clean.lastIndexOf('.');

        if (lastDotIndex > 0 && lastDotIndex < clean.length - 1) {
            baseName = clean.substring(0, lastDotIndex);
            ext = clean.substring(lastDotIndex + 1).toLowerCase();
        } else {
            ext = fallbackExtension.toLowerCase();
        }

        // 5. Protect against reserved Windows device names (CON, PRN, AUX, NUL, COM1..9, LPT1..9)
        if (FilenameSanitizer.RESERVED_NAMES.has(baseName.toLowerCase())) {
            baseName = `file_${baseName.toLowerCase()}`;
        }

        // 6. Enforce safe length constraints (max 180 chars for base name to avoid path limit)
        if (baseName.length > 180) {
            baseName = baseName.substring(0, 180);
        }

        if (!baseName || baseName.trim() === '') {
            baseName = `edm_download_${Date.now()}`;
        }

        return `${baseName}.${ext}`;
    }
}
