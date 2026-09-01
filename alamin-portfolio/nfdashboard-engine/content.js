// EDM Control Plane — Content Manager Module
import { apiFetch, showToast } from './api.js';

let allDocsCache = [];
let currentStatusFilter = 'All';

const DocIcons = {
    'About': 'info',
    'Privacy': 'shield-check',
    'Terms': 'file-text',
    'FAQ': 'help-circle',
    'Help': 'life-buoy',
    'Documentation': 'book-open',
    'ReleaseNotes': 'sparkles',
    'Announcements': 'megaphone',
    'General': 'file'
};

export async function loadContentManager() {
    const container = document.getElementById('content-cards-container');
    if (!container) return;

    container.innerHTML = '<div class="card" style="grid-column: 1/-1; text-align:center; padding: 40px;"><i data-lucide="loader" class="animate-spin"></i><p style="margin-top:10px; color:var(--color-text-secondary);">Loading documents from Content/ workspace...</p></div>';
    if (window.lucide) window.lucide.createIcons();

    try {
        const docs = await apiFetch('/api/v1/admin/content/documents?includeDrafts=true');
        allDocsCache = Array.isArray(docs) ? docs : [];
        renderContentCards();
    } catch (err) {
        container.innerHTML = `<div class="card" style="grid-column: 1/-1; text-align:center; padding: 30px; color: var(--color-danger);"><i data-lucide="alert-triangle"></i><p style="margin-top:8px;">Failed to load documents: ${err.message}</p></div>`;
        if (window.lucide) window.lucide.createIcons();
    }
}

export function filterContentByStatus(status) {
    currentStatusFilter = status;
    renderContentCards();
}

function renderContentCards() {
    const container = document.getElementById('content-cards-container');
    if (!container) return;

    let filtered = allDocsCache;
    if (currentStatusFilter === 'Published') {
        filtered = filtered.filter(d => d.isPublished);
    } else if (currentStatusFilter === 'Draft') {
        filtered = filtered.filter(d => d.isDraft);
    }

    if (filtered.length === 0) {
        container.innerHTML = `
            <div class="card" style="grid-column: 1/-1; text-align: center; padding: 48px 20px;">
                <i data-lucide="file-question" style="width: 44px; height: 44px; margin: 0 auto 14px auto; color: var(--color-text-muted);"></i>
                <h3 style="font-size: 16px; font-weight: 700; color: var(--color-text-main); margin-bottom: 6px;">No Documents Found</h3>
                <p style="font-size: 13px; color: var(--color-text-secondary); max-width: 420px; margin: 0 auto 20px auto;">
                    No markdown documents match your filter. Click "Scan Local Workspace" to detect markdown files in the Content/ directory.
                </p>
                <button class="btn btn-primary" onclick="window.edmContent.scanWorkspace()"><i data-lucide="folder-search"></i> Scan Content Workspace</button>
            </div>`;
        if (window.lucide) window.lucide.createIcons();
        return;
    }

    container.innerHTML = filtered.map(d => {
        const iconName = DocIcons[d.docType] || 'file-text';
        const statusBadge = d.isPublished ? 
            `<span class="badge badge-published"><i data-lucide="check-circle" style="width:11px; height:11px; margin-right:4px; vertical-align:-1px;"></i>Published</span>` : 
            `<span class="badge badge-draft"><i data-lucide="clock" style="width:11px; height:11px; margin-right:4px; vertical-align:-1px;"></i>Draft</span>`;
        
        const wordCount = d.markdownContent ? d.markdownContent.trim().split(/\s+/).length : 0;
        const fileSizeKb = (d.fileSizeBytes / 1024).toFixed(1) + ' KB';
        const revisionCount = d.revisions ? d.revisions.length : 0;

        return `
        <div class="content-card" data-id="${d.id}">
            <div class="card-header-row">
                <div style="display: flex; align-items: center; gap: 10px;">
                    <div style="width: 32px; height: 32px; border-radius: 8px; background: rgba(88,86,214,0.15); display: flex; align-items: center; justify-content: center; color: #818cf8;">
                        <i data-lucide="${iconName}" style="width: 18px; height: 18px;"></i>
                    </div>
                    <div>
                        <strong style="font-size: 15px; color: #ffffff; display: block; line-height: 1.2;">${d.docType}</strong>
                        <span style="font-size: 11px; color: var(--color-text-muted); font-family: monospace;">/${d.slug}</span>
                    </div>
                </div>
                ${statusBadge}
            </div>

            <div>
                <h4 style="font-size: 14px; font-weight: 600; color: var(--color-text-main); margin: 0 0 6px 0;">${d.title}</h4>
                <p style="font-size: 12px; color: var(--color-text-secondary); margin: 0; line-height: 1.4; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">
                    ${d.markdownContent ? d.markdownContent.replace(/[#*`_\[\]]/g, '').substring(0, 140) + '...' : 'No content'}
                </p>
            </div>

            <div class="card-meta-row" style="background: var(--color-bg-subtle); padding: 8px 10px; border-radius: 6px; border: 1px solid var(--color-border);">
                <span><i data-lucide="file-code" style="width: 12px; height: 12px; margin-right: 3px; vertical-align: -1px;"></i>${d.relativeFilePath}</span>
                <span><i data-lucide="file-text" style="width: 12px; height: 12px; margin-right: 3px; vertical-align: -1px;"></i>${wordCount} words (${fileSizeKb})</span>
                <span><i data-lucide="git-branch" style="width: 12px; height: 12px; margin-right: 3px; vertical-align: -1px;"></i>v${d.version} (${revisionCount} rev)</span>
                <span><i data-lucide="user" style="width: 12px; height: 12px; margin-right: 3px; vertical-align: -1px;"></i>${d.lastEditor || 'Admin'}</span>
            </div>

            <div class="card-actions-row">
                <div style="display: flex; gap: 6px; flex-wrap: wrap;">
                    <button class="btn btn-primary btn-sm" onclick="window.edmContent.openEditor('${d.id}')">
                        <i data-lucide="edit-3"></i> Edit Document
                    </button>
                    ${d.isPublished ? 
                        `<button class="btn btn-secondary btn-sm" onclick="window.edmContent.unpublish('${d.id}')"><i data-lucide="eye-off"></i> Unpublish</button>` : 
                        `<button class="btn btn-secondary btn-sm" onclick="window.edmContent.publish('${d.id}')"><i data-lucide="upload-cloud"></i> Publish</button>`
                    }
                </div>
                <div style="display: flex; gap: 4px;">
                    <button class="btn-icon-only" onclick="window.edmContent.openReplaceModal('${d.id}', '${d.docType}')" title="Replace with Markdown File"><i data-lucide="upload"></i></button>
                    <button class="btn-icon-only" onclick="window.edmContent.openHistoryModal('${d.id}')" title="Revision History"><i data-lucide="history"></i></button>
                </div>
            </div>
        </div>`;
    }).join('');

    if (window.lucide) window.lucide.createIcons();
}

export function openEditor(id) {
    const doc = allDocsCache.find(d => d.id === id);
    if (!doc) return;

    if (window.edmEditor && typeof window.edmEditor.loadDocument === 'function') {
        window.edmEditor.loadDocument(doc);
        // Switch view to document editor
        if (window.switchView) {
            window.switchView('content-editor');
        }
    }
}

export async function publish(id) {
    if (!confirm('Publish this document? It will become live immediately on the public website and API.')) return;
    try {
        await apiFetch(`/api/v1/admin/content/documents/${id}/publish`, { method: 'POST' });
        showToast('Document published to public website & API!', 'success');
        loadContentManager();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function unpublish(id) {
    if (!confirm('Unpublish this document? It will be switched to DRAFT and hidden from the public website.')) return;
    try {
        await apiFetch(`/api/v1/admin/content/documents/${id}/unpublish`, { method: 'POST' });
        showToast('Document unpublished to Draft.', 'info');
        loadContentManager();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function scanWorkspace() {
    showToast('Scanning local Content/ directory for documents...', 'info');
    try {
        const res = await apiFetch('/api/v1/admin/content/scan-workspace', { method: 'POST' });
        showToast(res.message || 'Content workspace scanned!', 'success');
        loadContentManager();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export function openReplaceModal(id, docType) {
    window.currentReplaceDocId = id;
    const modal = document.getElementById('modal-replace-content-file');
    const label = document.getElementById('replace-content-label');
    if (label) label.textContent = `${docType} Markdown File`;
    if (modal) modal.classList.add('active');
}

export async function submitReplaceFile() {
    const fileInput = document.getElementById('replace-content-input');
    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        showToast('Please select a markdown (.md) file to upload', 'error');
        return;
    }

    const file = fileInput.files[0];
    const formData = new FormData();
    formData.append('file', file);

    try {
        await apiFetch(`/api/v1/admin/content/documents/${window.currentReplaceDocId}/replace-file`, {
            method: 'POST',
            body: formData
        });
        showToast('Document file replaced and new revision saved!', 'success');
        const modal = document.getElementById('modal-replace-content-file');
        if (modal) modal.classList.remove('active');
        loadContentManager();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function openHistoryModal(id) {
    const doc = allDocsCache.find(d => d.id === id);
    if (!doc) return;

    window.currentHistoryDocId = id;
    const modal = document.getElementById('modal-doc-history');
    const titleElem = document.getElementById('doc-history-title');
    const listElem = document.getElementById('doc-history-list');

    if (titleElem) titleElem.textContent = `${doc.docType} — Revision History`;
    if (listElem) {
        listElem.innerHTML = '<div style="text-align:center; padding: 20px;"><i data-lucide="loader" class="animate-spin"></i> Loading revisions...</div>';
        if (window.lucide) window.lucide.createIcons();
    }
    if (modal) modal.classList.add('active');

    try {
        const history = await apiFetch(`/api/v1/admin/content/documents/${id}/history`);
        if (!Array.isArray(history) || history.length === 0) {
            listElem.innerHTML = '<p style="text-align:center; color:var(--color-text-secondary); padding: 20px;">No earlier revisions stored yet.</p>';
            return;
        }

        listElem.innerHTML = history.slice().reverse().map(rev => `
            <div class="revision-item">
                <div>
                    <strong style="font-size: 13.5px; color: #ffffff;">v${rev.version} — ${rev.title || 'Revision'}</strong>
                    <div style="font-size: 11px; color: var(--color-text-secondary); margin-top: 2px;">
                        <span>Saved by ${rev.savedBy || 'Admin'} on ${new Date(rev.savedAtUtc).toLocaleString()}</span>
                        ${rev.wasPublished ? '<span class="badge badge-published" style="font-size: 9px; padding: 1px 4px; margin-left: 6px;">Was Live</span>' : ''}
                    </div>
                </div>
                <div style="display: flex; gap: 6px;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmContent.restoreRevision('${id}', ${rev.version})">
                        <i data-lucide="rotate-ccw"></i> Restore
                    </button>
                </div>
            </div>
        `).join('');

        if (window.lucide) window.lucide.createIcons();
    } catch (err) {
        listElem.innerHTML = `<p style="color:var(--color-danger); padding:20px;">Failed to load history: ${err.message}</p>`;
    }
}

export async function restoreRevision(id, version) {
    if (!confirm(`Restore revision v${version}? This will create a new draft containing this version's content.`)) return;
    try {
        await apiFetch(`/api/v1/admin/content/documents/${id}/restore/${version}`, { method: 'POST' });
        showToast(`Restored version v${version} as new draft!`, 'success');
        const modal = document.getElementById('modal-doc-history');
        if (modal) modal.classList.remove('active');
        loadContentManager();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

// Global Exports
window.edmContent = {
    loadContentManager,
    filterContentByStatus,
    openEditor,
    publish,
    unpublish,
    scanWorkspace,
    openReplaceModal,
    submitReplaceFile,
    openHistoryModal,
    restoreRevision
};
