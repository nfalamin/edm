/**
 * EDM Extension - MV3 State & Stream Session Persistence
 * Preserves tab media streams and correlation IDs across Service Worker idle recycles.
 */

import { Logger } from '../core/logger.js';

export class StateManager {
    static isSessionStorageAvailable() {
        return typeof chrome !== 'undefined' && chrome.storage && chrome.storage.session;
    }

    static isLocalStorageAvailable() {
        return typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local;
    }

    static async setTabMedia(tabId, streamKey, streamData) {
        if (tabId < 0 || !streamKey) return;
        const key = `tab_media_${tabId}_${encodeURIComponent(streamKey)}`;

        try {
            if (StateManager.isSessionStorageAvailable()) {
                await chrome.storage.session.set({ [key]: streamData });
            } else if (StateManager.isLocalStorageAvailable()) {
                await chrome.storage.local.set({ [key]: streamData });
            }
        } catch (e) {
            Logger.warn("Failed to persist tab media state:", e);
        }
    }

    static async getTabMedia(tabId) {
        if (tabId < 0) return [];
        const prefix = `tab_media_${tabId}_`;

        try {
            let all = {};
            if (StateManager.isSessionStorageAvailable()) {
                all = await chrome.storage.session.get(null);
            } else if (StateManager.isLocalStorageAvailable()) {
                all = await chrome.storage.local.get(null);
            }

            const results = [];
            for (const [k, v] of Object.entries(all)) {
                if (k.startsWith(prefix) && v) {
                    results.push(v);
                }
            }
            return results;
        } catch (e) {
            Logger.warn("Failed to retrieve tab media state:", e);
            return [];
        }
    }

    static async clearTabMedia(tabId) {
        if (tabId < 0) return;
        const prefix = `tab_media_${tabId}_`;

        try {
            let all = {};
            if (StateManager.isSessionStorageAvailable()) {
                all = await chrome.storage.session.get(null);
            } else if (StateManager.isLocalStorageAvailable()) {
                all = await chrome.storage.local.get(null);
            }

            const keysToRemove = Object.keys(all).filter(k => k.startsWith(prefix));
            if (keysToRemove.length > 0) {
                if (StateManager.isSessionStorageAvailable()) {
                    await chrome.storage.session.remove(keysToRemove);
                } else if (StateManager.isLocalStorageAvailable()) {
                    await chrome.storage.local.remove(keysToRemove);
                }
            }
        } catch (e) {
            Logger.warn("Failed to clear tab media state:", e);
        }
    }
}
