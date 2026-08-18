/**
 * EDM (Exclusive Download Manager) - Main-World YouTube Stream Extractor Bridge
 * Injected in the MAIN execution context of YouTube pages to capture all real video/audio formats,
 * resolutions (4K, 2K, 1080p, 720p, 480p, 360p, 240p, 144p), codecs, bitrates, and streams.
 */

(function() {
    'use strict';

    let lastVideoId = '';
    let lastPlayerResponse = null;

    function extractVideoId(url) {
        try {
            const u = new URL(url || window.location.href);
            if (u.hostname.includes('youtube.com')) {
                return u.searchParams.get('v') || '';
            } else if (u.hostname.includes('youtu.be')) {
                return u.pathname.substring(1).split('?')[0];
            }
        } catch (e) {}
        return '';
    }

    function getPlayerResponse() {
        let pr = null;

        // 1. YouTube movie_player component
        const moviePlayer = document.getElementById('movie_player');
        if (moviePlayer && typeof moviePlayer.getPlayerResponse === 'function') {
            try {
                pr = moviePlayer.getPlayerResponse();
                if (pr && pr.streamingData) return pr;
            } catch (e) {}
        }

        // 2. Polymer ytd-watch-flexy component
        const watchFlexy = document.querySelector('ytd-watch-flexy');
        if (watchFlexy && watchFlexy.playerData) {
            try {
                pr = watchFlexy.playerData;
                if (pr && pr.streamingData) return pr;
            } catch (e) {}
        }

        // 3. Global window.ytInitialPlayerResponse
        if (window.ytInitialPlayerResponse && window.ytInitialPlayerResponse.streamingData) {
            return window.ytInitialPlayerResponse;
        }

        // 4. ytplayer config args
        if (window.ytplayer && window.ytplayer.config && window.ytplayer.config.args) {
            const rawPr = window.ytplayer.config.args.raw_player_response;
            if (rawPr) {
                if (typeof rawPr === 'object' && rawPr.streamingData) return rawPr;
                if (typeof rawPr === 'string') {
                    try {
                        const parsed = JSON.parse(rawPr);
                        if (parsed && parsed.streamingData) return parsed;
                    } catch (e) {}
                }
            }
        }

        return pr;
    }

    function broadcastPlayerData(force = false) {
        const currentUrl = window.location.href;
        const currentId = extractVideoId(currentUrl);

        const pr = getPlayerResponse();
        if (pr && pr.streamingData) {
            if (force || currentId !== lastVideoId || pr !== lastPlayerResponse) {
                lastVideoId = currentId;
                lastPlayerResponse = pr;

                window.postMessage({
                    type: '__EDM_YT_PLAYER_DATA_RESPONSE__',
                    playerResponse: pr,
                    videoId: currentId,
                    url: currentUrl,
                    title: pr.videoDetails?.title || document.title.replace(' - YouTube', '').trim()
                }, '*');
            }
        }
    }

    // Intercept YouTube Internal Fetch API for player responses
    const originalFetch = window.fetch;
    window.fetch = async function(...args) {
        const response = await originalFetch.apply(this, args);
        try {
            const url = args[0] ? (typeof args[0] === 'string' ? args[0] : args[0].url) : '';
            if (url && (url.includes('/youtubei/v1/player') || url.includes('/v1/player'))) {
                const clone = response.clone();
                clone.json().then(data => {
                    if (data && data.streamingData) {
                        lastPlayerResponse = data;
                        broadcastPlayerData(true);
                    }
                }).catch(() => {});
            }
        } catch (e) {}
        return response;
    };

    // Intercept XHR for player responses
    const originalOpen = XMLHttpRequest.prototype.open;
    const originalSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function(method, url, ...rest) {
        this._edmUrl = url;
        return originalOpen.apply(this, [method, url, ...rest]);
    };
    XMLHttpRequest.prototype.send = function(...args) {
        if (this._edmUrl && (this._edmUrl.includes('/youtubei/v1/player') || this._edmUrl.includes('/v1/player'))) {
            this.addEventListener('load', function() {
                try {
                    const data = JSON.parse(this.responseText);
                    if (data && data.streamingData) {
                        lastPlayerResponse = data;
                        broadcastPlayerData(true);
                    }
                } catch (e) {}
            });
        }
        return originalSend.apply(this, args);
    };

    // Listen for requests from content script
    window.addEventListener('message', function(e) {
        if (e.data && e.data.type === '__EDM_REQUEST_YT_PLAYER_DATA__' || e.data.type === '__EDM_REQUEST_YT_PLAYER_DATA__') {
            broadcastPlayerData(true);
        }
    });

    // YouTube SPA navigation and lifecycle events
    window.addEventListener('yt-navigate-finish', () => {
        setTimeout(() => broadcastPlayerData(true), 250);
        setTimeout(() => broadcastPlayerData(true), 1000);
    });

    window.addEventListener('yt-player-updated', () => {
        setTimeout(() => broadcastPlayerData(true), 200);
    });

    // Initial and periodic checks
    broadcastPlayerData();
    setTimeout(() => broadcastPlayerData(true), 400);
    setTimeout(() => broadcastPlayerData(true), 1200);
    setTimeout(() => broadcastPlayerData(true), 2500);
})();
