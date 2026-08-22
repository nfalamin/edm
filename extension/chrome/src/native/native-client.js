/**
 * EDM Extension - Native Messaging Client
 * Handles standard I/O 32-bit LE binary framed communication with EDM.NativeHost.exe.
 */

import { NATIVE_HOST_NAME, HANDOFF_TIMEOUT_MS } from '../core/constants.js';
import { Logger } from '../core/logger.js';

export class NativeClient {
    static async sendMessage(message, timeoutMs = HANDOFF_TIMEOUT_MS) {
        return new Promise((resolve, reject) => {
            if (typeof chrome === 'undefined' || !chrome.runtime || !chrome.runtime.sendNativeMessage) {
                return reject(new Error("Native messaging API is not available in this context."));
            }

            let completed = false;
            const timer = setTimeout(() => {
                if (!completed) {
                    completed = true;
                    reject(new Error(`Native messaging request timed out after ${timeoutMs}ms.`));
                }
            }, timeoutMs);

            try {
                chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, message, (response) => {
                    clearTimeout(timer);
                    if (completed) return;
                    completed = true;

                    if (chrome.runtime.lastError) {
                        Logger.warn("Native messaging lastError:", chrome.runtime.lastError.message);
                        reject(new Error(chrome.runtime.lastError.message));
                    } else {
                        resolve(response || { success: true, status: "acknowledged" });
                    }
                });
            } catch (err) {
                clearTimeout(timer);
                if (!completed) {
                    completed = true;
                    reject(err);
                }
            }
        });
    }

    static async ping() {
        return NativeClient.sendMessage({ action: "PING" }, 2500);
    }
}
