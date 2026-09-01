<?php
/**
 * EDM Theme Setup & Feature Registrations
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

if (!function_exists('edm_theme_setup')) {
    /**
     * Sets up theme defaults and registers support for various WordPress features.
     */
    function edm_theme_setup() {
        // Make theme available for translation.
        load_theme_textdomain('edm-theme', get_template_directory() . '/languages');

        // Let WordPress manage the document title.
        add_theme_support('title-tag');

        // Enable support for Post Thumbnails on posts and pages.
        add_theme_support('post-thumbnails');

        // Switch default core markup for search form, comment form, and comments to output valid HTML5.
        add_theme_support('html5', array(
            'search-form',
            'comment-form',
            'comment-list',
            'gallery',
            'caption',
            'style',
            'script',
        ));

        // Add support for core custom logo.
        add_theme_support('custom-logo', array(
            'height'      => 48,
            'width'       => 160,
            'flex-width'  => true,
            'flex-height' => true,
        ));

        // Register Primary Navigation Menus.
        register_nav_menus(array(
            'primary-menu'  => esc_html__('Primary Header Menu', 'edm-theme'),
            'mobile-menu'   => esc_html__('Mobile Drawer Menu', 'edm-theme'),
            'footer-legal'  => esc_html__('Footer Legal Menu', 'edm-theme'),
            'footer-links'  => esc_html__('Footer Product Menu', 'edm-theme'),
        ));
    }
}
add_action('after_setup_theme', 'edm_theme_setup');

/**
 * Register widget area / sidebars.
 */
function edm_widgets_init() {
    register_sidebar(array(
        'name'          => esc_html__('Sidebar', 'edm-theme'),
        'id'            => 'sidebar-1',
        'description'   => esc_html__('Add widgets here.', 'edm-theme'),
        'before_widget' => '<section id="%1$s" class="widget %2$s">',
        'after_widget'  => '</section>',
        'before_title'  => '<h3 class="widget-title">',
        'after_title'   => '</h3>',
    ));
}
add_action('widgets_init', 'edm_widgets_init');
