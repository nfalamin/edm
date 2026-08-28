<?php
/**
 * EDM Theme — Functions & Enqueue
 * Premium WordPress theme for Exclusive Download Manager v2.1.0
 */

if (!defined('ABSPATH')) exit;

// ── Theme Support ────────────────────────────────────────────────
function edm_theme_setup() {
    add_theme_support('title-tag');
    add_theme_support('post-thumbnails');
    add_theme_support('html5', ['search-form','comment-form','comment-list','gallery','caption']);
    add_theme_support('custom-logo', [
        'height'      => 48,
        'width'       => 160,
        'flex-width'  => true,
        'flex-height' => true,
    ]);
    add_theme_support('responsive-embeds');
    add_theme_support('editor-styles');

    register_nav_menus([
        'primary-menu' => __('Primary Navigation', 'edm-theme'),
        'footer-menu'  => __('Footer Navigation',  'edm-theme'),
    ]);
}
add_action('after_setup_theme', 'edm_theme_setup');

// ── Enqueue Scripts & Styles ─────────────────────────────────────
function edm_enqueue_assets() {
    $ver = '2.1.0';

    // Google Fonts
    wp_enqueue_style(
        'edm-fonts',
        'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800;900&family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
        [],
        null
    );

    // Lucide Icons
    wp_enqueue_script(
        'lucide-icons',
        'https://unpkg.com/lucide@latest',
        [],
        null,
        true
    );

    // Main Theme CSS
    wp_enqueue_style(
        'edm-theme-css',
        get_template_directory_uri() . '/assets/css/edm-theme.css',
        ['edm-fonts'],
        $ver
    );

    // WordPress style.css (theme header)
    wp_enqueue_style(
        'edm-style',
        get_stylesheet_uri(),
        ['edm-theme-css'],
        $ver
    );

    // Theme JavaScript
    wp_enqueue_script(
        'edm-theme-js',
        get_template_directory_uri() . '/assets/js/edm-theme.js',
        ['jquery'],
        $ver,
        true
    );

    // Pass WP data to JS
    wp_localize_script('edm-theme-js', 'EDM_WP', [
        'ajaxUrl'  => admin_url('admin-ajax.php'),
        'nonce'    => wp_create_nonce('edm_nonce'),
        'themeUrl' => get_template_directory_uri(),
        'siteUrl'  => get_site_url(),
        'version'  => $ver,
    ]);
}
add_action('wp_enqueue_scripts', 'edm_enqueue_assets');

// ── Responsive Image Srcsets ─────────────────────────────────────
function edm_custom_image_sizes() {
    add_image_size('edm-hero',    1920, 900,  true);
    add_image_size('edm-feature', 800,  600,  true);
    add_image_size('edm-thumb',   400,  300,  true);
    add_image_size('edm-square',  600,  600,  true);
}
add_action('after_setup_theme', 'edm_custom_image_sizes');

// ── Widgets ──────────────────────────────────────────────────────
function edm_register_widgets() {
    register_sidebar([
        'name'          => __('Footer Column 1', 'edm-theme'),
        'id'            => 'footer-col-1',
        'before_widget' => '<div class="edm-footer-widget">',
        'after_widget'  => '</div>',
        'before_title'  => '<h4 class="edm-footer-col-title">',
        'after_title'   => '</h4>',
    ]);
    register_sidebar([
        'name'          => __('Footer Column 2', 'edm-theme'),
        'id'            => 'footer-col-2',
        'before_widget' => '<div class="edm-footer-widget">',
        'after_widget'  => '</div>',
        'before_title'  => '<h4 class="edm-footer-col-title">',
        'after_title'   => '</h4>',
    ]);
}
add_action('widgets_init', 'edm_register_widgets');

// ── Shortcodes ───────────────────────────────────────────────────
// [edm_download_btn] — renders primary download button
function edm_download_btn_shortcode($atts) {
    $atts = shortcode_atts([
        'text'    => 'Download EDM Free',
        'url'     => '/download',
        'version' => '2.1.0',
        'size'    => 'large',
        'icon'    => '⬇',
    ], $atts, 'edm_download_btn');

    $size_class = $atts['size'] === 'large' ? 'edm-btn-lg' : 'edm-btn-sm';
    return sprintf(
        '<a href="%s" class="edm-btn edm-btn-primary %s"><span>%s</span> %s <span class="edm-badge edm-badge-success" style="font-size:11px;">v%s</span></a>',
        esc_url($atts['url']),
        esc_attr($size_class),
        esc_html($atts['icon']),
        esc_html($atts['text']),
        esc_html($atts['version'])
    );
}
add_shortcode('edm_download_btn', 'edm_download_btn_shortcode');

// [edm_badge text="..." type="primary|success|warning"] — inline badge
function edm_badge_shortcode($atts, $content = '') {
    $atts = shortcode_atts(['text' => '', 'type' => 'primary'], $atts, 'edm_badge');
    return sprintf(
        '<span class="edm-badge edm-badge-%s">%s</span>',
        esc_attr($atts['type']),
        esc_html($atts['text'] ?: $content)
    );
}
add_shortcode('edm_badge', 'edm_badge_shortcode');

// ── Custom Body Classes ──────────────────────────────────────────
function edm_body_classes($classes) {
    $classes[] = 'edm-theme';
    if (is_front_page()) $classes[] = 'edm-home';
    return $classes;
}
add_filter('body_class', 'edm_body_classes');

// ── Remove WordPress Emoji (Performance) ────────────────────────
function edm_disable_emojis() {
    remove_action('wp_head',             'print_emoji_detection_script', 7);
    remove_action('admin_print_scripts', 'print_emoji_detection_script');
    remove_action('wp_print_styles',     'print_emoji_styles');
    remove_action('admin_print_styles',  'print_emoji_styles');
    remove_filter('the_content_feed',    'wp_staticize_emoji');
    remove_filter('comment_text_rss',    'wp_staticize_emoji');
    remove_filter('wp_mail',             'wp_staticize_emoji_for_email');
}
add_action('init', 'edm_disable_emojis');

// ── SEO Meta Tags ────────────────────────────────────────────────
function edm_seo_meta_tags() {
    if (is_front_page()) {
        echo '<meta name="description" content="EDM - Exclusive Download Manager. Lightning-fast multi-threaded downloads with browser extension support. Free download for Windows.">' . "\n";
        echo '<meta property="og:type" content="website">' . "\n";
        echo '<meta property="og:title" content="EDM — Exclusive Download Manager">' . "\n";
        echo '<meta property="og:description" content="Next-generation download manager with 32-thread acceleration, browser integration, and smart bandwidth control.">' . "\n";
        echo '<meta property="og:url" content="' . esc_url(get_site_url()) . '">' . "\n";
        echo '<meta name="twitter:card" content="summary_large_image">' . "\n";
    }
}
add_action('wp_head', 'edm_seo_meta_tags');
