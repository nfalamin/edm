<?php
/**
 * Dashboard Sidebar Navigation Template Part
 * Exact Module Alignment for EDM Control Dashboard
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<aside class="app-sidebar" id="sidebar">
    <div class="sidebar-header">
        <a href="<?php echo esc_url(home_url('/')); ?>" class="sidebar-brand-link">
            <div class="brand-logo-wrap">
                <i data-lucide="zap" style="width: 18px; height: 18px;"></i>
            </div>
            <div class="brand-info">
                <span class="brand-name">EDM Control Plane</span>
                <span class="brand-tagline">v2.1.0 · Super Admin</span>
            </div>
        </a>
    </div>

    <!-- Scrollable Navigation Items -->
    <nav class="sidebar-nav" aria-label="<?php esc_attr_e('Dashboard Navigation', 'edm-theme'); ?>">
        
        <!-- 1. OVERVIEW & LIVE MAP -->
        <div class="nav-section">
            <button class="nav-item active" data-page="dashboard" onclick="if(window.edmDashboard) window.edmDashboard.navigate('dashboard');">
                <span class="nav-item-icon"><i data-lucide="layout-dashboard"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Overview', 'edm-theme'); ?></span>
            </button>
            <button class="nav-item" data-page="live-map" onclick="if(window.edmDashboard) window.edmDashboard.navigate('live-map');">
                <span class="nav-item-icon"><i data-lucide="globe"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Live World Map', 'edm-theme'); ?></span>
            </button>
        </div>

        <!-- 2. LANDING PAGE & RELEASES -->
        <div class="nav-section">
            <div class="nav-section-title"><?php esc_html_e('Distribution & Content', 'edm-theme'); ?></div>
            
            <button class="nav-item" data-page="content" onclick="if(window.edmDashboard) window.edmDashboard.navigate('content');">
                <span class="nav-item-icon"><i data-lucide="layout-template"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Landing Page', 'edm-theme'); ?></span>
            </button>
            
            <button class="nav-item" data-page="releases" onclick="if(window.edmDashboard) window.edmDashboard.navigate('releases');">
                <span class="nav-item-icon"><i data-lucide="package"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Releases', 'edm-theme'); ?></span>
            </button>
            
            <button class="nav-item" data-page="extensions" onclick="if(window.edmDashboard) window.edmDashboard.navigate('extensions');">
                <span class="nav-item-icon"><i data-lucide="puzzle"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Extensions', 'edm-theme'); ?></span>
            </button>
        </div>

        <!-- 3. ANALYTICS & TRIALS -->
        <div class="nav-section">
            <div class="nav-section-title"><?php esc_html_e('Intelligence & Licensing', 'edm-theme'); ?></div>
            
            <button class="nav-item" data-page="analytics" onclick="if(window.edmDashboard) window.edmDashboard.navigate('analytics');">
                <span class="nav-item-icon"><i data-lucide="bar-chart-3"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Analytics', 'edm-theme'); ?></span>
            </button>
            
            <button class="nav-item" data-page="trials" onclick="if(window.edmDashboard) window.edmDashboard.navigate('trials');">
                <span class="nav-item-icon"><i data-lucide="clock"></i></span>
                <span class="nav-item-text"><?php esc_html_e('30-Day Trial', 'edm-theme'); ?></span>
            </button>
            
            <button class="nav-item" data-page="promotions" onclick="if(window.edmDashboard) window.edmDashboard.navigate('promotions');">
                <span class="nav-item-icon"><i data-lucide="tag"></i></span>
                <span class="nav-item-text"><?php esc_html_e('Coupons / Offers', 'edm-theme'); ?></span>
            </button>
        </div>

        <!-- 4. SYSTEM & AUDIT -->
        <div class="nav-section">
            <div class="nav-section-title"><?php esc_html_e('System & Security', 'edm-theme'); ?></div>
            
            <button class="nav-item" data-page="audit-logs" onclick="if(window.edmDashboard) window.edmDashboard.navigate('audit-logs');">
                <span class="nav-item-icon"><i data-lucide="shield-check"></i></span>
                <span class="nav-item-text"><?php esc_html_e('System Security & Logs', 'edm-theme'); ?></span>
            </button>
            
            <button class="nav-item" data-page="system-health" onclick="if(window.edmDashboard) window.edmDashboard.navigate('system-health');">
                <span class="nav-item-icon"><i data-lucide="activity"></i></span>
                <span class="nav-item-text"><?php esc_html_e('System Configuration', 'edm-theme'); ?></span>
            </button>
        </div>
    </nav>

    <!-- Sidebar Bottom User Profile -->
    <div class="sidebar-footer">
        <div class="user-profile-badge">
            <div class="user-avatar-box">SA</div>
            <div class="user-profile-text">
                <span class="user-name">Super Admin</span>
                <span class="user-role">admin@edm-download.org</span>
            </div>
            <a href="<?php echo esc_url(home_url('/')); ?>" title="<?php esc_attr_e('Back to Main Site', 'edm-theme'); ?>" class="btn-exit-dash">
                <i data-lucide="log-out" style="width: 16px; height: 16px;"></i>
            </a>
        </div>
    </div>
</aside>
