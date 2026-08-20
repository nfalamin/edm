/**
 * EDM Extension - Security & URL Validator Unit Tests
 */

import { SecurityValidator } from '../../src/security/validator.js';

export function runSecurityValidatorTests() {
    const results = [];

    function test(name, fn) {
        try {
            fn();
            results.push({ name, status: "PASS" });
        } catch (err) {
            results.push({ name, status: "FAIL", error: err.message });
        }
    }

    // 1. Valid HTTPS Direct URL
    test("SecurityValidator: Validates HTTPS video stream URL", () => {
        const valid = SecurityValidator.isValidMediaUrl("https://example.com/stream/master.m3u8");
        if (!valid) throw new Error("Expected valid URL to pass");
    });

    // 2. Reject Insecure / Non-HTTP Schemes
    test("SecurityValidator: Rejects javascript: scheme", () => {
        const valid = SecurityValidator.isValidMediaUrl("javascript:alert(1)");
        if (valid) throw new Error("Expected javascript scheme to fail");
    });

    // 3. HTML Escaping
    test("SecurityValidator: Escapes HTML XSS payloads", () => {
        const escaped = SecurityValidator.sanitizeHtml("<script>alert('xss')</script>");
        if (escaped.includes("<script>")) throw new Error("Failed to sanitize script tag");
        if (!escaped.includes("&lt;script&gt;")) throw new Error("Expected entity encoded HTML");
    });

    // 4. Cipher URL Parameter Extraction
    test("SecurityValidator: Reconstructs signed cipher URL safely", () => {
        const cipher = "url=https%3A%2F%2Fexample.com%2Fvideo.mp4&s=sig12345&sp=sig";
        const parsed = SecurityValidator.parseAndValidateCipherUrl(cipher);
        if (!parsed.startsWith("https://example.com/video.mp4?sig=sig12345")) {
            throw new Error(`Unexpected parsed cipher URL: ${parsed}`);
        }
    });

    return results;
}
