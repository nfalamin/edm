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

/**
 * Register Private Dashboard Rewrites (/nf)
 */
function edm_register_private_dashboard_routes() {
    add_rewrite_rule('^nf/?$', 'index.php?edm_private_dash=1', 'top');
    add_rewrite_rule('^nfdashbord/?$', 'index.php?edm_private_dash=1', 'top');
}
add_action('init', 'edm_register_private_dashboard_routes');

function edm_register_private_query_vars($vars) {
    $vars[] = 'edm_private_dash';
    return $vars;
}
add_filter('query_vars', 'edm_register_private_query_vars');

/**
 * Route /nf requests to page-dashboard.php with Security Headers
 */
function edm_route_private_dashboard_template($template) {
    $req_uri = isset($_SERVER['REQUEST_URI']) ? sanitize_text_field(wp_unslash($_SERVER['REQUEST_URI'])) : '';
    $is_nf_route = (get_query_var('edm_private_dash') == 1) || (preg_match('#^/nf(/|\?|$)#i', $req_uri));

    if ($is_nf_route) {
        // Enforce Search Engine Blocking
        if (!headers_sent()) {
            header('X-Robots-Tag: noindex, nofollow, noarchive, nosnippet', true);
        }
        
        $dash_template = locate_template(['page-dashboard.php', 'page-nfdashbord.php']);
        if ($dash_template) {
            return $dash_template;
        }
    }
    return $template;
}
add_filter('template_include', 'edm_route_private_dashboard_template', 99);

