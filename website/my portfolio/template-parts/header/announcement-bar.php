<?php
/**
 * Top Announcement Bar Template Part
 * Compact, responsive, and dismissible with localStorage persistence.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$show_notice = get_theme_mod('edm_show_announcement', true);
if (!$show_notice) {
    return;
}

$version = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';
$custom_text = get_theme_mod('edm_announcement_text', '');
$notice_text = !empty($custom_text) ? $custom_text : sprintf(__('⚡ EDM v%s Production Turbo Engine with 32-Socket Acceleration is Live!', 'edm-theme'), esc_html($version));
?>
<div class="top-notice-bar" id="top-announcement-bar" role="region" aria-label="<?php esc_attr_e('Announcement', 'edm-theme'); ?>">
    <div class="container top-notice-content">
        <div class="top-notice-left">
            <span class="badge-pulse" id="top-notice-badge"><?php esc_html_e('VERIFIED', 'edm-theme'); ?></span>
            <span id="top-notice-text" class="top-notice-title"><?php echo esc_html($notice_text); ?></span>
        </div>
        <div class="top-notice-right">
            <a href="<?php echo function_exists('edm_get_download_url') ? edm_get_download_url() : esc_url(home_url('/downloads/EDM-Setup-v2.1.0.exe')); ?>" class="notice-quick-link" download>
                <i data-lucide="download" style="width: 12px; height: 12px;"></i> <span><?php printf(esc_html__('Setup (%s)', 'edm-theme'), function_exists('edm_get_download_file_size') ? esc_html(edm_get_download_file_size()) : '19.8 MB'); ?></span>
            </a>
            <a href="<?php echo esc_url(home_url('/edm-extensions/')); ?>" class="notice-quick-link">
                <i data-lucide="puzzle" style="width: 12px; height: 12px;"></i> <span><?php esc_html_e('Extensions', 'edm-theme'); ?></span>
            </a>
            <a href="<?php echo esc_url(home_url('/nf/')); ?>" class="notice-dash-link">
                <i data-lucide="layout-dashboard" style="width: 12px; height: 12px;"></i> <span><?php esc_html_e('Control Plane', 'edm-theme'); ?></span>
            </a>
            <!-- Dismiss Button -->
            <button type="button" class="notice-close-btn" id="btn-close-notice" title="<?php esc_attr_e('Dismiss Notice', 'edm-theme'); ?>" aria-label="<?php esc_attr_e('Close', 'edm-theme'); ?>" onclick="dismissTopNotice();">
                &times;
            </button>
        </div>
    </div>
</div>

<script>
    (function() {
        if (localStorage.getItem('edm_notice_dismissed') === 'true') {
            var bar = document.getElementById('top-announcement-bar');
            if (bar) bar.style.display = 'none';
        }
    })();

    function dismissTopNotice() {
        var bar = document.getElementById('top-announcement-bar');
        if (bar) {
            bar.style.transition = 'opacity 0.25s ease, max-height 0.25s ease, padding 0.25s ease';
            bar.style.opacity = '0';
            bar.style.maxHeight = '0';
            bar.style.padding = '0';
            bar.style.overflow = 'hidden';
            setTimeout(function() { bar.style.display = 'none'; }, 260);
        }
        try {
            localStorage.setItem('edm_notice_dismissed', 'true');
        } catch(e) {}
    }
</script>
