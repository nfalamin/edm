<?php
/**
 * EDM Theme Functions & Asset Loader
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly
}

function edm_theme_setup() {
    add_theme_support('title-tag');
    add_theme_support('post-thumbnails');
    add_theme_support('html5', array('search-form', 'comment-form', 'comment-list', 'gallery', 'caption'));
}
add_action('after_setup_theme', 'edm_theme_setup');

function edm_theme_enqueue_scripts() {
    // 1. Google Fonts
    wp_enqueue_style('edm-fonts', 'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700&display=swap', array(), null);

    // 2. Lucide Icons CDN
    wp_enqueue_script('lucide-icons', 'https://unpkg.com/lucide@latest', array(), null, true);

    // 3. Chart.js CDN (Loaded on Dashboard page)
    if (is_page_template('page-dashboard.php') || is_page('dashboard') || is_front_page()) {
        wp_enqueue_script('chart-js', 'https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js', array(), '4.4.1', true);
    }

    // 4. Stylesheets
    wp_enqueue_style('edm-main-style', get_stylesheet_uri(), array(), '1.3.0');

    if (is_page_template('page-dashboard.php') || is_page('dashboard')) {
        wp_enqueue_style('edm-dashboard-style', get_template_directory_uri() . '/assets/css/dashboard.css', array(), '1.3.0');
        wp_enqueue_script('edm-mock-data', get_template_directory_uri() . '/assets/js/mock-data.js', array(), '1.3.0', true);
        wp_enqueue_script('edm-dashboard-app', get_template_directory_uri() . '/assets/js/dashboard-app.js', array('chart-js', 'lucide-icons'), '1.3.0', true);
    } else {
        wp_enqueue_style('edm-landing-style', get_template_directory_uri() . '/assets/css/landing.css', array(), '1.3.0');
        wp_enqueue_script('edm-landing-app', get_template_directory_uri() . '/assets/js/landing-app.js', array('lucide-icons'), '1.3.0', true);
    }
}
add_action('wp_enqueue_scripts', 'edm_theme_enqueue_scripts');
