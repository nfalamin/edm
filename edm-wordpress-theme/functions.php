<?php
/**
 * EDM Theme Functions and Definitions
 *
 * @link https://developer.wordpress.org/themes/basics/theme-functions/
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

// Resilient base directory path
$theme_base_dir = function_exists('get_template_directory') ? get_template_directory() : __DIR__;
$inc_dir = $theme_base_dir . '/inc';
if (!is_dir($inc_dir)) {
    $inc_dir = __DIR__ . '/inc';
}

$required_files = [
    '/setup.php',
    '/enqueue.php',
    '/theme-functions.php',
    '/security.php',
    '/helpers.php',
    '/custom-post-types.php',
    '/customizer.php'
];

foreach ($required_files as $rel_path) {
    $primary_path = $inc_dir . $rel_path;
    $fallback_path = __DIR__ . '/inc' . $rel_path;
    $flat_fallback = __DIR__ . $rel_path;

    if (file_exists($primary_path)) {
        require_once $primary_path;
    } elseif (file_exists($fallback_path)) {
        require_once $fallback_path;
    } elseif (file_exists($flat_fallback)) {
        require_once $flat_fallback;
    }
}
