<?php
/**
 * Dashboard Topbar Header Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<header class="app-topbar">
    <div class="topbar-left">
        <button class="btn-sidebar-toggle" id="btn-sidebar-toggle" onclick="if(window.edmDashboard) window.edmDashboard.toggleSidebar();" aria-label="<?php esc_attr_e('Toggle Sidebar', 'edm-theme'); ?>">
            <i data-lucide="menu" style="width: 20px; height: 20px;"></i>
        </button>
        <div class="topbar-breadcrumbs">
            <span class="crumb-root"><?php esc_html_e('Control Plane', 'edm-theme'); ?></span>
            <span class="crumb-separator">/</span>
            <span class="crumb-current" id="dash-current-page-title"><?php esc_html_e('Dashboard Overview', 'edm-theme'); ?></span>
        </div>
    </div>

    <div class="topbar-right">
        <!-- System Health Pill -->
        <div class="status-health-badge">
            <span class="pulse-health-dot"></span>
            <span id="topbar-health-status"><?php esc_html_e('API: Operational', 'edm-theme'); ?></span>
        </div>

        <!-- Global Quick Search -->
        <div class="topbar-search-box">
            <i data-lucide="search" style="width: 15px; height: 15px; color: var(--edm-text-muted);"></i>
            <input type="text" id="dash-global-search" placeholder="<?php esc_attr_e('Search users, licenses, releases...', 'edm-theme'); ?>" oninput="if(window.edmDashboard) window.edmDashboard.handleGlobalSearch(this.value);" />
        </div>

        <!-- Theme Switcher -->
        <button class="btn-dash-icon" onclick="if(window.edmSite) window.edmSite.toggleTheme();" title="<?php esc_attr_e('Toggle Theme', 'edm-theme'); ?>" aria-label="<?php esc_attr_e('Toggle Theme', 'edm-theme'); ?>">
            <i data-lucide="sun" style="width: 16px; height: 16px;"></i>
        </button>

        <!-- Exit to Public Website -->
        <a href="<?php echo esc_url(home_url('/')); ?>" class="btn btn-outline btn-sm">
            <i data-lucide="globe" style="width: 14px; height: 14px;"></i>
            <span><?php esc_html_e('View Public Site', 'edm-theme'); ?></span>
        </a>
    </div>
</header>
