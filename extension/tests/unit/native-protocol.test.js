/**
 * EDM Extension - Native Protocol v1 Unit Tests
 */

import { NativeProtocolV1 } from '../../src/native/protocol-v1.js';
import { ActionNames, PROTOCOL_VERSION, EXTENSION_VERSION } from '../../src/core/constants.js';

export function runNativeProtocolUnitTests() {
    const results = [];

    function test(name, fn) {
        try {
            fn();
            results.push({ name, status: "PASS" });
        } catch (err) {
            results.push({ name, status: "FAIL", error: err.message });
        }
    }

    test("NativeProtocolV1: Creates compliant PING request", () => {
        const req = NativeProtocolV1.createPingRequest();
        if (req.action !== ActionNames.PING) throw new Error("Action must be PING");
        if (req.protocolVersion !== PROTOCOL_VERSION) throw new Error("Version mismatch");
        if (req.extensionVersion !== EXTENSION_VERSION) throw new Error("Extension version mismatch");
        if (!req.timestamp) throw new Error("Missing timestamp");
    });

    test("NativeProtocolV1: Creates complete DOWNLOAD_REQUEST envelope", () => {
        const req = NativeProtocolV1.createDownloadRequest({
            url: "https://example.com/test.zip",
            filename: "test.zip",
            estimatedSizeBytes: 1048576
        });
        if (req.action !== ActionNames.DOWNLOAD_REQUEST) throw new Error("Action must be DOWNLOAD_REQUEST");
        if (req.url !== "https://example.com/test.zip") throw new Error("URL mismatch");
        if (req.filename !== "test.zip") throw new Error("Filename mismatch");
        if (!req.correlationId || !req.correlationId.startsWith("edm_corr_")) throw new Error("Invalid correlationId format");
    });

    return results;
}
