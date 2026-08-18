/**
 * EDM Extension - Deterministic Active Player Scorer
 * Version: 1.0.0
 * Calculates activeScore and confidence tier based on multi-signal observable browser evidence.
 */

import { ConfidenceLevel } from './media-candidate.js';

export class ActivePlayerScorer {
    /**
     * Computes the deterministic active player score (0 to 100) and confidence tier.
     * @param {Object} candidate - Candidate object or DOM video representation
     * @returns {{ score: number, confidence: string, breakdown: Object }}
     */
    static calculateScore(candidate) {
        let score = 0;
        const breakdown = {};

        const width = candidate.dimensions?.width || 0;
        const height = candidate.dimensions?.height || 0;
        const isVisible = candidate.isVisible !== false;
        const isPlaying = !!candidate.isPlaying;
        const isMuted = !!candidate.isMuted;
        const isLooping = !!candidate.isLooping;
        const isAutoplay = !!candidate.isAutoplay;
        const duration = candidate.duration || 0;
        const currentTime = candidate.currentTime || 0;

        // ── 1. HARD DISQUALIFIERS (Score = 0, Confidence = LOW) ──
        if (!isVisible || width <= 0 || height <= 0) {
            return {
                score: 0,
                confidence: ConfidenceLevel.LOW,
                breakdown: { disqualifier: "HIDDEN_OR_ZERO_DIMENSIONS" }
            };
        }

        // Reject tiny thumbnail cards / preview hover widgets (< 160x100)
        if (width < 160 || height < 100) {
            return {
                score: 5,
                confidence: ConfidenceLevel.LOW,
                breakdown: { penalty: "TINY_THUMBNAIL_PREVIEW", width, height }
            };
        }

        // ── 2. POSITIVE PLAYBACK SIGNALS (Up to +50) ──
        if (isPlaying) {
            score += 40;
            breakdown.activePlayback = +40;

            if (currentTime > 0.5) {
                score += 10;
                breakdown.timeAdvancing = +10;
            }
        } else if (candidate.playState === 'buffering') {
            score += 25;
            breakdown.buffering = +25;
        } else if (currentTime > 0) {
            score += 15; // Paused after active playback
            breakdown.pausedWithProgress = +15;
        }

        // ── 3. POSITIVE GEOMETRIC & VIEWPORT SIGNALS (Up to +35) ──
        if (width >= 480 && height >= 270) {
            score += 20; // Full standard 16:9 player
            breakdown.standardLargePlayer = +20;
        } else if (width >= 320 && height >= 180) {
            score += 15; // Medium player
            breakdown.mediumPlayer = +15;
        } else {
            score += 5;
            breakdown.smallPlayer = +5;
        }

        if (candidate.viewportRatio >= 0.5) {
            score += 15; // High viewport prominence
            breakdown.viewportProminence = +15;
        } else if (candidate.viewportRatio > 0.1) {
            score += 8;
            breakdown.partialViewport = +8;
        }

        // ── 4. AUDIO & INTERACTION SIGNALS (Up to +15) ──
        if (!isMuted && candidate.volume > 0) {
            score += 10;
            breakdown.unmutedAudio = +10;
        }

        if (duration >= 15) {
            score += 5; // Standard length media
            breakdown.standardDuration = +5;
        }

        // ── 5. PENALTIES (Decorative Backgrounds & Ads) ──
        // Decorative background loopers (Muted + Autoplay + Loop + Zero User Controls)
        if (isMuted && isLooping && isAutoplay && candidate.hasControls === false) {
            score -= 35;
            breakdown.decorativeBackgroundPenalty = -35;
        }

        // Very short bumper / teaser / micro-clip (< 6 seconds)
        if (duration > 0 && duration <= 6 && isLooping) {
            score -= 25;
            breakdown.shortTeaserPenalty = -25;
        }

        // Clamp final score to [0, 100]
        score = Math.max(0, Math.min(100, score));

        // ── 6. CONFIDENCE CLASSIFICATION ──
        let confidence = ConfidenceLevel.LOW;
        if (score >= 70) {
            confidence = ConfidenceLevel.HIGH;
        } else if (score >= 40) {
            confidence = ConfidenceLevel.MEDIUM;
        } else if (score >= 20) {
            confidence = ConfidenceLevel.LOW;
        } else {
            confidence = ConfidenceLevel.LOW;
        }

        return { score, confidence, breakdown };
    }

    /**
     * Evaluates a collection of media candidates and selects the primary active player.
     * Returns AMBIGUOUS if two competing candidates have identical high scores.
     * @param {Array<MediaCandidate>} candidates
     * @returns {{ selected: MediaCandidate|null, status: string }}
     */
    static selectPrimaryCandidate(candidates) {
        if (!Array.isArray(candidates) || candidates.length === 0) {
            return { selected: null, status: "NO_CANDIDATES" };
        }

        // Calculate scores for all candidates
        candidates.forEach(cand => {
            const result = ActivePlayerScorer.calculateScore(cand);
            cand.activeScore = result.score;
            cand.confidence = result.confidence;
        });

        // Filter candidates with minimum viable score
        const viable = candidates.filter(c => c.activeScore >= 35);
        if (viable.length === 0) {
            return { selected: null, status: "NO_VIABLE_CANDIDATE" };
        }

        // Sort descending by score, then area, then currentTime
        viable.sort((a, b) => {
            if (b.activeScore !== a.activeScore) return b.activeScore - a.activeScore;
            const areaA = (a.dimensions.width || 0) * (a.dimensions.height || 0);
            const areaB = (b.dimensions.width || 0) * (b.dimensions.height || 0);
            if (areaB !== areaA) return areaB - areaA;
            return (b.currentTime || 0) - (a.currentTime || 0);
        });

        const top = viable[0];

        // Check for ambiguous competing top candidates
        if (viable.length > 1) {
            const runnerUp = viable[1];
            if (top.activeScore === runnerUp.activeScore && top.isPlaying === runnerUp.isPlaying) {
                // If scores and play states are identical, mark as AMBIGUOUS
                top.confidence = ConfidenceLevel.AMBIGUOUS;
                return { selected: top, status: "AMBIGUOUS_SELECTION" };
            }
        }

        return { selected: top, status: "PRIMARY_SELECTED" };
    }
}
