// EDM Control Plane — Centralized API Client Module

const API_BASE_URL = window.location.origin.includes('localhost') || window.location.origin.includes('127.0.0.1')
    ? window.location.origin
    : 'https://control.edm.local';

let accessToken = sessionStorage.getItem('edm_access_token') || null;
let refreshToken = localStorage.getItem('edm_refresh_token') || null;

export function getApiBaseUrl() {
    return API_BASE_URL;
}

export function setTokens(access, refresh) {
    accessToken = access;
    refreshToken = refresh;
    if (access) sessionStorage.setItem('edm_access_token', access);
    else sessionStorage.removeItem('edm_access_token');

    if (refresh) localStorage.setItem('edm_refresh_token', refresh);
    else localStorage.removeItem('edm_refresh_token');
}

export function clearTokens() {
    setTokens(null, null);
}

export function getAccessToken() {
    return accessToken;
}

export async function apiFetch(endpoint, options = {}) {
    const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : '/' + endpoint}`;
    const headers = options.headers || {};

    headers['Content-Type'] = headers['Content-Type'] || 'application/json';

    if (accessToken) {
        headers['Authorization'] = `Bearer ${accessToken}`;
    }

    try {
        let response = await fetch(url, { ...options, headers });

        // If 401 Unauthorized, attempt refresh-token flow once
        if (response.status === 401 && refreshToken) {
            const refreshed = await attemptTokenRefresh();
            if (refreshed) {
                headers['Authorization'] = `Bearer ${accessToken}`;
                response = await fetch(url, { ...options, headers });
            } else {
                clearTokens();
                window.dispatchEvent(new CustomEvent('edm:auth-expired'));
                throw new Error('Session expired. Please sign in again.');
            }
        }

        if (response.status === 429) {
            showToast('Rate limit exceeded. Please wait a moment.', 'error');
            throw new Error('RATE_LIMITED');
        }

        if (!response.ok) {
            const errorBody = await response.json().catch(() => ({ message: response.statusText }));
            const msg = errorBody.message || errorBody.error || `HTTP ${response.status}`;
            throw new Error(msg);
        }

        // Return JSON or text
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            return await response.json();
        }
        return await response.text();
    } catch (err) {
        console.error(`[API Error] ${endpoint}:`, err.message);
        throw err;
    }
}

async function attemptTokenRefresh() {
    if (!refreshToken) return false;
    try {
        const res = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken })
        });
        if (res.ok) {
            const data = await res.json();
            setTokens(data.accessToken, data.refreshToken);
            return true;
        }
    } catch {
        // Refresh failed
    }
    return false;
}

export function showToast(message, type = 'success') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.remove();
    }, 4000);
}
