/**
 * EDM Extension - Standardized Error Model
 * Defines structured error codes and serialized error objects for all communication layers.
 */

export const ErrorCodes = Object.freeze({
    INVALID_MESSAGE: "INVALID_MESSAGE",
    INVALID_PAYLOAD: "INVALID_PAYLOAD",
    UNAUTHORIZED_MESSAGE: "UNAUTHORIZED_MESSAGE",
    VERSION_MISMATCH: "VERSION_MISMATCH",
    NATIVE_HOST_UNAVAILABLE: "NATIVE_HOST_UNAVAILABLE",
    NATIVE_HOST_DISCONNECTED: "NATIVE_HOST_DISCONNECTED",
    REQUEST_TIMEOUT: "REQUEST_TIMEOUT",
    REQUEST_CANCELLED: "REQUEST_CANCELLED",
    UNKNOWN_REQUEST: "UNKNOWN_REQUEST",
    INTERNAL_ERROR: "INTERNAL_ERROR",
    FORMAT_EXTRACTION_FAILED: "FORMAT_EXTRACTION_FAILED",
    INVALID_MEDIA_URL: "INVALID_MEDIA_URL",
    EDM_UNAVAILABLE: "EDM_UNAVAILABLE"
});

export class EdmError extends Error {
    constructor(code, message, details = null, requestId = null) {
        super(message);
        this.name = "EdmError";
        this.code = code || ErrorCodes.INTERNAL_ERROR;
        this.details = details;
        this.requestId = requestId;
        this.timestamp = new Date().toISOString();
    }

    toResponse() {
        return {
            success: false,
            errorCode: this.code,
            error: this.message,
            details: this.details,
            requestId: this.requestId,
            timestamp: this.timestamp
        };
    }

    static fromException(err, defaultCode = ErrorCodes.INTERNAL_ERROR, requestId = null) {
        if (err instanceof EdmError) {
            return err;
        }
        return new EdmError(
            defaultCode,
            err?.message || "An unexpected error occurred in the EDM extension.",
            err?.stack || null,
            requestId
        );
    }
}
