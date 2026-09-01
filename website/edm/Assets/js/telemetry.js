// EDM Control Plane — Telemetry View Module
import { apiFetch } from './api.js';

export async function loadTelemetry() {
    const tbody = document.getElementById('telemetry-table-body');
    if (!tbody) return;

    tbody.innerHTML = '<tr><td colspan="5" class="text-center">Streaming telemetry events...</td></tr>';

    try {
        const res = await apiFetch('/api/v1/telemetry/events');

        if (!res.events || res.events.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center">No telemetry events recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = res.events.map(e => `
            <tr>
                <td>${new Date(e.timestampUtc).toLocaleString()}</td>
                <td><strong>${e.eventName}</strong></td>
                <td>${e.clientType}</td>
                <td><code>${e.installationId}</code></td>
                <td><code>${escapeJson(e.eventPayloadJson)}</code></td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="5" class="text-center text-danger">Error: ${err.message}</td></tr>`;
    }
}

function escapeJson(jsonStr) {
    if (!jsonStr) return '{}';
    return jsonStr.length > 80 ? jsonStr.substring(0, 77) + '...' : jsonStr;
}
