<?php
/**
 * Top Announcement Bar Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$version = edm_get_latest_version();
?>
<div class="top-notice-bar" role="region" aria-label="<?php esc_attr_e('Announcement', 'edm-theme'); ?>">
    <div class="container top-notice-content">
        <div class="top-notice-left">
            <span class="badge-pulse" id="top-notice-badge"><?php esc_html_e('VERIFIED RELEASE', 'edm-theme'); ?></span>
            <span id="top-notice-text">
                <?php printf(esc_html__('⚡ EDM v%s Production Turbo Engine with 32-Socket Acceleration is Live!', 'edm-theme'), esc_html($version)); ?>
            </span>
        </div>
        <div class="top-notice-right">
            <a href="<?php echo esc_url(home_url('/dashboard/')); ?>" class="notice-dash-link">
                <i data-lucide="layout-dashboard" style="width: 12px; height: 12px;"></i> <?php esc_html_e('Dashboard', 'edm-theme'); ?>
            </a>
            <a href="<?php echo esc_url(home_url('/support/')); ?>">
                <i data-lucide="help-circle" style="width: 12px; height: 12px;"></i> <?php esc_html_e('Support Center', 'edm-theme'); ?>
            </a>
            <a href="<?php echo esc_url(edm_get_download_url()); ?>" download>
                <i data-lucide="download" style="width: 12px; height: 12px;"></i> <?php esc_html_e('Download Setup', 'edm-theme'); ?>
            </a>
            <a href="javascript:void(0)" onclick="if(window.edmSite) window.edmSite.toggleCurrency();" aria-label="<?php esc_attr_e('Toggle Currency', 'edm-theme'); ?>">
                <i data-lucide="globe" style="width: 12px; height: 12px;"></i> <span id="currency-label">BDT (৳)</span>
            </a>
        </div>
    </div>
</div>
