/**
 * EDM (Exclusive Download Manager) - Shared Design Tokens & Theme Engine
 * Version: 1.0.0
 * Unified design variables and theme state management for popup, settings, floating bar, and dashboard.
 */

export const EDMTheme = Object.freeze({
    DARK: 'dark',
    LIGHT: 'light',
    SYSTEM: 'system'
});

export const DesignTokens = Object.freeze({
    colors: {
        primaryGradient: 'linear-gradient(135deg, #0284C7 0%, #0EA5E9 100%)',
        accentBlue: '#0284C7',
        accentCyan: '#38BDF8',
        accentEmerald: '#10B981',
        accentPurple: '#8B5CF6',
        accentAmber: '#F59E0B',
        accentRose: '#F43F5E',
        dark: {
            bgPrimary: '#0B0F19',
            bgSecondary: '#0F172A',
            bgCard: 'rgba(15, 23, 42, 0.88)',
            borderGlass: 'rgba(255, 255, 255, 0.12)',
            textPrimary: '#F8FAFC',
            textSecondary: '#94A3B8',
            textMuted: '#64748B'
        },
        light: {
            bgPrimary: '#F8FAFC',
            bgSecondary: '#FFFFFF',
            bgCard: 'rgba(255, 255, 255, 0.92)',
            borderGlass: 'rgba(0, 0, 0, 0.1)',
            textPrimary: '#0F172A',
            textSecondary: '#475569',
            textMuted: '#94A3B8'
        }
    },
    shadows: {
        glass: '0 16px 40px rgba(0, 0, 0, 0.5), 0 0 24px rgba(2, 132, 199, 0.25)',
        card: '0 4px 20px rgba(0, 0, 0, 0.15)',
        button: '0 4px 14px rgba(2, 132, 199, 0.4)'
    },
    radius: {
        sm: '6px',
        md: '10px',
        lg: '16px',
        full: '9999px'
    }
});

export class ThemeManager {
    static getStoredTheme() {
        return localStorage.getItem('edm_theme') || EDMTheme.DARK;
    }

    static setStoredTheme(theme) {
        localStorage.setItem('edm_theme', theme);
        ThemeManager.applyTheme(theme);
    }

    static applyTheme(theme) {
        const root = document.documentElement;
        let activeTheme = theme;

        if (theme === EDMTheme.SYSTEM) {
            const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
            activeTheme = prefersDark ? EDMTheme.DARK : EDMTheme.LIGHT;
        }

        root.setAttribute('data-theme', activeTheme);
    }
}
