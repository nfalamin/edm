/**
 * EDM Extension - Popup Controller
 * Version: 1.0.0
 */

import { ThemeManager, EDMTheme } from '../src/ui/design-tokens.js';

document.addEventListener('DOMContentLoaded', async () => {
    // 1. Initialize Theme
    const currentTheme = ThemeManager.getStoredTheme();
    ThemeManager.applyTheme(currentTheme);

    const themeToggleBtn = document.getElementById('themeToggleBtn');
    themeToggleBtn.addEventListener('click', () => {
        const nextTheme = document.documentElement.getAttribute('data-theme') === 'dark' ? EDMTheme.LIGHT : EDMTheme.DARK;
        ThemeManager.setStoredTheme(nextTheme);
        themeToggleBtn.textContent = nextTheme === 'dark' ? '🌙' : '☀️';
    });
    themeToggleBtn.textContent = currentTheme === 'dark' ? '🌙' : '☀️';

    // 2. Navigation
    document.getElementById('openSettingsBtn').addEventListener('click', () => {
        if (chrome.runtime.openOptionsPage) {
            chrome.runtime.openOptionsPage();
        } else {
            window.open(chrome.runtime.getURL('settings/settings.html'));
        }
    });

    document.getElementById('openDashboardBtn').addEventListener('click', () => {
        window.open(chrome.runtime.getURL('dashboard/dashboard.html'));
    });

    // 3. Query Native Host Connection Status
    try {
        chrome.runtime.sendMessage({ action: 'GET_NATIVE_STATUS' }, (res) => {
            const statusDot = document.getElementById('statusDot');
            const statusText = document.getElementById('statusText');
            if (res && res.connected) {
                statusDot.className = 'edm-status-dot edm-status-online';
                statusText.textContent = 'Connected';
            } else {
                statusDot.className = 'edm-status-dot edm-status-offline';
                statusText.textContent = 'Disconnected';
            }
        });
    } catch (e) {
        console.warn("Could not query native status:", e);
    }

    // 4. Query Active Tab for Media Candidates
    try {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tab && tab.id) {
            chrome.tabs.sendMessage(tab.id, { action: 'GET_ACTIVE_MEDIA_SESSION' }, (response) => {
                if (chrome.runtime.lastError || !response || !response.session) {
                    return; // Keep empty state
                }

                const session = response.session;
                const reps = session.videoRepresentations || [];
                if (reps.length === 0) return;

                document.getElementById('mediaEmpty').style.display = 'none';
                document.getElementById('mediaDetails').style.display = 'block';
                document.getElementById('mediaCountBadge').textContent = `${reps.length} Available`;

                document.getElementById('mediaTitle').textContent = session.title || tab.title || 'Detected Video Stream';
                document.getElementById('mediaQuality').textContent = session.maximumAvailable ? session.maximumAvailable.qualityLabel : 'Auto Quality';

                const select = document.getElementById('popupFormatSelect');
                select.innerHTML = '';
                reps.forEach((rep) => {
                    const opt = document.createElement('option');
                    opt.value = rep.formatId;
                    opt.textContent = `${rep.qualityLabel || (rep.height + 'p')} (${rep.container.toUpperCase()}) ${rep.bitrate ? Math.round(rep.bitrate/1000) + 'kbps' : ''}`;
                    select.appendChild(opt);
                });

                document.getElementById('popupDownloadBtn').addEventListener('click', () => {
                    const selectedFormatId = select.value;
                    const chosenRep = reps.find(r => r.formatId === selectedFormatId) || reps[0];

                    chrome.runtime.sendMessage({
                        action: 'START_DOWNLOAD_REQUEST',
                        candidate: {
                            url: chosenRep.url,
                            videoUrl: chosenRep.videoUrl || chosenRep.url,
                            audioUrl: chosenRep.audioUrl || '',
                            quality: chosenRep.qualityLabel,
                            filename: `${session.title || 'video'}.${chosenRep.container || 'mp4'}`,
                            container: chosenRep.container || 'mp4',
                            requiresMerge: chosenRep.isVideoOnly,
                            size: chosenRep.estimatedSizeBytes
                        }
                    }, (res) => {
                        if (res && res.success) {
                            alert("Download dispatched to EDM Desktop!");
                        }
                    });
                });
            });
        }
    } catch (err) {
        console.warn("Error querying active tab media:", err);
    }

    // 5. Query Storage Settings
    const autoCaptureToggle = document.getElementById('autoCaptureToggle');
    chrome.storage.local.get(['autoCaptureEnabled'], (data) => {
        if (data.autoCaptureEnabled !== undefined) {
            autoCaptureToggle.checked = data.autoCaptureEnabled;
        }
    });

    autoCaptureToggle.addEventListener('change', () => {
        chrome.storage.local.set({ autoCaptureEnabled: autoCaptureToggle.checked });
    });
});
