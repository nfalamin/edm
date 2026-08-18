<?php
/**
 * EDM Theme Script and Style Enqueues
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

/**
 * Enqueue scripts and styles.
 */
function edm_theme_scripts() {
    $theme_version = wp_get_theme()->get('Version');
    $theme_uri     = get_template_directory_uri();

    // 1. Google Fonts: Plus Jakarta Sans, Inter, Space Grotesk, JetBrains Mono
    wp_enqueue_style(
        'edm-google-fonts',
        'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700;800;900&family=Space+Grotesk:wght@500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
        array(),
        null
    );

    // 2. Lucide Icons CDN
    wp_enqueue_script(
        'lucide-icons',
        'https://unpkg.com/lucide@latest',
        array(),
        null,
        true
    );

    // 3. Theme Master Stylesheet (style.css contains root tokens)
    wp_enqueue_style('edm-style', get_stylesheet_uri(), array(), $theme_version);

    // 4. Global CSS (typography, reset, buttons, forms, layout)
    wp_enqueue_style(
        'edm-global-style',
        $theme_uri . '/assets/css/global.css',
        array('edm-style'),
        $theme_version
    );

    // =========================================================================
    // ROUTE 1: DASHBOARD SPA (/dashboard)
    // =========================================================================
    if (is_page_template('page-dashboard.php') || is_page('dashboard')) {
        // Chart.js CDN for live metrics & telemetry graphs
        wp_enqueue_script(
            'chart-js',
            'https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js',
            array(),
            '4.4.1',
            true
        );

        // Dashboard Styles
        wp_enqueue_style(
            'edm-dashboard-style',
            $theme_uri . '/assets/css/dashboard.css',
            array('edm-global-style'),
            $theme_version
        );

        // Mock / Fallback Data Store
        wp_enqueue_script(
            'edm-mock-data',
            $theme_uri . '/assets/js/mock-data.js',
            array(),
            $theme_version,
            true
        );

        // Dashboard SPA Engine
        wp_enqueue_script(
            'edm-dashboard-app',
            $theme_uri . '/assets/js/dashboard-app.js',
            array('chart-js', 'lucide-icons', 'edm-mock-data'),
            $theme_version,
            true
        );

        // Localize Script for dynamic AJAX & API endpoints
        wp_localize_script('edm-dashboard-app', 'edmDashboardSettings', array(
            'ajaxUrl'     => admin_url('admin-ajax.php'),
            'nonce'       => wp_create_nonce('edm_dashboard_nonce'),
            'homeUrl'     => esc_url(home_url('/')),
            'siteTitle'   => get_bloginfo('name'),
            'apiBase'     => esc_url(home_url('/api/v1/')),
        ));

        // Responsive Stylesheet for Dashboard
        wp_enqueue_style(
            'edm-responsive-style',
            $theme_uri . '/assets/css/responsive.css',
            array('edm-dashboard-style'),
            $theme_version
        );
    } 
    // =========================================================================
    // ROUTE 2: FRONT PAGE PORTFOLIO (N F Alamin Hossain Master Portfolio)
    // =========================================================================
    elseif (is_front_page() || is_singular('portfolio')) {
        // Portfolio Modular CSS
        wp_enqueue_style('portfolio-global', $theme_uri . '/assets/css/portfolio/global-colors.css', array('edm-global-style'), $theme_version);
        wp_enqueue_style('portfolio-dark-light', $theme_uri . '/assets/css/portfolio/dark-light-mode.css', array('portfolio-global'), $theme_version);
        wp_enqueue_style('portfolio-hero', $theme_uri . '/assets/css/portfolio/hero-image.css', array('portfolio-dark-light'), $theme_version);
        wp_enqueue_style('portfolio-components', $theme_uri . '/assets/css/portfolio/components.css', array('portfolio-hero'), $theme_version);
        wp_enqueue_style('portfolio-responsive', $theme_uri . '/assets/css/portfolio/responsive.css', array('portfolio-components'), $theme_version);

        // Portfolio Engine JS
        wp_enqueue_script(
            'portfolio-main',
            $theme_uri . '/assets/js/portfolio-main.js',
            array(),
            $theme_version,
            true
        );
    }
    // =========================================================================
    // ROUTE 3: EDM PRODUCT HUB (/edm) & ALL SUBPAGES
    // =========================================================================
    else {
        // Landing & Subpages CSS
        wp_enqueue_style(
            'edm-landing-style',
            $theme_uri . '/assets/css/landing.css',
            array('edm-global-style'),
            $theme_version
        );

        // Responsive Stylesheet for Landing & Subpages
        wp_enqueue_style(
            'edm-responsive-style',
            $theme_uri . '/assets/css/responsive.css',
            array('edm-landing-style'),
            $theme_version
        );

        // Landing & Multi-page Engine JS
        wp_enqueue_script(
            'edm-landing-app',
            $theme_uri . '/assets/js/landing-app.js',
            array('lucide-icons'),
            $theme_version,
            true
        );

        // Localize Script for Dynamic Pricing & State Bus
        wp_localize_script('edm-landing-app', 'edmSiteSettings', array(
            'ajaxUrl'      => admin_url('admin-ajax.php'),
            'nonce'        => wp_create_nonce('edm_site_nonce'),
            'homeUrl'      => esc_url(home_url('/')),
            'dashboardUrl' => esc_url(home_url('/dashboard/')),
            'downloadUrl'  => esc_url($theme_uri . '/downloads/EDM-Setup-v2.1.0.exe'),
            'version'      => '2.1.0',
            'apiBase'      => esc_url(home_url('/api/v1/')),
        ));
    }
}
add_action('wp_enqueue_scripts', 'edm_theme_scripts');
