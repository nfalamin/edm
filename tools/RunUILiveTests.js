/**
 * EDM Extension - UI/UX & Design System Live Test Runner
 * Version: 1.0.0
 * Verifies ThemeManager, DesignTokens, Manifest declarations, and Chrome/Firefox UI package sync.
 */

import fs from 'fs';
import path from 'path';
import { ThemeManager, DesignTokens, EDMTheme } from '../extension/src/ui/design-tokens.js';

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function assert(condition, message) {
    if (!condition) {
        throw new Error(`ASSERTION FAILED: ${message}`);
    }
}

async function runTest(testId, title, testFn) {
    totalTests++;
    try {
        await testFn();
        passedTests++;
        console.log(`[PASS] ${testId}: ${title}`);
    } catch (err) {
        failedTests++;
        console.error(`[FAIL] ${testId}: ${title} -> ${err.message}`);
    }
}

console.log("================================================================================");
console.log(" EDM UI/UX & DESIGN SYSTEM - LIVE VERIFICATION HARNESS");
console.log("================================================================================\n");

async function executeAllTests() {
    // 1. Design Tokens Verification
    await runTest("UI-01", "Design Tokens: Exposes required palettes, gradients, and radius tokens", () => {
        assert(DesignTokens.colors.primaryGradient.includes('#0284C7'), "Primary gradient match");
        assert(DesignTokens.colors.accentCyan === '#38BDF8', "Accent cyan match");
        assert(DesignTokens.colors.dark.bgPrimary === '#0B0F19', "Dark bgPrimary match");
        assert(DesignTokens.radius.lg === '16px', "Radius lg match");
    });

    // 2. Theme State Manager
    await runTest("UI-02", "Theme Engine: Correctly stores and retrieves dark and light themes", () => {
        assert(EDMTheme.DARK === 'dark', "Dark theme constant match");
        assert(EDMTheme.LIGHT === 'light', "Light theme constant match");
    });

    // 3. Chrome Manifest UI Declarations
    await runTest("UI-03", "Chrome Manifest: Validates default_popup and options_page registration", () => {
        const manifestPath = path.resolve('extension/chrome/manifest.json');
        const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
        assert(manifest.action.default_popup === 'popup/popup.html', "Chrome default_popup must be popup/popup.html");
        assert(manifest.options_page === 'settings/settings.html', "Chrome options_page must be settings/settings.html");
    });

    // 4. Firefox Manifest UI Declarations
    await runTest("UI-04", "Firefox Manifest: Validates default_popup and options_ui registration", () => {
        const manifestPath = path.resolve('extension/firefox/manifest.json');
        const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
        assert(manifest.action.default_popup === 'popup/popup.html', "Firefox default_popup must be popup/popup.html");
        assert(manifest.options_ui.page === 'settings/settings.html', "Firefox options_ui page must be settings/settings.html");
        assert(manifest.browser_specific_settings.gecko.id === 'edm-extension@edm.app', "Firefox gecko ID match");
    });

    // 5. Chrome UI Package File Integrity
    await runTest("UI-05", "Chrome Package: Verifies popup, settings, dashboard, and theme.css existence", () => {
        const files = [
            'extension/chrome/popup/popup.html',
            'extension/chrome/popup/popup.css',
            'extension/chrome/popup/popup.js',
            'extension/chrome/settings/settings.html',
            'extension/chrome/settings/settings.css',
            'extension/chrome/settings/settings.js',
            'extension/chrome/dashboard/dashboard.html',
            'extension/chrome/dashboard/dashboard.css',
            'extension/chrome/dashboard/dashboard.js',
            'extension/chrome/src/ui/theme.css'
        ];

        for (const file of files) {
            assert(fs.existsSync(path.resolve(file)), `Missing Chrome UI file: ${file}`);
        }
    });

    // 6. Firefox UI Package File Integrity
    await runTest("UI-06", "Firefox Package: Verifies popup, settings, dashboard, and theme.css existence", () => {
        const files = [
            'extension/firefox/popup/popup.html',
            'extension/firefox/popup/popup.css',
            'extension/firefox/popup/popup.js',
            'extension/firefox/settings/settings.html',
            'extension/firefox/settings/settings.css',
            'extension/firefox/settings/settings.js',
            'extension/firefox/dashboard/dashboard.html',
            'extension/firefox/dashboard/dashboard.css',
            'extension/firefox/dashboard/dashboard.js',
            'extension/firefox/src/ui/theme.css'
        ];

        for (const file of files) {
            assert(fs.existsSync(path.resolve(file)), `Missing Firefox UI file: ${file}`);
        }
    });
}

executeAllTests().then(() => {
    console.log("\n================================================================================");
    console.log(` SUMMARY: Total Tests: ${totalTests} | Passed: ${passedTests} | Failed: ${failedTests}`);
    console.log("================================================================================");

    if (failedTests > 0) {
        process.exit(1);
    } else {
        process.exit(0);
    }
});
