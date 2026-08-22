<?php
/**
 * Navigation Bar Template Part
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = edm_get_download_url();
$is_edm_page = (is_page('edm') || is_page_template('page-edm.php'));
?>
<!-- ══════════════════════════════════════════════════════════════
     STICKY GLASSMORPHIC NAVBAR
     ══════════════════════════════════════════════════════════════ -->
<header class="navbar" id="main-navbar">
    <div class="container navbar-container">
        <!-- Brand Logo -->
        <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-brand" aria-label="<?php esc_attr_e('Exclusive Download Manager', 'portfolio'); ?>">
            <img src="<?php echo esc_url(get_template_directory_uri() . '/edm-logo.png'); ?>" alt="EDM Logo" class="brand-logo-img" style="width: 34px; height: 34px; object-fit: contain;">
            <div class="brand-title-wrap">
                <span class="brand-title">EDM</span>
                <span class="brand-subtitle"><?php esc_html_e('Exclusive Download Manager', 'portfolio'); ?></span>
            </div>
        </a>

        <!-- Desktop Menu Links -->
        <nav class="nav-links" aria-label="<?php esc_attr_e('Main Navigation', 'portfolio'); ?>">
            <a href="<?php echo esc_url(home_url('/')); ?>" class="nav-link <?php echo is_front_page() ? 'active' : ''; ?>"><?php esc_html_e('Portfolio', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-link nav-link-highlight <?php echo $is_edm_page ? 'active' : ''; ?>">
                <i data-lucide="zap" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('EDM Hub', 'portfolio'); ?></span>
            </a>
            <a href="<?php echo esc_url(home_url('/edm/#turbo')); ?>" class="nav-link"><?php esc_html_e('32x Turbo', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm-extensions/')); ?>" class="nav-link"><?php esc_html_e('Extensions', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm-download/')); ?>" class="nav-link"><?php esc_html_e('Downloads', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm/#knowledge-hub')); ?>" class="nav-link"><?php esc_html_e('15-Step Guide', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm/#pricing')); ?>" class="nav-link"><?php esc_html_e('Pricing', 'portfolio'); ?></a>
            <a href="<?php echo esc_url(home_url('/edm/#faq')); ?>" class="nav-link"><?php esc_html_e('FAQ', 'portfolio'); ?></a>
        </nav>

        <!-- Right Action Items -->
        <div class="nav-actions">
            <!-- Theme Switcher -->
            <button type="button" class="btn-theme-toggle" id="btn-theme-toggle" title="<?php esc_attr_e('Toggle Theme', 'portfolio'); ?>" onclick="if(window.edmSite) window.edmSite.toggleTheme();" aria-label="<?php esc_attr_e('Toggle Theme', 'portfolio'); ?>">
                <i data-lucide="sun" id="theme-icon" style="width: 15px; height: 15px;"></i>
            </button>

            <!-- Primary CTA: Download EDM -->
            <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-sm" download>
                <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('Download EDM', 'portfolio'); ?></span>
            </a>

            <!-- Mobile Hamburger Toggle -->
            <button type="button" class="btn-hamburger" id="btn-hamburger" onclick="if(window.edmSite) window.edmSite.toggleMobileMenu();" aria-label="<?php esc_attr_e('Open Menu', 'portfolio'); ?>">
                <i data-lucide="menu" style="width: 20px; height: 20px;"></i>
            </button>
        </div>
    </div>
</header>

<!-- Mobile Navigation Drawer (Hidden on Desktop) -->
<div class="mobile-drawer" id="mobile-drawer" role="dialog" aria-modal="true" aria-label="<?php esc_attr_e('Mobile Navigation', 'portfolio'); ?>">
    <div style="display: flex; align-items: center; justify-content: space-between; padding-bottom: 12px; margin-bottom: 12px; border-bottom: 1px solid rgba(255,255,255,0.08);">
        <span style="font-size: 13px; font-weight: 700; color: #fff; text-transform: uppercase; letter-spacing: 1px;">Navigation Menu</span>
        <button type="button" onclick="if(window.edmSite) window.edmSite.toggleMobileMenu();" style="background: none; border: none; color: #94A3B8; font-size: 24px; cursor: pointer; padding: 0 4px;">&times;</button>
    </div>
    <a href="<?php echo esc_url(home_url('/')); ?>" class="mobile-nav-link <?php echo is_front_page() ? 'active' : ''; ?>">Portfolio Home</a>
    <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="mobile-nav-link mobile-nav-highlight <?php echo $is_edm_page ? 'active' : ''; ?>">
        <i data-lucide="zap" style="width: 14px; height: 14px;"></i> EDM Product Hub
    </a>
    <a href="<?php echo esc_url(home_url('/edm-extensions/')); ?>" class="mobile-nav-link">
        <i data-lucide="puzzle" style="width: 14px; height: 14px;"></i> Browser Extensions
    </a>
    <a href="<?php echo esc_url(home_url('/edm-download/')); ?>" class="mobile-nav-link">
        <i data-lucide="download-cloud" style="width: 14px; height: 14px;"></i> Official Downloads (19.8 MB)
    </a>
    <a href="<?php echo esc_url(home_url('/edm-features/')); ?>" class="mobile-nav-link">
        <i data-lucide="cpu" style="width: 14px; height: 14px;"></i> 32-Socket Architecture
    </a>
    <a href="<?php echo esc_url(home_url('/edm/#pricing')); ?>" class="mobile-nav-link">Pricing &amp; Plans</a>
    <a href="<?php echo esc_url(home_url('/edm/#faq')); ?>" class="mobile-nav-link">Technical FAQ</a>
    <a href="<?php echo esc_url(home_url('/about/')); ?>" class="mobile-nav-link">About Alamin</a>
    <a href="<?php echo esc_url(home_url('/services/')); ?>" class="mobile-nav-link">Services</a>
    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="mobile-nav-link">Contact &amp; Hire</a>
    <a href="<?php echo esc_url(home_url('/nf/')); ?>" class="mobile-nav-link" style="color: #38BDF8;">
        <i data-lucide="layout-dashboard" style="width: 14px; height: 14px;"></i> Super Admin (/nf)
    </a>
    <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary" style="width: 100%; margin-top: 14px;" download>
        <i data-lucide="download" style="width: 14px; height: 14px;"></i> Download EDM (19.8 MB)
    </a>
</div>
