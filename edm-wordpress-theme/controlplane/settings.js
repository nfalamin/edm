// EDM Control Plane — Settings View Module
import { getApiBaseUrl } from './api.js';

export function loadSettings() {
    const apiHostInput = document.getElementById('setting-api-url');
    if (apiHostInput) {
        apiHostInput.value = getApiBaseUrl();
    }
}
