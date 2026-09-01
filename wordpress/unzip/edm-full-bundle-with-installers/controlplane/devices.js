// EDM Control Plane — Devices View Module
import { apiFetch } from './api.js';

export async function loadDevices() {
    const tbody = document.getElementById('devices-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="7" class="text-center">Loading devices...</td></tr>';

    try {
        const res = await apiFetch('/api/v1/admin/devices');

        if (!res.devices || res.devices.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-center">No registered devices found.</td></tr>';
            return;
        }

        tbody.innerHTML = res.devices.map(d => `
            <tr>
                <td><code>${d.installationId}</code></td>
                <td><strong>${d.clientType}</strong></td>
                <td>${d.osVersion || 'Unknown'}</td>
                <td>${d.appVersion || '2.0.0'}</td>
                <td>${d.coarseCountryCode || 'Global'}</td>
                <td>${d.sessionCount}</td>
                <td>${new Date(d.lastSeenAtUtc).toLocaleString()}</td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}
