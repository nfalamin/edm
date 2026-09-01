// EDM Control Plane — Releases View Module
import { apiFetch, showToast } from './api.js';

export async function loadReleases() {
    const tbody = document.getElementById('releases-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="8" class="text-center">Loading releases...</td></tr>';

    try {
        const releases = await apiFetch('/api/v1/admin/releases');

        if (!releases || releases.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center">No releases published yet.</td></tr>';
            return;
        }

        tbody.innerHTML = releases.map(r => `
            <tr>
                <td><strong>v${r.version}</strong></td>
                <td>${r.platform}</td>
                <td><span class="badge ${r.severity === 'Critical' ? 'badge-danger' : ''}">${r.severity}</span></td>
                <td>v${r.minimumSupportedVersion}</td>
                <td>${r.isMandatory ? 'Yes' : 'No'}</td>
                <td>${r.isWithdrawn ? '<span class="text-danger">Archived</span>' : '<span class="status-active">Published</span>'}</td>
                <td>${new Date(r.publishedAtUtc).toLocaleDateString()}</td>
                <td>
                    ${!r.isWithdrawn ? `<button class="btn-secondary btn-sm" onclick="window.archiveRelease('${r.id}')">Archive</button>` : '—'}
                </td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}

export async function createRelease(payload) {
    try {
        const res = await apiFetch('/api/v1/admin/releases', {
            method: 'POST',
            body: JSON.stringify(payload)
        });
        showToast(`Release v${payload.version} published successfully!`);
        loadReleases();
        return true;
    } catch (err) {
        showToast(err.message, 'error');
        return false;
    }
}

export async function archiveRelease(releaseId) {
    if (!confirm('Are you sure you want to archive/withdraw this release?')) return;
    try {
        await apiFetch(`/api/v1/admin/releases/${releaseId}/archive`, { method: 'PUT' });
        showToast('Release archived successfully.');
        loadReleases();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

window.archiveRelease = archiveRelease;
