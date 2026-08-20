<?php
/**
 * EDM Theme Security, Headers & Input Sanitization
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

/**
 * 1. Clean & sanitize text strings safely against XSS attacks
 */
function edm_clean_string($input) {
    if (is_array($input)) {
        return array_map('edm_clean_string', $input);
    }
    return sanitize_text_field(wp_unslash($input));
}

/**
 * 2. Clean JSON strings
 */
function edm_clean_json($json_string) {
    $decoded = json_decode($json_string, true);
    if ($decoded === null) {
        return '';
    }
    return wp_json_encode(edm_clean_string($decoded));
}

/**
 * 3. Remove WordPress generator meta tag for security
 */
remove_action('wp_head', 'wp_generator');

/**
 * 4. Production Security Headers
 */
function edm_security_headers() {
    if (!is_admin() && !headers_sent()) {
        header('X-Content-Type-Options: nosniff');
        header('X-Frame-Options: SAMEORIGIN');
        header('X-XSS-Protection: 1; mode=block');
        header('Referrer-Policy: strict-origin-when-cross-origin');
        header('Permissions-Policy: camera=(), microphone=(), geolocation=()');
        header("Content-Security-Policy: default-src 'self' https: data: 'unsafe-inline' 'unsafe-eval'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https:; connect-src 'self' https:;");
    }
}
add_action('send_headers', 'edm_security_headers');

/**
 * 5. Block Author Enumeration Attacks (?author=1)
 */
function edm_block_user_enumeration() {
    if (!is_admin() && isset($_REQUEST['author'])) {
        wp_safe_redirect(home_url(), 301);
        exit;
    }
}
add_action('template_redirect', 'edm_block_user_enumeration');

/**
 * 6. Disable XML-RPC for security hardening
 */
add_filter('xmlrpc_enabled', '__return_false');

/**
 * 7. Remove RSD & WLW manifest links
 */
remove_action('wp_head', 'rsd_link');
remove_action('wp_head', 'wlwmanifest_link');
