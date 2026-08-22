/**
 * EDM Extension - Active Playing Media Detector
 * Distinguishes ACTIVE PLAYING MEDIA from thumbnails, previews, video cards, and background muted teasers.
 */

export class ActivePlayerTracker {
    constructor(onActivePlayerChanged) {
        this.onActivePlayerChanged = onActivePlayerChanged;
        this.trackedVideos = new Map(); // video element -> state
        this.activePlayer = null;
        this.intersectionObserver = null;
        this.init();
    }

    init() {
        if (typeof IntersectionObserver !== 'undefined') {
            this.intersectionObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    const vState = this.trackedVideos.get(entry.target);
                    if (vState) {
                        vState.isVisible = entry.isIntersecting && entry.intersectionRatio >= 0.4;
                        this.evaluateActivePlayer();
                    }
                });
            }, { threshold: [0.0, 0.4, 0.8] });
        }
    }

    trackVideo(videoElement, containerElement) {
        if (!videoElement || this.trackedVideos.has(videoElement)) return;

        const state = {
            element: videoElement,
            container: containerElement || videoElement.parentElement || videoElement,
            isPlaying: !videoElement.paused && videoElement.currentTime > 0 && !videoElement.ended,
            isVisible: true,
            isUserEngaged: false
        };

        this.trackedVideos.set(videoElement, state);

        if (this.intersectionObserver) {
            this.intersectionObserver.observe(videoElement);
        }

        const handlePlay = () => {
            state.isPlaying = true;
            this.evaluateActivePlayer();
        };

        const handlePause = () => {
            state.isPlaying = false;
            this.evaluateActivePlayer();
        };

        const handleEnded = () => {
            state.isPlaying = false;
            this.evaluateActivePlayer();
        };

        videoElement.addEventListener('play', handlePlay);
        videoElement.addEventListener('playing', handlePlay);
        videoElement.addEventListener('pause', handlePause);
        videoElement.addEventListener('ended', handleEnded);

        state.cleanup = () => {
            videoElement.removeEventListener('play', handlePlay);
            videoElement.removeEventListener('playing', handlePlay);
            videoElement.removeEventListener('pause', handlePause);
            videoElement.removeEventListener('ended', handleEnded);
            if (this.intersectionObserver) {
                this.intersectionObserver.unobserve(videoElement);
            }
        };

        this.evaluateActivePlayer();
    }

    untrackVideo(videoElement) {
        const state = this.trackedVideos.get(videoElement);
        if (state) {
            if (state.cleanup) state.cleanup();
            this.trackedVideos.delete(videoElement);
            this.evaluateActivePlayer();
        }
    }

    evaluateActivePlayer() {
        let bestCandidate = null;

        for (const [video, state] of this.trackedVideos.entries()) {
            const rect = video.getBoundingClientRect();
            // Ignore tiny video cards / thumbnails (< 160x100)
            if (rect.width < 160 || rect.height < 100) continue;

            // Priority: Playing + Visible + Largest area
            if (state.isPlaying && state.isVisible) {
                const area = rect.width * rect.height;
                if (!bestCandidate || area > (bestCandidate.element.getBoundingClientRect().width * bestCandidate.element.getBoundingClientRect().height)) {
                    bestCandidate = state;
                }
            }
        }

        if (bestCandidate !== this.activePlayer) {
            this.activePlayer = bestCandidate;
            if (this.onActivePlayerChanged) {
                this.onActivePlayerChanged(this.activePlayer);
            }
        }
    }

    destroy() {
        for (const [video, state] of this.trackedVideos.entries()) {
            if (state.cleanup) state.cleanup();
        }
        this.trackedVideos.clear();
        if (this.intersectionObserver) {
            this.intersectionObserver.disconnect();
        }
    }
}
