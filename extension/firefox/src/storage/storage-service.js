/**
 * EDM Extension - Versioned Storage Service
 * Manages storage schema migrations, key namespaces, and session/local fallback.
 */

import { Logger } from '../core/logger.js';

export const CURRENT_STORAGE_SCHEMA_VERSION = 1;

export class StorageService {
    static async get(key, defaultValue = null) {
        try {
            if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
                const result = await chrome.storage.local.get(key);
                return (result && key in result) ? result[key] : defaultValue;
            }
        } catch (e) {
            Logger.warn(`[StorageService] Failed to read key '${key}':`, e);
        }
        return defaultValue;
    }

    static async set(key, value) {
        try {
            if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
                await chrome.storage.local.set({ [key]: value });
                return true;
            }
        } catch (e) {
            Logger.warn(`[StorageService] Failed to set key '${key}':`, e);
        }
        return false;
    }

    static async remove(key) {
        try {
            if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
                await chrome.storage.local.remove(key);
                return true;
            }
        } catch (e) {
            Logger.warn(`[StorageService] Failed to remove key '${key}':`, e);
        }
        return false;
    }

    static async runMigrations() {
        const storedVersion = await StorageService.get("storageSchemaVersion", 0);

        if (storedVersion < CURRENT_STORAGE_SCHEMA_VERSION) {
            Logger.info(`[StorageService] Migrating storage schema: v${storedVersion} -> v${CURRENT_STORAGE_SCHEMA_VERSION}`);

            if (storedVersion === 0) {
                // Initial schema initialization
                await StorageService.set("extensionSettings", {
                    autoCaptureDownloads: true,
                    showFloatingButton: true,
                    bypassAltKey: true,
                    notificationsEnabled: true
                });
            }

            await StorageService.set("storageSchemaVersion", CURRENT_STORAGE_SCHEMA_VERSION);
            Logger.info(`[StorageService] Storage migration complete.`);
        }
    }
}
