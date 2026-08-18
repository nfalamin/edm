/**
 * EDM Extension - Centralized Message Router & Dispatch Bus
 * Handles all internal message routing with validation, request IDs, timeouts, and cancellation.
 */

import { PROTOCOL_VERSION, EXTENSION_VERSION } from '../core/constants.js';
import { ErrorCodes, EdmError } from '../core/errors.js';
import { MessageValidator } from './schemas.js';
import { Logger } from '../core/logger.js';

export class MessageRouter {
    constructor() {
        this.handlers = new Map(); // action -> async (message, sender) => response
        this.pendingRequests = new Map(); // requestId -> { resolve, reject, timer, timestamp, action }
        this.recentRequestDeduplication = new Map(); // hash -> timestamp
        this.init();
    }

    init() {
        if (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.onMessage) {
            chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
                this.routeIncomingMessage(message, sender)
                    .then(response => sendResponse(response))
                    .catch(err => {
                        const edmErr = EdmError.fromException(err, ErrorCodes.INTERNAL_ERROR, message?.requestId);
                        Logger.error(`[MessageRouter] Routing failed for action '${message?.action}':`, edmErr.message);
                        sendResponse(edmErr.toResponse());
                    });
                return true; // Keep async response channel open
            });
        }
    }

    registerHandler(action, handler) {
        if (!action || typeof handler !== 'function') {
            throw new Error("Invalid handler registration.");
        }
        this.handlers.set(action, handler);
        Logger.debug(`[MessageRouter] Registered handler for action '${action}'`);
    }

    async routeIncomingMessage(message, sender) {
        const startTime = Date.now();
        const requestId = message?.requestId || `edm_msg_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;

        try {
            // 1. Validate Schema & Allowlist
            MessageValidator.validateIncomingMessage(message, sender);

            const action = message.action || message.type;
            const handler = this.handlers.get(action);

            if (!handler) {
                throw new EdmError(ErrorCodes.UNKNOWN_REQUEST, `No handler registered for action '${action}'.`, null, requestId);
            }

            // 2. Deduplicate Rapid Duplicate Submissions (500ms window)
            const dedupKey = `${action}_${message.url || message.videoUrl || ''}_${sender?.tab?.id || ''}`;
            const lastTime = this.recentRequestDeduplication.get(dedupKey);
            if (lastTime && (startTime - lastTime) < 400 && action.startsWith("START_")) {
                Logger.info(`[MessageRouter] Suppressed duplicate rapid action '${action}' (dedupKey: ${dedupKey})`);
                return { success: true, status: "deduplicated", requestId };
            }
            this.recentRequestDeduplication.set(dedupKey, startTime);

            // Cleanup old dedup keys
            if (this.recentRequestDeduplication.size > 200) {
                const cutoff = startTime - 5000;
                for (const [k, t] of this.recentRequestDeduplication.entries()) {
                    if (t < cutoff) this.recentRequestDeduplication.delete(k);
                }
            }

            // 3. Execute Handler
            Logger.debug(`[MessageRouter] Dispatching action '${action}' [requestId: ${requestId}]`);
            const result = await handler(message, sender);

            const durationMs = Date.now() - startTime;
            Logger.debug(`[MessageRouter] Completed action '${action}' in ${durationMs}ms [requestId: ${requestId}]`);

            if (result && typeof result === 'object' && !('requestId' in result)) {
                result.requestId = requestId;
                result.protocolVersion = PROTOCOL_VERSION;
            }

            return result || { success: true, requestId };
        } catch (err) {
            const edmErr = EdmError.fromException(err, ErrorCodes.INTERNAL_ERROR, requestId);
            return edmErr.toResponse();
        }
    }

    createRequest(action, payload = {}, timeoutMs = 6000) {
        const requestId = `edm_req_${Date.now()}_${Math.random().toString(36).substr(2, 7)}`;

        const requestEnvelope = {
            ...payload,
            action,
            requestId,
            protocolVersion: PROTOCOL_VERSION,
            extensionVersion: EXTENSION_VERSION,
            timestamp: new Date().toISOString()
        };

        return {
            envelope: requestEnvelope,
            requestId,
            promise: new Promise((resolve, reject) => {
                const timer = setTimeout(() => {
                    if (this.pendingRequests.has(requestId)) {
                        this.pendingRequests.delete(requestId);
                        reject(new EdmError(ErrorCodes.REQUEST_TIMEOUT, `Request '${action}' timed out after ${timeoutMs}ms.`, null, requestId));
                    }
                }, timeoutMs);

                this.pendingRequests.set(requestId, {
                    resolve,
                    reject,
                    timer,
                    timestamp: Date.now(),
                    action
                });
            })
        };
    }

    cancelRequest(requestId, reason = "Request was cancelled by caller.") {
        const pending = this.pendingRequests.get(requestId);
        if (pending) {
            clearTimeout(pending.timer);
            this.pendingRequests.delete(requestId);
            pending.reject(new EdmError(ErrorCodes.REQUEST_CANCELLED, reason, null, requestId));
            Logger.info(`[MessageRouter] Cancelled request [requestId: ${requestId}]: ${reason}`);
            return true;
        }
        return false;
    }

    settlePendingRequest(requestId, response) {
        const pending = this.pendingRequests.get(requestId);
        if (pending) {
            clearTimeout(pending.timer);
            this.pendingRequests.delete(requestId);
            pending.resolve(response);
            return true;
        }
        // Late response received for timed out or cancelled request -> ignore cleanly
        Logger.debug(`[MessageRouter] Ignored late or unmatched response for [requestId: ${requestId}]`);
        return false;
    }
}
