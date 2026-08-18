<?php
/**
 * EDM Custom Theme Functions & Template Utilities
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

/**
 * Get the latest verified EDM Version
 */
function edm_get_latest_version() {
    return apply_filters('edm_latest_version', '2.1.0');
}

/**
 * Get direct download link for verified EDM Windows Setup
 */
function edm_get_download_url() {
    $custom_url = get_theme_mod('edm_download_url', '');
    if (!empty($custom_url)) {
        return esc_url($custom_url);
    }
    return esc_url(get_template_directory_uri() . '/downloads/EDM-Setup-v2.1.0.exe');
}

/**
 * Format currency amounts with dynamic symbol
 */
function edm_format_price($bdt_amount, $currency = 'BDT') {
    if (strtoupper($currency) === 'USD') {
        $usd = round($bdt_amount / 120, 2);
        return '$' . number_format($usd, 2);
    }
    return '৳' . number_format($bdt_amount, 0);
}

/**
 * Output dynamic active state for navigation links
 */
function edm_nav_active_class($page_slug) {
    if (is_front_page() && ($page_slug === 'home' || $page_slug === 'index')) {
        return 'active';
    }
    if (is_page($page_slug) || is_page_template('page-' . $page_slug . '.php')) {
        return 'active';
    }
    return '';
}
