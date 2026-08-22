/**
 * EDM Extension - Settings Controller
 * Version: 1.0.0
 */

import { ThemeManager } from '../src/ui/design-tokens.js';

document.addEventListener('DOMContentLoaded', async () => {
    // 1. Theme Management
    const currentTheme = ThemeManager.getStoredTheme();
    ThemeManager.applyTheme(currentTheme);

    const themeSelect = document.getElementById('themeSelect');
    themeSelect.value = currentTheme;
    themeSelect.addEventListener('change', () => {
        ThemeManager.setStoredTheme(themeSelect.value);
    });

    // 2. Tab Navigation
    const navItems = document.querySelectorAll('.edm-nav-item');
    const tabContents = document.querySelectorAll('.edm-tab-content');

    navItems.forEach(item => {
        item.addEventListener('click', () => {
            navItems.forEach(n => n.classList.remove('active'));
            tabContents.forEach(t => t.classList.remove('active'));

            item.classList.add('active');
            const tabId = `tab-${item.getAttribute('data-tab')}`;
            const target = document.getElementById(tabId);
            if (target) target.classList.add('active');
        });
    });

    // 3. Storage Settings (General & Media)
    const settingAutoCapture = document.getElementById('settingAutoCapture');
    const settingAltBypass = document.getElementById('settingAltBypass');
    const settingDedupGuard = document.getElementById('settingDedupGuard');
    const settingFloatingBar = document.getElementById('settingFloatingBar');
    const settingIgnoreThumbnails = document.getElementById('settingIgnoreThumbnails');

    chrome.storage.local.get([
        'autoCaptureEnabled',
        'altBypassEnabled',
        'dedupGuardEnabled',
        'floatingBarEnabled',
        'ignoreThumbnailsEnabled',
        'customExtensions'
    ], (data) => {
        if (data.autoCaptureEnabled !== undefined) settingAutoCapture.checked = data.autoCaptureEnabled;
        if (data.altBypassEnabled !== undefined) settingAltBypass.checked = data.altBypassEnabled;
        if (data.dedupGuardEnabled !== undefined) settingDedupGuard.checked = data.dedupGuardEnabled;
        if (data.floatingBarEnabled !== undefined) settingFloatingBar.checked = data.floatingBarEnabled;
        if (data.ignoreThumbnailsEnabled !== undefined) settingIgnoreThumbnails.checked = data.ignoreThumbnailsEnabled;

        renderChips(data.customExtensions || defaultExtensions);
    });

    settingAutoCapture.addEventListener('change', () => chrome.storage.local.set({ autoCaptureEnabled: settingAutoCapture.checked }));
    settingAltBypass.addEventListener('change', () => chrome.storage.local.set({ altBypassEnabled: settingAltBypass.checked }));
    settingDedupGuard.addEventListener('change', () => chrome.storage.local.set({ dedupGuardEnabled: settingDedupGuard.checked }));
    settingFloatingBar.addEventListener('change', () => chrome.storage.local.set({ floatingBarEnabled: settingFloatingBar.checked }));
    settingIgnoreThumbnails.addEventListener('change', () => chrome.storage.local.set({ ignoreThumbnailsEnabled: settingIgnoreThumbnails.checked }));

    // 4. File Types Chips
    const defaultExtensions = ['exe', 'msi', 'zip', 'rar', '7z', 'iso', 'dmg', 'apk', 'pdf', 'mp4', 'mkv', 'webm', 'mp3', 'flac', 'wav'];
    let activeExtensions = [...defaultExtensions];

    function renderChips(exts) {
        activeExtensions = exts;
        const container = document.getElementById('extChipsContainer');
        container.innerHTML = '';

        activeExtensions.forEach((ext, idx) => {
            const chip = document.createElement('div');
            chip.className = 'edm-chip';
            chip.innerHTML = `<span>.${ext}</span><button class="edm-chip-remove" data-idx="${idx}">&times;</button>`;
            container.appendChild(chip);
        });

        document.querySelectorAll('.edm-chip-remove').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const idx = parseInt(e.currentTarget.getAttribute('data-idx'), 10);
                activeExtensions.splice(idx, 1);
                chrome.storage.local.set({ customExtensions: activeExtensions });
                renderChips(activeExtensions);
            });
        });
    }

    document.getElementById('addExtBtn').addEventListener('click', () => {
        const input = document.getElementById('newExtInput');
        const val = input.value.trim().toLowerCase().replace(/^\./, '');
        if (val && !activeExtensions.includes(val)) {
            activeExtensions.push(val);
            chrome.storage.local.set({ customExtensions: activeExtensions });
            renderChips(activeExtensions);
            input.value = '';
        }
    });

    // 5. Diagnostics
    const diagConsole = document.getElementById('diagConsole');
    const diagStatusDot = document.getElementById('diagStatusDot');
    const diagStatusText = document.getElementById('diagStatusText');
    const testConnectionBtn = document.getElementById('testConnectionBtn');

    function logDiag(msg) {
        const timestamp = new Date().toLocaleTimeString();
        diagConsole.textContent += `\n[${timestamp}] ${msg}`;
        diagConsole.scrollTop = diagConsole.scrollHeight;
    }

    testConnectionBtn.addEventListener('click', () => {
        logDiag("Initiating Ping to Native Host: com.edm.downloader...");
        diagStatusDot.className = 'edm-status-dot edm-status-connecting';
        diagStatusText.textContent = 'Testing connection...';

        const startTime = Date.now();
        chrome.runtime.sendMessage({ action: 'TEST_NATIVE_PING' }, (res) => {
            const duration = Date.now() - startTime;
            if (chrome.runtime.lastError || !res || !res.success) {
                diagStatusDot.className = 'edm-status-dot edm-status-offline';
                diagStatusText.textContent = 'Connection Failed';
                logDiag(`ERROR: Native Host did not respond (${chrome.runtime.lastError?.message || 'Offline'}).`);
            } else {
                diagStatusDot.className = 'edm-status-dot edm-status-online';
                diagStatusText.textContent = `Connected (${duration}ms)`;
                logDiag(`SUCCESS: Received PONG from EDM.NativeHost.exe (Version: ${res.version || '2.0.0'}, Roundtrip: ${duration}ms).`);
            }
        });
    });
});
