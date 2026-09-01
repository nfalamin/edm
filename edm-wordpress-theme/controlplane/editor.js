// EDM Control Plane — Markdown Document Editor Module

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

let activeDoc = null;
let isDirty = false;

// Simple, secure, zero-dependency Markdown Parser for live preview
function parseMarkdown(md) {
    if (!md) return '<p style="color:var(--color-text-muted); font-style:italic;">No content to preview.</p>';

    let html = md
        // Escape HTML tags to prevent XSS
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        // Headers
        .replace(/^### (.*$)/gim, '<h3>$1</h3>')
        .replace(/^## (.*$)/gim, '<h2>$1</h2>')
        .replace(/^# (.*$)/gim, '<h1>$1</h1>')
        // Blockquotes
        .replace(/^\> (.*$)/gim, '<blockquote>$1</blockquote>')
        // Code Blocks
        .replace(/```([\s\S]*?)```/gim, '<pre><code>$1</code></pre>')
        // Inline Code
        .replace(/`([^`]+)`/gim, '<code>$1</code>')
        // Bold & Italic
        .replace(/\*\*([^*]+)\*\*/gim, '<strong>$1</strong>')
        .replace(/\*([^*]+)\*/gim, '<em>$1</em>')
        .replace(/__([^_]+)__/gim, '<strong>$1</strong>')
        .replace(/_([^_]+)_/gim, '<em>$1</em>')
        // Links
        .replace(/\[([^\]]+)\]\(([^)]+)\)/gim, '<a href="$2" target="_blank" rel="noopener noreferrer" style="color:var(--color-primary-light); text-decoration:underline;">$1</a>')
        // Unordered lists
        .replace(/^\s*-\s+(.*$)/gim, '<li>$1</li>')
        .replace(/(<li>.*<\/li>)/gim, '<ul>$1</ul>')
        // Horizontal Rule
        .replace(/^---$/gim, '<hr style="border:none; border-top:1px solid var(--color-border); margin:16px 0;">')
        // Line breaks & Paragraphs
        .replace(/\n\n/gim, '</p><p>')
        .replace(/\n/gim, '<br />');

    return `<p>${html}</p>`;
}

function loadDocument(doc) {
    activeDoc = doc;
    isDirty = false;

    const titleInput = document.getElementById('editor-doc-title');
    const docTypeBadge = document.getElementById('editor-doctype-badge');
    const statusBadge = document.getElementById('editor-status-badge');
    const pathLabel = document.getElementById('editor-file-path');
    const textarea = document.getElementById('editor-markdown-input');
    const preview = document.getElementById('editor-preview-output');

    if (titleInput) titleInput.value = doc.title || '';
    if (docTypeBadge) docTypeBadge.textContent = doc.docType || 'Document';
    if (pathLabel) pathLabel.textContent = doc.relativeFilePath || '';
    if (textarea) textarea.value = doc.markdownContent || '';

    updateStatusBadge(doc.isPublished);
    updateWordCount();
    renderPreview();
}

function updateStatusBadge(isPublished) {
    const statusBadge = document.getElementById('editor-status-badge');
    if (!statusBadge) return;
    if (isPublished) {
        statusBadge.className = 'badge badge-published';
        statusBadge.innerHTML = '<i data-lucide="check-circle" style="width:11px; height:11px; margin-right:4px; vertical-align:-1px;"></i>Published Live';
    } else {
        statusBadge.className = 'badge badge-draft';
        statusBadge.innerHTML = '<i data-lucide="clock" style="width:11px; height:11px; margin-right:4px; vertical-align:-1px;"></i>Draft (Unpublished)';
    }
    if (window.lucide) window.lucide.createIcons();
}

function renderPreview() {
    const textarea = document.getElementById('editor-markdown-input');
    const preview = document.getElementById('editor-preview-output');
    if (!textarea || !preview) return;

    preview.innerHTML = parseMarkdown(textarea.value);
    updateWordCount();
}

function updateWordCount() {
    const textarea = document.getElementById('editor-markdown-input');
    const wcElem = document.getElementById('editor-word-count');
    const ccElem = document.getElementById('editor-char-count');
    if (!textarea) return;

    const text = textarea.value.trim();
    const words = text ? text.split(/\s+/).length : 0;
    const chars = text.length;

    if (wcElem) wcElem.textContent = `${words} words`;
    if (ccElem) ccElem.textContent = `${chars} characters`;
}

function insertFormatting(type) {
    const textarea = document.getElementById('editor-markdown-input');
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = textarea.value.substring(start, end);
    let replacement = '';

    switch (type) {
        case 'h1': replacement = `# ${selected || 'Heading 1'}`; break;
        case 'h2': replacement = `## ${selected || 'Heading 2'}`; break;
        case 'h3': replacement = `### ${selected || 'Heading 3'}`; break;
        case 'bold': replacement = `**${selected || 'Bold Text'}**`; break;
        case 'italic': replacement = `*${selected || 'Italic Text'}*`; break;
        case 'code': replacement = `\`${selected || 'code'}\``; break;
        case 'codeblock': replacement = `\n\`\`\`bash\n${selected || '# your code here'}\n\`\`\`\n`; break;
        case 'bullet': replacement = `\n- ${selected || 'List item'}`; break;
        case 'ordered': replacement = `\n1. ${selected || 'List item'}`; break;
        case 'quote': replacement = `\n> ${selected || 'Quote text'}`; break;
        case 'link': replacement = `[${selected || 'Link title'}](https://)`; break;
        case 'table':
            replacement = `\n| Column 1 | Column 2 |\n|---|---|\n| Item 1 | Value 1 |\n| Item 2 | Value 2 |\n`;
            break;
    }

    textarea.setRangeText(replacement, start, end, 'end');
    textarea.focus();
    renderPreview();
}

async function saveDraft() {
    if (!activeDoc) return;
    const titleInput = document.getElementById('editor-doc-title');
    const textarea = document.getElementById('editor-markdown-input');

    const title = titleInput?.value?.trim() || activeDoc.title;
    const content = textarea?.value || '';

    try {
        const updated = await apiFetch(`/api/v1/admin/content/documents/${activeDoc.id}/draft`, {
            method: 'POST',
            body: JSON.stringify({ title, markdownContent: content })
        });
        activeDoc = updated;
        updateStatusBadge(false);
        showToast('Draft revision saved successfully!', 'success');
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function publishNow() {
    if (!activeDoc) return;
    if (!confirm(`Publish "${activeDoc.docType}" to public website immediately?`)) return;

    // Save current content first
    await saveDraft();

    try {
        const published = await apiFetch(`/api/v1/admin/content/documents/${activeDoc.id}/publish`, {
            method: 'POST'
        });
        activeDoc = published;
        updateStatusBadge(true);
        showToast(`Document "${activeDoc.docType}" is now LIVE on website & API!`, 'success');
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function openRevisions() {
    if (!activeDoc) return;
    if (window.edmContent && typeof window.edmContent.openHistoryModal === 'function') {
        window.edmContent.openHistoryModal(activeDoc.id);
    }
}

function backToList() {
    if (window.edmApp && typeof window.edmApp.navigateTo === 'function') {
        window.edmApp.navigateTo('content-manager');
    } else if (window.switchView) {
        window.switchView('content-manager');
    }
}

// Global Exports
window.edmEditor = {
    loadDocument,
    renderPreview,
    insertFormatting,
    saveDraft,
    publishNow,
    openRevisions,
    backToList
};
