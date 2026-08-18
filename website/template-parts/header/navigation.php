<?php
/**
 * Navigation Bar Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = edm_get_download_url();
?>
<!-- ══════════════════════════════════════════════════════════════
     STICKY GLASSMORPHIC NAVBAR
     ══════════════════════════════════════════════════════════════ -->
<header class="navbar" id="main-navbar">
    <div class="container navbar-container">
        <!-- Brand Logo -->
        <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-brand" aria-label="<?php esc_attr_e('Exclusive Download Manager', 'edm-theme'); ?>">
            <div class="brand-logo-box">
                <i data-lucide="zap" style="width: 20px; height: 20px;"></i>
            </div>
            <div class="brand-title-wrap">
                <span class="brand-title">EDM</span>
                <span class="brand-subtitle"><?php esc_html_e('Exclusive Download Manager', 'edm-theme'); ?></span>
            </div>
        </a>

        <!-- Desktop Menu Links -->
        <nav class="nav-links" aria-label="<?php esc_attr_e('Main Navigation', 'edm-theme'); ?>">
            <a href="<?php echo esc_url(home_url('/')); ?>" class="nav-link <?php echo is_front_page() ? 'active' : ''; ?>"><?php esc_html_e('Portfolio', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-link nav-link-highlight <?php echo (is_page('edm') || is_page_template('page-edm.php')) ? 'active' : ''; ?>">
                <i data-lucide="zap" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('EDM Hub (/edm)', 'edm-theme'); ?></span>
            </a>
            <a href="<?php echo esc_url(home_url('/features/')); ?>" class="nav-link <?php echo is_page('features') ? 'active' : ''; ?>"><?php esc_html_e('Features', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/technology/')); ?>" class="nav-link <?php echo is_page('technology') ? 'active' : ''; ?>"><?php esc_html_e('32x Turbo', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/browser-extension/')); ?>" class="nav-link <?php echo is_page('browser-extension') ? 'active' : ''; ?>"><?php esc_html_e('Extension', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/download/')); ?>" class="nav-link <?php echo is_page('download') ? 'active' : ''; ?>"><?php esc_html_e('Download', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/pricing/')); ?>" class="nav-link <?php echo is_page('pricing') ? 'active' : ''; ?>"><?php esc_html_e('Pricing', 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/changelog/')); ?>" class="nav-link <?php echo is_page('changelog') ? 'active' : ''; ?>"><?php esc_html_e("What's New", 'edm-theme'); ?></a>
            <a href="<?php echo esc_url(home_url('/dashboard/')); ?>" class="nav-link <?php echo is_page('dashboard') ? 'active' : ''; ?>">
                <i data-lucide="layout-dashboard" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('Dashboard', 'edm-theme'); ?></span>
            </a>
        </nav>

        <!-- Right Action Items -->
        <div class="nav-actions">
            <!-- Theme Switcher -->
            <button type="button" class="btn-theme-toggle" id="btn-theme-toggle" title="<?php esc_attr_e('Toggle Theme', 'edm-theme'); ?>" onclick="if(window.edmSite) window.edmSite.toggleTheme();" aria-label="<?php esc_attr_e('Toggle Theme', 'edm-theme'); ?>">
                <i data-lucide="sun" id="theme-icon" style="width: 15px; height: 15px;"></i>
            </button>

            <!-- Primary CTA: Download EDM -->
            <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-sm" download>
                <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('Download EDM', 'edm-theme'); ?></span>
            </a>

            <!-- Mobile Hamburger Toggle -->
            <button type="button" class="btn-hamburger" id="btn-hamburger" onclick="if(window.edmSite) window.edmSite.toggleMobileMenu();" aria-label="<?php esc_attr_e('Open Menu', 'edm-theme'); ?>">
                <i data-lucide="menu" style="width: 20px; height: 20px;"></i>
            </button>
        </div>
    </div>
</header>

<!-- Mobile Navigation Drawer (Hidden on Desktop) -->
<div class="mobile-drawer" id="mobile-drawer" role="dialog" aria-modal="true" aria-label="<?php esc_attr_e('Mobile Navigation', 'edm-theme'); ?>">
    <a href="<?php echo esc_url(home_url('/')); ?>" class="mobile-nav-link <?php echo is_front_page() ? 'active' : ''; ?>">Portfolio Home</a>
    <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="mobile-nav-link mobile-nav-highlight <?php echo (is_page('edm') || is_page_template('page-edm.php')) ? 'active' : ''; ?>">
        <i data-lucide="zap" style="width: 14px; height: 14px;"></i> EDM Product Hub (/edm)
    </a>
    <a href="<?php echo esc_url(home_url('/features/')); ?>" class="mobile-nav-link <?php echo is_page('features') ? 'active' : ''; ?>">Features</a>
    <a href="<?php echo esc_url(home_url('/technology/')); ?>" class="mobile-nav-link <?php echo is_page('technology') ? 'active' : ''; ?>">32x Turbo / Technology</a>
    <a href="<?php echo esc_url(home_url('/browser-extension/')); ?>" class="mobile-nav-link <?php echo is_page('browser-extension') ? 'active' : ''; ?>">Browser Extension</a>
    <a href="<?php echo esc_url(home_url('/download/')); ?>" class="mobile-nav-link <?php echo is_page('download') ? 'active' : ''; ?>">Download Setup</a>
    <a href="<?php echo esc_url(home_url('/pricing/')); ?>" class="mobile-nav-link <?php echo is_page('pricing') ? 'active' : ''; ?>">Pricing & Plans</a>
    <a href="<?php echo esc_url(home_url('/screenshots/')); ?>" class="mobile-nav-link <?php echo is_page('screenshots') ? 'active' : ''; ?>">Screenshots</a>
    <a href="<?php echo esc_url(home_url('/changelog/')); ?>" class="mobile-nav-link <?php echo is_page('changelog') ? 'active' : ''; ?>">What's New / Changelog</a>
    <a href="<?php echo esc_url(home_url('/faq/')); ?>" class="mobile-nav-link <?php echo is_page('faq') ? 'active' : ''; ?>">FAQ</a>
    <a href="<?php echo esc_url(home_url('/dashboard/')); ?>" class="mobile-nav-link <?php echo is_page('dashboard') ? 'active' : ''; ?>">
        <i data-lucide="layout-dashboard" style="width: 14px; height: 14px;"></i> Control Plane Dashboard (/dashboard)
    </a>
    <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary" style="width: 100%; margin-top: 10px;" download>
        <i data-lucide="download" style="width: 14px; height: 14px;"></i> Download EDM Setup
    </a>
</div>
