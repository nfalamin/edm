// EDM Control Plane — Sessions View Module
import { apiFetch, showToast } from './api.js';

export async function loadSessions() {
    const tbody = document.getElementById('sessions-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="7" class="text-center">Loading active sessions...</td></tr>';

    try {
        const res = await apiFetch('/api/v1/admin/sessions');

        if (!res.sessions || res.sessions.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-center">No active sessions.</td></tr>';
            return;
        }

        tbody.innerHTML = res.sessions.map(s => `
            <tr>
                <td><strong>${s.username}</strong></td>
                <td><code>${s.installationId}</code></td>
                <td>${s.coarseIpAddress || 'Hidden'}</td>
                <td>${s.userAgent || 'Desktop App'}</td>
                <td>${s.isActive ? '<span class="status-active">Active</span>' : '<span class="status-inactive">Revoked</span>'}</td>
                <td>${new Date(s.lastActivityAtUtc).toLocaleString()}</td>
                <td>
                    ${s.isActive ? `<button class="btn-danger btn-sm" onclick="window.revokeSingleSession('${s.id}')">Revoke</button>` : '—'}
                </td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}

export async function revokeSingleSession(sessionId) {
    if (!confirm('Are you sure you want to revoke this session?')) return;
    try {
        await apiFetch(`/api/v1/admin/revoke-session/${sessionId}`, { method: 'POST' });
        showToast('Session revoked successfully.');
        loadSessions();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

window.revokeSingleSession = revokeSingleSession;
