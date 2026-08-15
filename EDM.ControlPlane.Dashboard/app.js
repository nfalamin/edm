// EDM Control Plane — Main SPA Orchestrator
import { apiFetch, showToast } from './api.js';
import { checkAuth, login, logout } from './auth.js';
import { loadUsers } from './users.js';
import { loadDevices } from './devices.js';
import { loadSessions } from './sessions.js';
import { loadTelemetry } from './telemetry.js';
import { loadDashboardOverviewCharts, loadDetailedAnalytics, loadSecurityAnalytics } from './analytics.js';
import { loadReleases, createRelease } from './releases.js';
import { submitBan } from './security.js';
import { loadSettings } from './settings.js';

let activeView = 'dashboard';
let pollingTimer = null;

document.addEventListener('DOMContentLoaded', async () => {
    initNavigation();
    initModals();
    initClock();

    // Check Authentication
    const isAuthenticated = await checkAuth();
    if (isAuthenticated) {
        switchView('dashboard');
        startPolling();
    }
});

function initNavigation() {
    const navItems = document.querySelectorAll('.nav-item');
    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const view = item.getAttribute('data-view');
            switchView(view);
        });
    });

    const logoutBtn = document.getElementById('btn-logout');
    if (logoutBtn) logoutBtn.addEventListener('click', () => logout());

    const refreshBtn = document.getElementById('btn-manual-refresh');
    if (refreshBtn) refreshBtn.addEventListener('click', () => refreshCurrentView());

    // Analytics range tabs
    const rangeTabs = document.querySelectorAll('.btn-tab');
    rangeTabs.forEach(tab => {
        tab.addEventListener('click', () => {
            rangeTabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            const range = tab.getAttribute('data-range');
            loadDetailedAnalytics(range);
        });
    });

    // Users search debounce
    const userSearchInput = document.getElementById('users-search');
    let debounceTimeout = null;
    if (userSearchInput) {
        userSearchInput.addEventListener('input', (e) => {
            clearTimeout(debounceTimeout);
            debounceTimeout = setTimeout(() => {
                loadUsers(e.target.value);
            }, 300);
        });
    }

    const refreshUsersBtn = document.getElementById('btn-refresh-users');
    if (refreshUsersBtn) refreshUsersBtn.addEventListener('click', () => loadUsers());
}

function initModals() {
    // Login form
    const loginForm = document.getElementById('login-form');
    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const u = document.getElementById('login-username').value;
            const p = document.getElementById('login-password').value;
            const errEl = document.getElementById('login-error');
            errEl.classList.add('hidden');

            try {
                const ok = await login(u, p);
                if (ok) {
                    switchView('dashboard');
                    startPolling();
                }
            } catch (err) {
                errEl.textContent = err.message;
                errEl.classList.remove('hidden');
            }
        });
    }

    // User Detail Modal Close
    const closeUserBtn = document.getElementById('btn-close-user-modal');
    if (closeUserBtn) closeUserBtn.addEventListener('click', () => document.getElementById('user-modal').classList.add('hidden'));

    // Release Modal
    const openRelBtn = document.getElementById('btn-create-release-modal');
    const closeRelBtn = document.getElementById('btn-close-release-modal');
    const relModal = document.getElementById('create-release-modal');
    if (openRelBtn) openRelBtn.addEventListener('click', () => relModal.classList.remove('hidden'));
    if (closeRelBtn) closeRelBtn.addEventListener('click', () => relModal.classList.add('hidden'));

    const relForm = document.getElementById('form-create-release');
    if (relForm) {
        relForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const platform = parseInt(document.getElementById('rel-platform').value, 10);
            const version = document.getElementById('rel-version').value;
            const minVer = document.getElementById('rel-min-ver').value;
            const title = document.getElementById('rel-title').value;
            const notes = document.getElementById('rel-notes').value;
            const url = document.getElementById('rel-art-url').value;
            const sha = document.getElementById('rel-art-sha').value;

            const payload = {
                platform,
                version,
                minimumSupportedVersion: minVer,
                title,
                releaseNotes: notes,
                isMandatory: false,
                severity: 0,
                artifacts: [
                    {
                        artifactName: `EDM-${version}-${platform}`,
                        downloadUrl: url,
                        sha256Hash: sha,
                        fileSizeBytes: 3500000,
                        signatureBase64: null
                    }
                ]
            };

            const success = await createRelease(payload);
            if (success) {
                relModal.classList.add('hidden');
                relForm.reset();
            }
        });
    }

    // Ban Modal
    const openBanBtn = document.getElementById('btn-open-ban-modal');
    const closeBanBtn = document.getElementById('btn-close-ban-modal');
    const banModal = document.getElementById('ban-modal');
    if (openBanBtn) openBanBtn.addEventListener('click', () => banModal.classList.remove('hidden'));
    if (closeBanBtn) closeBanBtn.addEventListener('click', () => banModal.classList.add('hidden'));

    const banForm = document.getElementById('form-ban-target');
    if (banForm) {
        banForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const targetType = document.getElementById('ban-type').value;
            const targetValue = document.getElementById('ban-value').value;
            const reason = document.getElementById('ban-reason').value;
            const duration = document.getElementById('ban-days').value;

            const success = await submitBan(targetType, targetValue, reason, duration);
            if (success) {
                banModal.classList.add('hidden');
                banForm.reset();
                loadSecurityAnalytics();
            }
        });
    }
}

export function switchView(viewName) {
    activeView = viewName;

    // Update nav active item
    document.querySelectorAll('.nav-item').forEach(item => {
        if (item.getAttribute('data-view') === viewName) item.classList.add('active');
        else item.classList.remove('active');
    });

    // Update panel active
    document.querySelectorAll('.view-panel').forEach(panel => {
        panel.classList.add('hidden');
        panel.classList.remove('active');
    });

    const targetPanel = document.getElementById(`view-${viewName}`);
    if (targetPanel) {
        targetPanel.classList.remove('hidden');
        targetPanel.classList.add('active');
    }

    const titleMap = {
        dashboard: 'System Overview',
        users: 'User Directory',
        devices: 'Device Inventory',
        sessions: 'Active Sessions',
        telemetry: 'Real-Time Telemetry Stream',
        analytics: 'Performance & Usage Analytics',
        releases: 'Release Orchestration',
        security: 'Security & Ban Administration',
        audit: 'Immutable Audit Ledger',
        settings: 'Control Plane Configuration'
    };

    const titleEl = document.getElementById('view-title');
    if (titleEl) titleEl.textContent = titleMap[viewName] || 'Control Center';

    refreshCurrentView();
}

async function refreshCurrentView() {
    switch (activeView) {
        case 'dashboard':
            await loadDashboardSummary();
            await loadDashboardOverviewCharts();
            break;
        case 'users':
            await loadUsers();
            break;
        case 'devices':
            await loadDevices();
            break;
        case 'sessions':
            await loadSessions();
            break;
        case 'telemetry':
            await loadTelemetry();
            break;
        case 'analytics':
            await loadDetailedAnalytics('7d');
            break;
        case 'releases':
            await loadReleases();
            break;
        case 'security':
            await loadSecurityAnalytics();
            break;
        case 'audit':
            await loadAuditLog();
            break;
        case 'settings':
            loadSettings();
            break;
    }
}

async function loadDashboardSummary() {
    try {
        const data = await apiFetch('/api/v1/admin/dashboard/summary');
        document.getElementById('card-total-users').textContent = data.totalUsers;
        document.getElementById('card-active-sessions').textContent = data.activeSessions;
        document.getElementById('card-registered-devices').textContent = data.registeredDevices;
        document.getElementById('card-total-downloads').textContent = data.totalDownloads;
        document.getElementById('card-today-downloads').textContent = data.downloadsToday;
        document.getElementById('card-current-release').textContent = data.currentRelease;
        document.getElementById('card-security-events').textContent = data.securityEvents;
        document.getElementById('card-banned-accounts').textContent = data.bannedAccounts;
    } catch (err) {
        console.error('Failed to load dashboard summary:', err);
    }
}

async function loadAuditLog() {
    const tbody = document.getElementById('audit-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="8" class="text-center">Loading audit log...</td></tr>';
    try {
        const res = await apiFetch('/api/v1/admin/audit-logs');
        if (!res.logs || res.logs.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center">No audit records found.</td></tr>';
            return;
        }

        tbody.innerHTML = res.logs.map(l => `
            <tr>
                <td>${new Date(l.timestampUtc).toLocaleString()}</td>
                <td><strong>${l.actorUsername}</strong></td>
                <td>${l.action}</td>
                <td>${l.targetEntity}</td>
                <td><code>${l.targetId || '—'}</code></td>
                <td><span class="badge ${l.resultStatus === 'SUCCESS' ? 'badge-success' : 'badge-danger'}">${l.resultStatus}</span></td>
                <td>${l.coarseIpAddress || 'Hidden'}</td>
                <td><code>${l.correlationId}</code></td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}

function startPolling() {
    if (pollingTimer) clearInterval(pollingTimer);
    pollingTimer = setInterval(() => {
        if (activeView === 'dashboard' || activeView === 'telemetry' || activeView === 'sessions') {
            refreshCurrentView();
        }
    }, 30000);
}

function initClock() {
    const clockEl = document.getElementById('server-clock');
    if (!clockEl) return;
    setInterval(() => {
        clockEl.textContent = `UTC: ${new Date().toISOString().substring(11, 19)}`;
    }, 1000);
}
