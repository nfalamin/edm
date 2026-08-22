/**
 * EDM Extension - Dashboard Controller
 * Version: 1.0.0
 */

import { ThemeManager } from '../src/ui/design-tokens.js';

document.addEventListener('DOMContentLoaded', async () => {
    // 1. Theme
    const currentTheme = ThemeManager.getStoredTheme();
    ThemeManager.applyTheme(currentTheme);

    // 2. State
    let allDownloads = [];
    let currentCategory = 'ALL';
    let searchQuery = '';

    const tableBody = document.getElementById('dashTableBody');
    const emptyNotice = document.getElementById('dashTableEmpty');

    function formatBytes(bytes) {
        if (!bytes || bytes <= 0) return 'Unknown';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    function renderTable() {
        let filtered = allDownloads.filter(item => {
            if (currentCategory === 'VIDEO') return item.container === 'mp4' || item.container === 'mkv' || item.container === 'webm' || item.category === 'VIDEO';
            if (currentCategory === 'AUDIO') return item.container === 'mp3' || item.container === 'm4a' || item.container === 'opus' || item.category === 'AUDIO';
            if (currentCategory === 'COMPRESSED') return item.container === 'zip' || item.container === 'rar' || item.container === '7z' || item.container === 'tar';
            if (currentCategory === 'PROGRAMS') return item.container === 'exe' || item.container === 'msi' || item.container === 'dmg' || item.container === 'apk';
            if (currentCategory === 'DOCS') return item.container === 'pdf' || item.container === 'docx' || item.container === 'xlsx';
            return true;
        });

        if (searchQuery) {
            const q = searchQuery.toLowerCase();
            filtered = filtered.filter(item => (item.filename || '').toLowerCase().includes(q) || (item.url || '').toLowerCase().includes(q));
        }

        tableBody.innerHTML = '';

        if (filtered.length === 0) {
            emptyNotice.style.display = 'block';
            return;
        }

        emptyNotice.style.display = 'none';

        filtered.forEach(item => {
            const tr = document.createElement('tr');
            const dateStr = item.createdAt ? new Date(item.createdAt).toLocaleDateString() : 'Recent';
            const sizeStr = formatBytes(item.size || item.downloadedBytes);

            tr.innerHTML = `
                <td>
                    <div style="font-weight: 700;">${item.filename || 'download'}</div>
                    <div style="font-size: 10.5px; color: var(--edm-text-muted); max-width: 320px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${item.url || ''}</div>
                </td>
                <td><span class="edm-badge edm-badge-sd">${(item.container || 'file').toUpperCase()}</span></td>
                <td>${sizeStr}</td>
                <td>
                    <div style="font-weight: 600;">${item.status === 'COMPLETED' ? '100%' : (item.progress?.percentage || 0) + '%'}</div>
                    <div class="edm-progress-track" style="width: 100px; height: 4px; margin-top: 4px;">
                        <div class="edm-progress-fill" style="width: ${item.status === 'COMPLETED' ? 100 : (item.progress?.percentage || 0)}%;"></div>
                    </div>
                </td>
                <td><span class="edm-badge edm-badge-720p">${item.status || 'COMPLETED'}</span></td>
                <td style="color: var(--edm-text-muted);">${dateStr}</td>
                <td style="text-align: right;">
                    <button class="edm-btn-secondary" style="padding: 4px 8px; font-size: 11px;">Details</button>
                </td>
            `;
            tableBody.appendChild(tr);
        });

        // Update stats
        document.getElementById('statTotalDownloads').textContent = allDownloads.length;
        document.getElementById('statActiveDownloads').textContent = allDownloads.filter(i => i.status === 'DOWNLOADING' || i.status === 'STARTED').length;
        const totalBytes = allDownloads.reduce((acc, curr) => acc + (curr.size || 0), 0);
        document.getElementById('statTotalData').textContent = formatBytes(totalBytes);
    }

    async function loadData() {
        chrome.storage.local.get(['downloadHistory'], (data) => {
            allDownloads = data.downloadHistory || [
                { filename: "Sample_Video_1080p.mp4", container: "mp4", size: 45200000, status: "COMPLETED", createdAt: Date.now() - 3600000, url: "https://example.com/video.mp4" },
                { filename: "EDM_Desktop_Installer_v2.0.exe", container: "exe", size: 68400000, status: "COMPLETED", createdAt: Date.now() - 7200000, url: "https://edm.app/download/EDM_Setup.exe" }
            ];
            renderTable();
        });
    }

    // Category Tabs
    document.querySelectorAll('.edm-dash-tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.edm-dash-tab-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentCategory = btn.getAttribute('data-category');
            renderTable();
        });
    });

    // Search
    document.getElementById('dashSearchInput').addEventListener('input', (e) => {
        searchQuery = e.target.value;
        renderTable();
    });

    // Refresh
    document.getElementById('refreshDashBtn').addEventListener('click', loadData);

    loadData();
});
