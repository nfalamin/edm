/**
 * EDM Extension - Player Media Session Lifecycle Manager
 * Version: 1.0.0
 * Manages the deterministic lifecycle of a media session and enforces legal state transitions.
 */

import { SessionState } from './media-candidate.js';
import { Logger } from '../core/logger.js';

export class PlayerSession {
    constructor(candidate) {
        this.sessionId = `sess_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`;
        this.candidateId = candidate.candidateId;
        this.candidate = candidate;
        this.state = SessionState.DISCOVERED;
        this.startTime = Date.now();
        this.lastSeen = Date.now();
        this.lastActive = Date.now();
        this.listeners = new Set();
    }

    getState() {
        return this.state;
    }

    transitionTo(newState, reason = "") {
        const oldState = this.state;
        if (oldState === newState) return true;

        // Legal state transitions:
        // DISCOVERED -> ACTIVE, PAUSED, INACTIVE, DESTROYED
        // ACTIVE -> PAUSED, INACTIVE, ENDED, DESTROYED
        // PAUSED -> ACTIVE, INACTIVE, ENDED, DESTROYED
        // INACTIVE -> ACTIVE, PAUSED, DESTROYED
        // ENDED -> ACTIVE, DESTROYED
        // DESTROYED -> (terminal state, no transitions allowed)

        if (oldState === SessionState.DESTROYED) {
            Logger.warn(`[PlayerSession] Attempted invalid transition from terminal DESTROYED to ${newState}`);
            return false;
        }

        const isValid = (oldState, newState) => {
            switch (oldState) {
                case SessionState.DISCOVERED:
                    return [SessionState.ACTIVE, SessionState.PAUSED, SessionState.INACTIVE, SessionState.DESTROYED].includes(newState);
                case SessionState.ACTIVE:
                    return [SessionState.PAUSED, SessionState.INACTIVE, SessionState.ENDED, SessionState.DESTROYED].includes(newState);
                case SessionState.PAUSED:
                    return [SessionState.ACTIVE, SessionState.INACTIVE, SessionState.ENDED, SessionState.DESTROYED].includes(newState);
                case SessionState.INACTIVE:
                    return [SessionState.ACTIVE, SessionState.PAUSED, SessionState.DESTROYED].includes(newState);
                case SessionState.ENDED:
                    return [SessionState.ACTIVE, SessionState.DESTROYED].includes(newState);
                default:
                    return false;
            }
        };

        if (!isValid(oldState, newState)) {
            Logger.warn(`[PlayerSession] Illegal state transition: ${oldState} -> ${newState}`);
            return false;
        }

        this.state = newState;
        this.lastSeen = Date.now();
        if (newState === SessionState.ACTIVE) {
            this.lastActive = Date.now();
        }

        Logger.debug(`[PlayerSession] Session ${this.sessionId} transition: ${oldState} -> ${newState} ${reason ? '(' + reason + ')' : ''}`);
        this.notifyStateChanged(oldState, newState, reason);
        return true;
    }

    subscribe(callback) {
        if (typeof callback === 'function') {
            this.listeners.add(callback);
        }
    }

    unsubscribe(callback) {
        this.listeners.delete(callback);
    }

    notifyStateChanged(oldState, newState, reason) {
        for (const cb of this.listeners) {
            try {
                cb({ sessionId: this.sessionId, oldState, newState, reason, session: this });
            } catch (e) {
                Logger.warn("[PlayerSession] Error in session state subscriber:", e);
            }
        }
    }

    destroy(reason = "Element destroyed") {
        this.transitionTo(SessionState.DESTROYED, reason);
        this.listeners.clear();
    }
}
