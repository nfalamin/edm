/**
 * EDM Extension - Centralized Lifecycle Coordinator
 * Coordinates service worker startup, installation events, tab cleanup, and graceful shutdowns.
 */

import { StorageService } from '../storage/storage-service.js';
import { StateManager } from '../background/state-manager.js';
import { Logger } from './logger.js';

export class LifecycleCoordinator {
    static init(onStartupCallback = null) {
        if (typeof chrome === 'undefined' || !chrome.runtime) return;

        // 1. Installation & Update Event
        if (chrome.runtime.onInstalled) {
            chrome.runtime.onInstalled.addListener(async (details) => {
                Logger.info(`[Lifecycle] Extension installed/updated. Reason: ${details.reason}, PreviousVersion: ${details.previousVersion || 'none'}`);
                try {
                    await StorageService.runMigrations();
                } catch (e) {
                    Logger.error("[Lifecycle] Storage migration error on install:", e);
                }
            });
        }

        // 2. Service Worker / Browser Startup Event
        if (chrome.runtime.onStartup) {
            chrome.runtime.onStartup.addListener(async () => {
                Logger.info("[Lifecycle] Browser startup event received.");
                try {
                    await StorageService.runMigrations();
                    if (onStartupCallback) onStartupCallback();
                } catch (e) {
                    Logger.error("[Lifecycle] Startup error:", e);
                }
            });
        }

        // 3. Tab Removal Cleanup
        if (chrome.tabs && chrome.tabs.onRemoved) {
            chrome.tabs.onRemoved.addListener(async (tabId) => {
                try {
                    await StateManager.clearTabMedia(tabId);
                } catch (e) {
                    Logger.warn(`[Lifecycle] Error clearing tab #${tabId} media state:`, e);
                }
            });
        }

        // Immediate migration check on worker execution
        StorageService.runMigrations().catch(e => Logger.warn("[Lifecycle] Initial migration check:", e));
    }
}
