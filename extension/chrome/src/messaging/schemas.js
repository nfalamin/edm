/**
 * EDM Extension - Message Schema Validation & Allowlist
 * Enforces strict structure, type-checking, protocol versioning, and allowlist security.
 */

import { PROTOCOL_VERSION, ActionNames } from '../core/constants.js';
import { ErrorCodes, EdmError } from '../core/errors.js';
import { SecurityValidator } from '../security/validator.js';

export const AllowedActions = Object.freeze(new Set([
    ActionNames.PING,
    ActionNames.PONG,
    ActionNames.GET_MEDIA_VARIANTS,
    ActionNames.MEDIA_VARIANTS_RESOLVED,
    ActionNames.GET_TAB_CAPTURED_MEDIA,
    ActionNames.START_EDM_DOWNLOAD,
    ActionNames.DOWNLOAD_REQUEST,
    ActionNames.DOWNLOAD_STATUS,
    "PING_EDM"
]));

export class MessageValidator {
    static validateIncomingMessage(msg, sender = null) {
        if (!msg || typeof msg !== 'object') {
            throw new EdmError(ErrorCodes.INVALID_MESSAGE, "Message payload must be a non-null JSON object.");
        }

        const action = msg.action || msg.type;
        if (!action || typeof action !== 'string') {
            throw new EdmError(ErrorCodes.INVALID_MESSAGE, "Message is missing a valid 'action' identifier.");
        }

        if (!AllowedActions.has(action)) {
            throw new EdmError(ErrorCodes.UNAUTHORIZED_MESSAGE, `Action '${action}' is not in the approved message allowlist.`);
        }

        // Protocol version check if supplied
        if (msg.protocolVersion && msg.protocolVersion !== PROTOCOL_VERSION) {
            throw new EdmError(
                ErrorCodes.VERSION_MISMATCH,
                `Protocol version '${msg.protocolVersion}' is incompatible with extension protocol '${PROTOCOL_VERSION}'.`
            );
        }

        // Action-specific schema validations
        switch (action) {
            case ActionNames.GET_MEDIA_VARIANTS:
                MessageValidator.validateGetVariants(msg);
                break;
            case ActionNames.START_EDM_DOWNLOAD:
            case ActionNames.DOWNLOAD_REQUEST:
                MessageValidator.validateDownloadRequest(msg);
                break;
            case ActionNames.PING:
            case "PING_EDM":
            case ActionNames.GET_TAB_CAPTURED_MEDIA:
                // Minimal payload allowed
                break;
            default:
                break;
        }

        return true;
    }

    static validateGetVariants(msg) {
        if (!msg.url || typeof msg.url !== 'string') {
            throw new EdmError(ErrorCodes.INVALID_PAYLOAD, "GET_MEDIA_VARIANTS requires a valid 'url' string parameter.");
        }
    }

    static validateDownloadRequest(msg) {
        const url = msg.url || msg.videoUrl;
        if (!url || typeof url !== 'string' || !SecurityValidator.isValidMediaUrl(url)) {
            throw new EdmError(ErrorCodes.INVALID_MEDIA_URL, "Download request contains an invalid or unsafe media URL.");
        }
    }

    static validateNativeResponse(response, expectedRequestId = null) {
        if (!response || typeof response !== 'object') {
            throw new EdmError(ErrorCodes.INVALID_PAYLOAD, "Received malformed or null response from NativeHost.");
        }

        if (expectedRequestId && response.requestId && response.requestId !== expectedRequestId) {
            throw new EdmError(
                ErrorCodes.INVALID_MESSAGE,
                `RequestId mismatch: expected '${expectedRequestId}', got '${response.requestId}'.`
            );
        }

        return true;
    }
}
