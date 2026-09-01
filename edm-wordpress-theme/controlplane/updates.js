// EDM Control Plane — Update Manager Module

async function apiFetch(url, options = {}) {
    if (window.edmApi && typeof window.edmApi.request === 'function') {
        return window.edmApi.request(url, options);
    }
    const res = await fetch(url, {
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        ...options
    });
    if (!res.ok) {
        const errData = await res.json().catch(() => ({ message: `HTTP ${res.status}` }));
        throw new Error(errData.message || `HTTP ${res.status}`);
    }
    return res.json();
}

function showToast(msg, type = 'info') {
    if (window.edmApp && typeof window.edmApp.showToast === 'function') {
        window.edmApp.showToast(msg, type);
    } else {
        console.log(`[Toast ${type}]: ${msg}`);
    }
}

let currentComponentFilter = 'All';
let currentUpdateStatusFilter = 'All';
let allUpdatesCache = [];

async function loadUpdateManager(component = 'All') {
    currentComponentFilter = component;
    const container = document.getElementById('update-cards-container');
    if (!container) return;

    container.innerHTML = '<div class="card" style="grid-column: 1/-1; text-align:center; padding: 40px;"><i data-lucide="loader" class="animate-spin"></i><p style="margin-top:10px; color:var(--color-text-secondary);">Loading updates from ControlPlane...</p></div>';
    if (window.lucide) window.lucide.createIcons();

    try {
        const url = component === 'All' ? '/api/v1/admin/updates?includeDrafts=true' : `/api/v1/admin/updates?component=${encodeURIComponent(component)}&includeDrafts=true`;
        const updates = await apiFetch(url);
        allUpdatesCache = Array.isArray(updates) ? updates : [];
        renderUpdateCards();
    } catch (err) {
        container.innerHTML = `<div class="card" style="grid-column: 1/-1; text-align:center; padding: 30px; color: var(--color-danger);"><i data-lucide="alert-triangle"></i><p style="margin-top:8px;">Failed to load updates: ${err.message}</p></div>`;
        if (window.lucide) window.lucide.createIcons();
    }
}

function filterUpdatesByStatus(status) {
    currentUpdateStatusFilter = status;
    renderUpdateCards();
}

function renderUpdateCards() {
    const container = document.getElementById('update-cards-container');
    if (!container) return;

    let filtered = allUpdatesCache;
    if (currentComponentFilter !== 'All') {
        filtered = filtered.filter(u => u.component.toLowerCase() === currentComponentFilter.toLowerCase());
    }
    if (currentUpdateStatusFilter !== 'All') {
        filtered = filtered.filter(u => u.status.toLowerCase() === currentUpdateStatusFilter.toLowerCase());
    }

    if (filtered.length === 0) {
        container.innerHTML = `
            <div class="card" style="grid-column: 1/-1; text-align: center; padding: 48px 20px;">
                <i data-lucide="package-search" style="width: 44px; height: 44px; margin: 0 auto 14px auto; color: var(--color-text-muted);"></i>
                <h3 style="font-size: 16px; font-weight: 700; color: var(--color-text-main); margin-bottom: 6px;">No Updates Found</h3>
                <p style="font-size: 13px; color: var(--color-text-secondary); max-width: 420px; margin: 0 auto 20px auto;">
                    No update packages match the selected component or status filter. Click "Scan Local Workspace" to detect packages in Update/ directory or create a new release draft.
                </p>
                <div style="display: flex; gap: 10px; justify-content: center;">
                    <button class="btn btn-secondary" onclick="window.edmUpdates.scanWorkspace()"><i data-lucide="folder-search"></i> Scan Workspace</button>
                    <button class="btn btn-primary" onclick="window.edmUpdates.openCreateModal()"><i data-lucide="plus-circle"></i> Create New Draft</button>
                </div>
            </div>`;
        if (window.lucide) window.lucide.createIcons();
        return;
    }

    container.innerHTML = filtered.map(u => {
        const statusClass = `badge-${u.status.toLowerCase()}`;
        const primaryArtifact = u.artifacts && u.artifacts.length > 0 ? u.artifacts[0] : null;
        const fileSizeMb = primaryArtifact ? (primaryArtifact.fileSizeBytes / (1024 * 1024)).toFixed(1) + ' MB' : 'Pending Upload';
        const fileName = primaryArtifact ? primaryArtifact.fileName : 'No file attached';
        const sha256 = primaryArtifact ? primaryArtifact.sha256Hash : '';

        return `
        <div class="update-card" data-id="${u.id}">
            <div class="card-header-row">
                <div style="display: flex; align-items: center; gap: 10px;">
                    <span class="badge" style="background: rgba(88,86,214,0.15); color: #818cf8; font-weight: 700; font-size: 11px;">${u.component}</span>
                    <strong style="font-size: 16px; color: #ffffff; letter-spacing: -0.3px;">v${u.version}</strong>
                    ${u.isLatest ? '<span class="badge badge-published" style="font-size: 10px; padding: 2px 6px;">LATEST</span>' : ''}
                </div>
                <span class="badge ${statusClass}">${u.status}</span>
            </div>

            <div style="display: flex; flex-direction: column; gap: 4px;">
                <h4 style="font-size: 13.5px; font-weight: 600; color: var(--color-text-main); margin: 0;">${u.title || 'Untitled Update'}</h4>
                <div class="card-meta-row">
                    <span><i data-lucide="file-box" style="width: 13px; height: 13px; margin-right: 4px; vertical-align: -2px;"></i>${fileName} (${fileSizeMb})</span>
                    <span><i data-lucide="calendar" style="width: 13px; height: 13px; margin-right: 4px; vertical-align: -2px;"></i>${new Date(u.createdAtUtc).toLocaleDateString()}</span>
                </div>
                ${sha256 ? `<div style="font-family: monospace; font-size: 11px; color: var(--color-text-muted); background: var(--color-bg-subtle); padding: 4px 8px; border-radius: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="SHA-256: ${sha256}">SHA: ${sha256.substring(0, 16)}...</div>` : ''}
            </div>

            <!-- Toggles & Status Settings -->
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px; background: var(--color-bg-subtle); padding: 10px 12px; border-radius: 8px; border: 1px solid var(--color-border);">
                <div style="display: flex; align-items: center; justify-content: space-between;">
                    <span style="font-size: 12px; color: var(--color-text-secondary);">Web Download</span>
                    <label class="switch">
                        <input type="checkbox" ${u.isWebsiteDownloadEnabled ? 'checked' : ''} onchange="window.edmUpdates.toggleDownload('${u.id}', this.checked)">
                        <span class="slider"></span>
                    </label>
                </div>
                <div style="display: flex; align-items: center; justify-content: space-between;">
                    <span style="font-size: 12px; color: var(--color-text-secondary);">Auto Update</span>
                    <label class="switch">
                        <input type="checkbox" ${u.isAutoUpdateEnabled ? 'checked' : ''} onchange="window.edmUpdates.toggleAutoUpdate('${u.id}', this.checked)">
                        <span class="slider"></span>
                    </label>
                </div>
            </div>

            <!-- Action Controls Row -->
            <div class="card-actions-row">
                <div style="display: flex; gap: 6px; flex-wrap: wrap;">
                    ${u.isPublished ? 
                        `<button class="btn btn-secondary btn-sm" onclick="window.edmUpdates.unpublish('${u.id}')"><i data-lucide="eye-off"></i> Unpublish</button>` : 
                        `<button class="btn btn-primary btn-sm" onclick="window.edmUpdates.publish('${u.id}')"><i data-lucide="upload-cloud"></i> Publish</button>`
                    }
                    ${!u.isLatest && u.isPublished ? 
                        `<button class="btn btn-ghost btn-sm" onclick="window.edmUpdates.setLatest('${u.id}')" title="Set as Latest Release"><i data-lucide="star"></i> Latest</button>` : ''
                    }
                    <button class="btn btn-ghost btn-sm" onclick="window.edmUpdates.openUploadModal('${u.id}', '${u.version}', '${u.component}')" title="Replace / Upload Binary"><i data-lucide="upload"></i> File</button>
                    <button class="btn btn-ghost btn-sm" onclick="window.edmUpdates.openEditModal('${u.id}')" title="Edit Metadata"><i data-lucide="edit-3"></i> Edit</button>
                </div>
                <div style="display: flex; gap: 4px;">
                    ${!u.isWithdrawn ? 
                        `<button class="btn-icon-only" onclick="window.edmUpdates.archive('${u.id}')" title="Archive Version" style="color: var(--color-text-muted);"><i data-lucide="archive"></i></button>` : ''
                    }
                    <button class="btn-icon-only" onclick="window.edmUpdates.deleteUpdate('${u.id}')" title="Delete Update" style="color: var(--color-danger);"><i data-lucide="trash-2"></i></button>
                </div>
            </div>
        </div>`;
    }).join('');

    if (window.lucide) window.lucide.createIcons();
}

async function publish(id) {
    if (!confirm('Are you sure you want to publish this release? It will become publicly available according to its download/auto-update policy.')) return;
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/publish?setAsLatest=true`, { method: 'POST' });
        showToast('Release published successfully!', 'success');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function unpublish(id) {
    if (!confirm('Unpublish this release? It will be switched back to DRAFT and hidden from public downloads.')) return;
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/unpublish`, { method: 'POST' });
        showToast('Release unpublished to Draft.', 'info');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function toggleDownload(id, enabled) {
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/toggle-download`, {
            method: 'PUT',
            body: JSON.stringify({ enabled })
        });
        showToast(`Website download ${enabled ? 'ENABLED' : 'DISABLED'}`, 'success');
    } catch (err) {
        showToast(err.message, 'error');
        loadUpdateManager(currentComponentFilter);
    }
}

async function toggleAutoUpdate(id, enabled) {
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/toggle-auto-update`, {
            method: 'PUT',
            body: JSON.stringify({ enabled })
        });
        showToast(`Auto-update ${enabled ? 'ENABLED' : 'DISABLED'}`, 'success');
    } catch (err) {
        showToast(err.message, 'error');
        loadUpdateManager(currentComponentFilter);
    }
}

async function setLatest(id) {
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/set-latest`, { method: 'POST' });
        showToast('Marked as Latest Release!', 'success');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function archive(id) {
    const reason = prompt('Enter reason for archiving / withdrawing this release (optional):');
    if (reason === null) return;
    try {
        await apiFetch(`/api/v1/admin/updates/${id}/archive`, {
            method: 'POST',
            body: JSON.stringify({ reason })
        });
        showToast('Release archived.', 'info');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function deleteUpdate(id) {
    if (!confirm('Permanently delete this update record? This cannot be undone.')) return;
    try {
        await apiFetch(`/api/v1/admin/updates/${id}`, { method: 'DELETE' });
        showToast('Update record deleted.', 'info');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function scanWorkspace() {
    showToast('Scanning local Update/ directory for new packages...', 'info');
    try {
        const res = await apiFetch('/api/v1/admin/updates/scan-workspace', { method: 'POST' });
        showToast(res.message || 'Scan completed!', 'success');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function openCreateModal() {
    const modal = document.getElementById('modal-create-update');
    if (modal) modal.classList.add('active');
}

async function submitCreateDraft() {
    const comp = document.getElementById('new-up-component')?.value || 'App';
    const ver = document.getElementById('new-up-version')?.value?.trim();
    const title = document.getElementById('new-up-title')?.value?.trim();
    const notes = document.getElementById('new-up-notes')?.value?.trim();
    const severity = document.getElementById('new-up-severity')?.value || 'Standard';
    const minver = document.getElementById('new-up-minver')?.value?.trim() || '1.0.0';
    const isMandatory = document.getElementById('new-up-mandatory')?.checked || false;

    if (!ver || !title) {
        showToast('Please enter version and title', 'error');
        return;
    }

    try {
        await apiFetch('/api/v1/admin/updates', {
            method: 'POST',
            body: JSON.stringify({
                component: comp,
                version: ver,
                title: title,
                releaseNotes: notes,
                severity: severity,
                minimumSupportedVersion: minver,
                isMandatory: isMandatory,
                channel: 'stable'
            })
        });
        showToast(`Draft update v${ver} created!`, 'success');
        const modal = document.getElementById('modal-create-update');
        if (modal) modal.classList.remove('active');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function openUploadModal(id, version, component) {
    window.currentUploadReleaseId = id;
    const modal = document.getElementById('modal-upload-artifact');
    const label = document.getElementById('upload-artifact-version-label');
    if (label) label.textContent = `${component} v${version}`;
    if (modal) modal.classList.add('active');
}

async function submitUploadArtifact() {
    const fileInput = document.getElementById('artifact-file-input');
    const archSelect = document.getElementById('artifact-arch-select');
    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        showToast('Please select a file to upload', 'error');
        return;
    }

    const file = fileInput.files[0];
    const arch = archSelect ? archSelect.value : 'x64';
    const formData = new FormData();
    formData.append('file', file);

    showToast(`Uploading ${file.name} (${(file.size / (1024 * 1024)).toFixed(1)} MB)...`, 'info');

    try {
        await apiFetch(`/api/v1/admin/updates/${window.currentUploadReleaseId}/upload-artifact?architecture=${arch}`, {
            method: 'POST',
            body: formData
        });
        showToast('Artifact uploaded & checksum calculated successfully!', 'success');
        const modal = document.getElementById('modal-upload-artifact');
        if (modal) modal.classList.remove('active');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function openEditModal(id) {
    const update = allUpdatesCache.find(u => u.id === id);
    if (!update) return;

    window.currentEditReleaseId = id;
    const modal = document.getElementById('modal-edit-update-meta');
    if (!modal) return;

    document.getElementById('edit-up-title').value = update.title || '';
    document.getElementById('edit-up-notes').value = update.artifacts?.[0]?.releaseNotes || '';
    document.getElementById('edit-up-minver').value = update.minimumSupportedVersion || '1.0.0';
    document.getElementById('edit-up-severity').value = update.severity || 'Standard';
    document.getElementById('edit-up-mandatory').checked = update.isMandatory || false;

    modal.classList.add('active');
}

async function submitEditMetadata() {
    const title = document.getElementById('edit-up-title')?.value?.trim();
    const notes = document.getElementById('edit-up-notes')?.value?.trim();
    const minver = document.getElementById('edit-up-minver')?.value?.trim() || '1.0.0';
    const severity = document.getElementById('edit-up-severity')?.value || 'Standard';
    const isMandatory = document.getElementById('edit-up-mandatory')?.checked || false;

    try {
        await apiFetch(`/api/v1/admin/updates/${window.currentEditReleaseId}/metadata`, {
            method: 'PUT',
            body: JSON.stringify({
                title,
                releaseNotes: notes,
                minimumSupportedVersion: minver,
                severity,
                isMandatory
            })
        });
        showToast('Metadata updated successfully!', 'success');
        const modal = document.getElementById('modal-edit-update-meta');
        if (modal) modal.classList.remove('active');
        loadUpdateManager(currentComponentFilter);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

// Global Exports
window.edmUpdates = {
    loadUpdateManager,
    filterUpdatesByStatus,
    publish,
    unpublish,
    toggleDownload,
    toggleAutoUpdate,
    setLatest,
    archive,
    deleteUpdate,
    scanWorkspace,
    openCreateModal,
    submitCreateDraft,
    openUploadModal,
    submitUploadArtifact,
    openEditModal,
    submitEditMetadata
};
