/**
 * EDM Extension - Structured Diagnostic Logger
 * Automatically scrubs cookies, tokens, and authorization headers from output.
 */

export class Logger {
    static scrub(str) {
        if (!str || typeof str !== 'string') return str;
        return str.replace(
            /(cookies?|authorization|token|password|auth|secret)\s*[:=]\s*["']?[^"',;&\s]+["']?/gi,
            '$1=[REDACTED]'
        );
    }

    static info(message, ...args) {
        console.info(`[EDM INFO] ${Logger.scrub(message)}`, ...args);
    }

    static warn(message, ...args) {
        console.warn(`[EDM WARN] ${Logger.scrub(message)}`, ...args);
    }

    static error(message, ...args) {
        console.error(`[EDM ERROR] ${Logger.scrub(message)}`, ...args);
    }

    static debug(message, ...args) {
        if (typeof window !== 'undefined' && window.__EDM_DEBUG__) {
            console.debug(`[EDM DEBUG] ${Logger.scrub(message)}`, ...args);
        }
    }
}
