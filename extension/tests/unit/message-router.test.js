/**
 * EDM Extension - Message Router & Validator Unit Tests
 */

import { MessageValidator, AllowedActions } from '../../src/messaging/schemas.js';
import { ErrorCodes } from '../../src/core/errors.js';
import { ActionNames, PROTOCOL_VERSION } from '../../src/core/constants.js';

export function runMessageRouterUnitTests() {
    const results = [];

    function test(name, fn) {
        try {
            fn();
            results.push({ name, status: "PASS" });
        } catch (err) {
            results.push({ name, status: "FAIL", error: err.message });
        }
    }

    // 1. Valid Message Validation
    test("MessageValidator: Accepts valid GET_MEDIA_VARIANTS message", () => {
        const msg = {
            action: ActionNames.GET_MEDIA_VARIANTS,
            protocolVersion: PROTOCOL_VERSION,
            url: "https://example.com/video.mp4"
        };
        const valid = MessageValidator.validateIncomingMessage(msg);
        if (valid !== true) throw new Error("Expected valid message to return true");
    });

    // 2. Reject Malformed Message (Null/Non-object)
    test("MessageValidator: Rejects null message", () => {
        try {
            MessageValidator.validateIncomingMessage(null);
            throw new Error("Should have thrown on null message");
        } catch (err) {
            if (err.code !== ErrorCodes.INVALID_MESSAGE) throw err;
        }
    });

    // 3. Reject Unapproved Action (Allowlist Enforcement)
    test("MessageValidator: Rejects unapproved arbitrary action", () => {
        try {
            MessageValidator.validateIncomingMessage({ action: "EXECUTE_ARBITRARY_CODE" });
            throw new Error("Should have rejected unapproved action");
        } catch (err) {
            if (err.code !== ErrorCodes.UNAUTHORIZED_MESSAGE) throw err;
        }
    });

    // 4. Reject Protocol Version Mismatch
    test("MessageValidator: Rejects protocol version mismatch", () => {
        try {
            MessageValidator.validateIncomingMessage({
                action: ActionNames.PING,
                protocolVersion: "v99.0"
            });
            throw new Error("Should have rejected version mismatch");
        } catch (err) {
            if (err.code !== ErrorCodes.VERSION_MISMATCH) throw err;
        }
    });

    // 5. Reject Invalid Media URL in Download Request
    test("MessageValidator: Rejects YouTube HTML watch page as download stream", () => {
        try {
            MessageValidator.validateIncomingMessage({
                action: ActionNames.DOWNLOAD_REQUEST,
                url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
            });
            throw new Error("Should have rejected HTML watch URL");
        } catch (err) {
            if (err.code !== ErrorCodes.INVALID_MEDIA_URL) throw err;
        }
    });

    return results;
}
