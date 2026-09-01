// EDM Control Plane — Security & Ban Management Module
import { apiFetch, showToast } from './api.js';

export async function submitBan(targetType, targetValue, reason, durationDays) {
    try {
        await apiFetch('/api/v1/admin/ban', {
            method: 'POST',
            body: JSON.stringify({
                targetType: parseInt(targetType, 10),
                targetValue,
                reason,
                durationDays: durationDays ? parseInt(durationDays, 10) : null
            })
        });
        showToast('Target banned successfully.');
        return true;
    } catch (err) {
        showToast(err.message, 'error');
        return false;
    }
}
