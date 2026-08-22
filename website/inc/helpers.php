<?php
/**
 * EDM Theme Route and Permalink Helpers
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

/**
 * Get internal URL safely with graceful fallback
 */
function edm_page_url($slug) {
    $page = get_page_by_path($slug);
    if ($page) {
        return esc_url(get_permalink($page->ID));
    }
    return esc_url(home_url('/' . $slug . '/'));
}

/**
 * Render Breadcrumbs navigation
 */
function edm_render_breadcrumbs($current_title = '') {
    echo '<nav class="edm-breadcrumbs" aria-label="Breadcrumb">';
    echo '<a href="' . esc_url(home_url('/')) . '">Home</a>';
    echo '<span class="separator">/</span>';
    if (!empty($current_title)) {
        echo '<span class="current">' . esc_html($current_title) . '</span>';
    } else {
        echo '<span class="current">' . esc_html(get_the_title()) . '</span>';
    }
    echo '</nav>';
}
