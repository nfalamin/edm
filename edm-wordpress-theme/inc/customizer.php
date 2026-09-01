<?php
/**
 * WordPress Customizer Settings & Controls for EDM / Portfolio Theme
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

function edm_customize_register($wp_customize) {
    $wp_customize->add_panel('edm_portfolio_panel', [
        'title'       => __('Portfolio Settings & Sections', 'edm-theme'),
        'description' => __('Customize the homepage sections, hero content, contact info, buttons and links.', 'edm-theme'),
        'priority'    => 25,
    ]);

    // 1. SECTION VISIBILITY
    $wp_customize->add_section('edm_section_visibility', [
        'title'       => __('Homepage Section Toggles', 'edm-theme'),
        'panel'       => 'edm_portfolio_panel',
        'description' => __('Toggle on/off specific sections on the front page.', 'edm-theme'),
        'priority'    => 10,
    ]);

    $sections = [
        'edm_show_hero'                 => [__('Show Hero Section', 'edm-theme'), true],
        'edm_show_trust'                => [__('Show Trust & Rating Bar', 'edm-theme'), true],
        'edm_show_services_preview'     => [__('Show Services Preview (3 Cards)', 'edm-theme'), true],
        'edm_show_testimonials_preview' => [__('Show Testimonials Strip', 'edm-theme'), true],
        'edm_show_cta_strip'            => [__('Show Final CTA Strip', 'edm-theme'), true],
    ];

    foreach ($sections as $setting_id => $data) {
        $wp_customize->add_setting($setting_id, [
            'default'           => $data[1],
            'sanitize_callback' => 'edm_sanitize_checkbox',
            'transport'         => 'refresh',
        ]);
        $wp_customize->add_control($setting_id, [
            'label'    => $data[0],
            'section'  => 'edm_section_visibility',
            'type'     => 'checkbox',
        ]);
    }

    // 2. HERO CONTENT
    $wp_customize->add_section('edm_hero_section', [
        'title'       => __('Hero Section Content', 'edm-theme'),
        'panel'       => 'edm_portfolio_panel',
        'priority'    => 20,
    ]);

    $wp_customize->add_setting('edm_hero_badge_text', [
        'default'           => 'Available for Projects',
        'sanitize_callback' => 'sanitize_text_field',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('edm_hero_badge_text', [
        'label'   => __('Badge Status Text', 'edm-theme'),
        'section' => 'edm_hero_section',
        'type'    => 'text',
    ]);

    $wp_customize->add_setting('edm_hero_desc', [
        'default'           => 'I build transparent, highly-optimized campaigns designed to maximize traffic, generate qualified leads, and grow revenue as a certified SEO Specialist, Google Ads Expert, and Social Media Marketing Strategist.',
        'sanitize_callback' => 'sanitize_textarea_field',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('edm_hero_desc', [
        'label'   => __('Hero Subtitle / Description', 'edm-theme'),
        'section' => 'edm_hero_section',
        'type'    => 'textarea',
    ]);

    // 3. CONTACT & SOCIAL CHANNELS
    $wp_customize->add_section('edm_contact_section', [
        'title'       => __('Contact Channels & Phone', 'edm-theme'),
        'panel'       => 'edm_portfolio_panel',
        'description' => __('Configure phone, WhatsApp, email, and booking links.', 'edm-theme'),
        'priority'    => 25,
    ]);

    $wp_customize->add_setting('portfolio_phone', [
        'default'           => '01888567189',
        'sanitize_callback' => 'sanitize_text_field',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('portfolio_phone', [
        'label'   => __('Direct Phone Number', 'edm-theme'),
        'section' => 'edm_contact_section',
        'type'    => 'text',
    ]);

    $wp_customize->add_setting('portfolio_whatsapp', [
        'default'           => '8801888567189',
        'sanitize_callback' => 'sanitize_text_field',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('portfolio_whatsapp', [
        'label'   => __('WhatsApp Number (International with Country Code)', 'edm-theme'),
        'section' => 'edm_contact_section',
        'type'    => 'text',
    ]);

    $wp_customize->add_setting('portfolio_email', [
        'default'           => 'nfxalamin@gmail.com',
        'sanitize_callback' => 'sanitize_email',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('portfolio_email', [
        'label'   => __('Direct Email Address', 'edm-theme'),
        'section' => 'edm_contact_section',
        'type'    => 'email',
    ]);

    // 4. EDM DOWNLOADS & CLOUD CDN URLs
    $wp_customize->add_section('edm_downloads_section', [
        'title'       => __('EDM Downloads & Cloud CDN', 'edm-theme'),
        'panel'       => 'edm_portfolio_panel',
        'description' => __('Configure GitHub Releases, Cloudflare R2, or CDN download links for apps and extensions.', 'edm-theme'),
        'priority'    => 30,
    ]);

    $wp_customize->add_setting('edm_download_url', [
        'default'           => '',
        'sanitize_callback' => 'esc_url_raw',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('edm_download_url', [
        'label'       => __('Windows Installer EXE URL (GitHub / CDN)', 'edm-theme'),
        'description' => __('Leave blank to use internal /downloads/ folder or paste your GitHub Release URL.', 'edm-theme'),
        'section'     => 'edm_downloads_section',
        'type'        => 'url',
    ]);

    $wp_customize->add_setting('edm_portable_url', [
        'default'           => '',
        'sanitize_callback' => 'esc_url_raw',
        'transport'         => 'refresh',
    ]);
    $wp_customize->add_control('edm_portable_url', [
        'label'       => __('Portable ZIP URL', 'edm-theme'),
        'section'     => 'edm_downloads_section',
        'type'        => 'url',
    ]);
}
add_action('customize_register', 'edm_customize_register');

function edm_sanitize_checkbox($checked) {
    return (isset($checked) && true === (bool) $checked) ? true : false;
}

if (!function_exists('edm_get_mod')) {
    function edm_get_mod($name, $default = '') {
        return get_theme_mod($name, $default);
    }
}

if (!function_exists('edm_get_phone')) {
    function edm_get_phone() {
        return get_theme_mod('portfolio_phone', '01888567189');
    }
}

if (!function_exists('edm_get_whatsapp')) {
    function edm_get_whatsapp() {
        return get_theme_mod('portfolio_whatsapp', '8801888567189');
    }
}

if (!function_exists('edm_get_email')) {
    function edm_get_email() {
        return get_theme_mod('portfolio_email', 'nfxalamin@gmail.com');
    }
}

if (!function_exists('edm_get_download_url')) {
    function edm_get_download_url() {
        $custom = get_theme_mod('edm_download_url', '');
        return !empty($custom) ? esc_url($custom) : esc_url(home_url('/downloads/EDM_Setup_v2.1.0.exe'));
    }
}

if (!function_exists('edm_get_cv_url')) {
    function edm_get_cv_url() {
        return esc_url(home_url('/downloads/Alamin-Hossain-CV.pdf'));
    }
}
