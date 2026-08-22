/**
 * EDM Extension - SPA Navigation & Dynamic Route Watcher
 * Version: 1.0.0
 * Monitors single-page application route transitions and purges stale media sessions.
 */

import { Logger } from '../core/logger.js';

export class SpaWatcher {
    constructor(onRouteChange) {
        this.onRouteChange = onRouteChange;
        this.lastUrl = typeof window !== 'undefined' ? window.location.href : '';
        this.debounceTimer = null;
        this.init();
    }

    init() {
        if (typeof window === 'undefined') return;

        // 1. Intercept History API (pushState & replaceState)
        const originalPushState = history.pushState;
        const originalReplaceState = history.replaceState;

        history.pushState = (...args) => {
            const result = originalPushState.apply(history, args);
            this.handlePossibleRouteChange('pushState');
            return result;
        };

        history.replaceState = (...args) => {
            const result = originalReplaceState.apply(history, args);
            this.handlePossibleRouteChange('replaceState');
            return result;
        };

        // 2. Popstate event (Back / Forward navigation)
        window.addEventListener('popstate', () => {
            this.handlePossibleRouteChange('popstate');
        });

        // 3. YouTube custom navigation finish event
        window.addEventListener('yt-navigate-finish', () => {
            this.handlePossibleRouteChange('yt-navigate-finish');
        });

        // 4. Generic hashchange
        window.addEventListener('hashchange', () => {
            this.handlePossibleRouteChange('hashchange');
        });
    }

    handlePossibleRouteChange(source) {
        const currentUrl = window.location.href;
        if (currentUrl === this.lastUrl && source !== 'yt-navigate-finish') {
            return;
        }

        this.lastUrl = currentUrl;
        Logger.info(`[SpaWatcher] Route change detected via '${source}': ${currentUrl}`);

        if (this.debounceTimer) clearTimeout(this.debounceTimer);
        this.debounceTimer = setTimeout(() => {
            if (this.onRouteChange) {
                this.onRouteChange(currentUrl, source);
            }
        }, 150);
    }
}
