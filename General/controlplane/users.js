// EDM Control Plane — Users View Module
import { apiFetch, showToast } from './api.js';

export async function loadUsers(search = '') {
    const tbody = document.getElementById('users-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="8" class="text-center">Loading users from API...</td></tr>';

    try {
        const query = search ? `?search=${encodeURIComponent(search)}` : '';
        const res = await apiFetch(`/api/v1/admin/users${query}`);

        if (!res.users || res.users.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center">No user records found.</td></tr>';
            return;
        }

        tbody.innerHTML = res.users.map(u => `
            <tr>
                <td><strong>${escapeHtml(u.username)}</strong></td>
                <td>${escapeHtml(u.email)}</td>
                <td><span class="badge ${u.role === 'SUPER_ADMIN' ? 'badge-admin' : ''}">${u.role}</span></td>
                <td>${u.isActive ? '<span class="status-active">Active</span>' : '<span class="status-inactive">Suspended</span>'}</td>
                <td>${u.deviceCount}</td>
                <td>${u.sessionCount}</td>
                <td>${new Date(u.createdAtUtc).toLocaleDateString()}</td>
                <td>
                    <button class="btn-secondary btn-sm" onclick="window.viewUserDetail('${u.id}')">Inspect</button>
                </td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}

export async function viewUserDetail(userId) {
    const modal = document.getElementById('user-modal');
    const body = document.getElementById('user-modal-body');
    const footer = document.getElementById('user-modal-footer');

    if (!modal || !body) return;

    modal.classList.remove('hidden');
    body.innerHTML = 'Loading user details...';
    footer.innerHTML = '';

    try {
        const res = await apiFetch(`/api/v1/admin/users/${userId}`);

        body.innerHTML = `
            <div class="detail-grid">
                <p><strong>User ID:</strong> ${res.id}</p>
                <p><strong>Username:</strong> ${escapeHtml(res.username)}</p>
                <p><strong>Email:</strong> ${escapeHtml(res.email)}</p>
                <p><strong>Role:</strong> ${res.role}</p>
                <p><strong>Account Status:</strong> ${res.isActive ? 'Active' : 'Suspended'}</p>
                <p><strong>Ban Status:</strong> ${res.isBanned ? `<span class="text-danger">BANNED (${escapeHtml(res.banReason || 'Active Ban')})</span>` : 'Clean'}</p>
                <p><strong>Entitlements:</strong> ${res.entitlements && res.entitlements.length ? res.entitlements.join(', ') : 'Standard Tier'}</p>
                <h4 class="mt-3">Recent Sessions</h4>
                <ul>
                    ${res.recentSessions && res.recentSessions.length ? res.recentSessions.map(s => `<li>${s.userAgent || 'Desktop Client'} (IP: ${s.coarseIpAddress || 'Unknown'}) - ${s.isRevoked ? 'Revoked' : 'Active'}</li>`).join('') : '<li>No active sessions</li>'}
                </ul>
            </div>
        `;

        footer.innerHTML = `
            <button class="btn-secondary btn-sm" onclick="window.revokeUserSessions('${res.id}')">Reset All Sessions</button>
            ${res.isBanned
                ? `<button class="btn-primary btn-sm" onclick="window.unbanUser('${res.id}')">Lift Ban</button>`
                : `<button class="btn-danger btn-sm" onclick="window.banUser('${res.id}')">Ban Account</button>`}
        `;
    } catch (err) {
        body.innerHTML = `<p class="text-danger">Failed to load user details: ${err.message}</p>`;
    }
}

export async function revokeUserSessions(userId) {
    if (!confirm('Are you sure you want to revoke all active sessions for this user?')) return;
    try {
        await apiFetch(`/api/v1/admin/revoke-user-sessions/${userId}`, { method: 'POST' });
        showToast('All user sessions revoked successfully.');
        viewUserDetail(userId);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function banUser(userId) {
    const reason = prompt('Enter ban reason:');
    if (!reason) return;

    try {
        await apiFetch('/api/v1/admin/ban', {
            method: 'POST',
            body: JSON.stringify({ targetType: 0, targetValue: userId, reason })
        });
        showToast('User has been banned.');
        viewUserDetail(userId);
        loadUsers();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function unbanUser(userId) {
    try {
        await apiFetch('/api/v1/admin/unban', {
            method: 'POST',
            body: JSON.stringify({ targetType: 0, targetValue: userId })
        });
        showToast('User ban lifted.');
        viewUserDetail(userId);
        loadUsers();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// Attach globally for inline HTML clicks
window.viewUserDetail = viewUserDetail;
window.revokeUserSessions = revokeUserSessions;
window.banUser = banUser;
window.unbanUser = unbanUser;
