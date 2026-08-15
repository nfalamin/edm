// EDM Control Plane — Auth State Module
import { apiFetch, setTokens, clearTokens, showToast } from './api.js';

let currentUser = null;

export function getCurrentUser() {
    return currentUser;
}

export async function login(usernameOrEmail, password) {
    try {
        const res = await apiFetch('/api/v1/auth/login', {
            method: 'POST',
            body: JSON.stringify({ usernameOrEmail, password })
        });

        if (res.success && res.accessToken) {
            setTokens(res.accessToken, res.refreshToken);
            currentUser = res.user;
            updateUserUI();
            hideLoginModal();
            showToast(`Welcome back, ${currentUser.username}!`);
            return true;
        }
    } catch (err) {
        throw err;
    }
    return false;
}

export async function logout() {
    try {
        await apiFetch('/api/v1/auth/logout', { method: 'POST' });
    } catch {
        // Continue client logout
    }
    clearTokens();
    currentUser = null;
    showLoginModal();
    showToast('Signed out successfully.');
}

export async function checkAuth() {
    try {
        const user = await apiFetch('/api/v1/auth/me');
        if (user && user.username) {
            currentUser = user;
            updateUserUI();
            hideLoginModal();
            return true;
        }
    } catch {
        // Not authenticated
    }
    showLoginModal();
    return false;
}

function updateUserUI() {
    if (!currentUser) return;
    const nameEl = document.getElementById('current-user-name');
    const roleEl = document.getElementById('current-user-role');
    const avatarEl = document.getElementById('current-user-avatar');

    if (nameEl) nameEl.textContent = currentUser.username;
    if (roleEl) roleEl.textContent = currentUser.role;
    if (avatarEl) avatarEl.textContent = currentUser.username.substring(0, 2).toUpperCase();
}

export function showLoginModal() {
    const modal = document.getElementById('login-modal');
    if (modal) modal.classList.remove('hidden');
}

export function hideLoginModal() {
    const modal = document.getElementById('login-modal');
    if (modal) modal.classList.add('hidden');
}
