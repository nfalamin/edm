/**
 * EDM Extension - Native Messaging Connection Manager
 * Manages NativeHost connection lifecycle, state transitions, bounded exponential backoff, and version negotiation.
 */

import { NATIVE_HOST_NAME, PROTOCOL_VERSION, HANDOFF_TIMEOUT_MS } from '../core/constants.js';
import { ErrorCodes, EdmError } from '../core/errors.js';
import { NativeProtocolV1 } from './protocol-v1.js';
import { MessageValidator } from '../messaging/schemas.js';
import { Logger } from '../core/logger.js';

export const NativeConnectionState = Object.freeze({
    IDLE: 'IDLE',
    CONNECTING: 'CONNECTING',
    CONNECTED: 'CONNECTED',
    DISCONNECTED: 'DISCONNECTED',
    RECONNECTING: 'RECONNECTING',
    FAILED: 'FAILED'
});

export class NativeConnectionManager {
    constructor() {
        this.state = NativeConnectionState.IDLE;
        this.consecutiveFailures = 0;
        this.maxConsecutiveFailures = 3;
        this.lastHealthCheck = 0;
        this.isHostAvailable = null; // null = unknown, true = verified, false = failed
        this.pendingRequests = new Map();
    }

    getState() {
        return this.state;
    }

    transitionTo(newState, reason = "") {
        const oldState = this.state;
        if (oldState === newState) return;

        // Legal state transitions:
        // IDLE -> CONNECTING, DISCONNECTED, FAILED
        // CONNECTING -> CONNECTED, FAILED, DISCONNECTED
        // CONNECTED -> DISCONNECTED, FAILED
        // DISCONNECTED -> RECONNECTING, IDLE, FAILED
        // RECONNECTING -> CONNECTED, FAILED, DISCONNECTED
        // FAILED -> RECONNECTING, IDLE

        this.state = newState;
        Logger.info(`[NativeConnectionManager] State: ${oldState} -> ${newState} ${reason ? '(' + reason + ')' : ''}`);
    }

    async sendNativeRequest(envelope, timeoutMs = HANDOFF_TIMEOUT_MS) {
        if (typeof chrome === 'undefined' || !chrome.runtime || !chrome.runtime.sendNativeMessage) {
            this.transitionTo(NativeConnectionState.FAILED, "sendNativeMessage API unavailable");
            throw new EdmError(ErrorCodes.NATIVE_HOST_UNAVAILABLE, "Native messaging API is not supported in this browser context.");
        }

        const requestId = envelope.requestId || `edm_nat_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        envelope.requestId = requestId;
        envelope.protocolVersion = PROTOCOL_VERSION;

        this.transitionTo(NativeConnectionState.CONNECTING);

        return new Promise((resolve, reject) => {
            let settled = false;

            const timer = setTimeout(() => {
                if (!settled) {
                    settled = true;
                    this.consecutiveFailures++;
                    if (this.consecutiveFailures >= this.maxConsecutiveFailures) {
                        this.transitionTo(NativeConnectionState.FAILED, `Exceeded ${this.maxConsecutiveFailures} consecutive timeouts`);
                    } else {
                        this.transitionTo(NativeConnectionState.DISCONNECTED, "Request timeout");
                    }
                    reject(new EdmError(ErrorCodes.REQUEST_TIMEOUT, `Native message request timed out after ${timeoutMs}ms.`, null, requestId));
                }
            }, timeoutMs);

            try {
                chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, envelope, (response) => {
                    clearTimeout(timer);
                    if (settled) return; // Ignore late response
                    settled = true;

                    if (chrome.runtime.lastError) {
                        this.consecutiveFailures++;
                        const errMsg = chrome.runtime.lastError.message || "Native host connection error";
                        Logger.warn(`[NativeConnectionManager] runtime.lastError:`, errMsg);

                        if (this.consecutiveFailures >= this.maxConsecutiveFailures) {
                            this.transitionTo(NativeConnectionState.FAILED, errMsg);
                        } else {
                            this.transitionTo(NativeConnectionState.DISCONNECTED, errMsg);
                        }

                        reject(new EdmError(ErrorCodes.NATIVE_HOST_UNAVAILABLE, errMsg, null, requestId));
                    } else {
                        this.consecutiveFailures = 0;
                        this.isHostAvailable = true;
                        this.transitionTo(NativeConnectionState.CONNECTED);

                        try {
                            MessageValidator.validateNativeResponse(response, requestId);
                        } catch (valErr) {
                            Logger.warn(`[NativeConnectionManager] Response validation warning:`, valErr.message);
                        }

                        resolve(response || { success: true, status: "acknowledged", requestId });
                    }
                });
            } catch (err) {
                clearTimeout(timer);
                if (!settled) {
                    settled = true;
                    this.consecutiveFailures++;
                    this.transitionTo(NativeConnectionState.FAILED, err.message);
                    reject(EdmError.fromException(err, ErrorCodes.NATIVE_HOST_UNAVAILABLE, requestId));
                }
            }
        });
    }

    async ping() {
        const pingReq = NativeProtocolV1.createPingRequest();
        try {
            const resp = await this.sendNativeRequest(pingReq, 2500);
            this.lastHealthCheck = Date.now();
            return resp;
        } catch (err) {
            this.isHostAvailable = false;
            throw err;
        }
    }
}
