<?php
/**
 * Portfolio Premium - Core Functions, Customizer API & Modular EDM Integration
 *
 * @package Portfolio_Theme
 */

if ( ! defined( 'ABSPATH' ) ) exit;

// Include 3-Way Sync & Security Hub Engine
$sync_handler = get_template_directory() . '/nfdashboard-engine/sync-handler.php';
if ( file_exists( $sync_handler ) ) {
    require_once $sync_handler;
}

// ─────────────────────────────────────────────────────────────
// 0. SETUP CORE THEME FEATURES
// ─────────────────────────────────────────────────────────────
function portfolio_theme_setup() {
    // Let WordPress manage the document title.
    add_theme_support( 'title-tag' );
    // Enable support for Post Thumbnails on posts and pages.
    add_theme_support( 'post-thumbnails' );
    // Add HTML5 markup support
    add_theme_support( 'html5', [ 'search-form', 'comment-form', 'comment-list', 'gallery', 'caption', 'style', 'script' ] );
    // Enable Elementor theme support for page builder usage.
    add_theme_support( 'elementor' );
}
add_action( 'after_setup_theme', 'portfolio_theme_setup' );

function portfolio_register_elementor_widgets( $widgets_manager ) {
    if ( ! class_exists( '\Elementor\Widget_Base' ) ) {
        return;
    }

    $widget_file = get_template_directory() . '/elementor-testimonial-carousel.php';
    if ( file_exists( $widget_file ) ) {
        require_once $widget_file;
        if ( class_exists( 'Elementor_Testimonial_Carousel_Widget' ) ) {
            $widgets_manager->register( new Elementor_Testimonial_Carousel_Widget() );
        }
    }
}
add_action( 'elementor/widgets/register', 'portfolio_register_elementor_widgets' );

// ─────────────────────────────────────────────────────────────
// 1. REGISTER THEME CUSTOMIZER SETTINGS (PORTFOLIO)
// ─────────────────────────────────────────────────────────────
function portfolio_theme_customizer( $wp_customize ) {

    // --- A. Color Themes ---
    $wp_customize->add_section( 'portfolio_colors', [
        'title'    => __( 'Theme Palettes', 'portfolio' ),
        'priority' => 30,
    ] );
    $wp_customize->add_setting( 'theme_color_preset', [
        'default'           => 'premium-ash',
        'sanitize_callback' => 'sanitize_text_field',
    ] );
    $wp_customize->add_control( 'theme_color_preset', [
        'label'   => __( 'Select Global Color Theme', 'portfolio' ),
        'section' => 'portfolio_colors',
        'type'    => 'select',
        'choices' => [
            'premium-ash' => 'Premium Ash (Off-white - Soothing)',
            'light'       => 'Pure Light',
            'dark'        => 'Deep Slate / Navy',
            'blue'        => 'Oceanic Blue',
            'neon'        => 'Neon Cyberpunk',
        ],
    ] );

    // --- B. Typography Options ---
    $wp_customize->add_section( 'portfolio_typography', [
        'title'    => __( 'Typography & Fonts', 'portfolio' ),
        'priority' => 35,
    ] );
    $wp_customize->add_setting( 'global_font', [ 'default' => 'Inter' ] );
    $wp_customize->add_control( 'global_font', [
        'label'   => __( 'Primary Font Family', 'portfolio' ),
        'section' => 'portfolio_typography',
        'type'    => 'select',
        'choices' => [
            'Inter'           => 'Inter',
            'Space Grotesk'   => 'Space Grotesk',
            'DM Sans'         => 'DM Sans',
            'Poppins'         => 'Poppins',
            'Roboto'          => 'Roboto',
            'Playfair Display'=> 'Playfair Display',
            'Outfit'          => 'Outfit',
        ],
    ] );

    // --- C. Advanced Shadows & Button Filters ---
    $wp_customize->add_section( 'portfolio_fx', [
        'title'    => __( 'Shadows & UI Effects', 'portfolio' ),
        'priority' => 40,
    ] );
    $wp_customize->add_setting( 'shadow_intensity', [ 'default' => 'soft' ] );
    $wp_customize->add_control( 'shadow_intensity', [
        'label'   => __( 'Element Shadow Intensity', 'portfolio' ),
        'section' => 'portfolio_fx',
        'type'    => 'select',
        'choices' => [
            'none'    => 'Flat (No Shadows)',
            'soft'    => 'Soft & Elegant (Default)',
            'medium'  => 'Medium 3D Pop',
            'glowing' => 'Colorful Glow Drop-shadow',
        ],
    ] );

    // --- D. Hero & Profile Customizer ---
    $wp_customize->add_section( 'portfolio_hero_settings', [
        'title'    => __( 'Hero Section & Profile Photo', 'portfolio' ),
        'priority' => 42,
    ] );
    $wp_customize->add_setting( 'hero_profile_photo' );
    $wp_customize->add_control( new WP_Customize_Image_Control( $wp_customize, 'hero_profile_photo', [
        'label'       => __( 'Upload Profile Photo / Future Photo', 'portfolio' ),
        'description' => __( 'Upload your portrait photo (PNG recommended with transparent background).', 'portfolio' ),
        'section'     => 'portfolio_hero_settings',
    ] ) );

    $wp_customize->add_setting( 'hero_name', [ 'default' => 'Alamin Hossain', 'sanitize_callback' => 'sanitize_text_field' ] );
    $wp_customize->add_control( 'hero_name', [
        'label'   => __( 'Hero Name', 'portfolio' ),
        'section' => 'portfolio_hero_settings',
        'type'    => 'text',
    ] );

    $wp_customize->add_setting( 'hero_tagline', [ 'default' => 'Growth Expert', 'sanitize_callback' => 'sanitize_text_field' ] );
    $wp_customize->add_control( 'hero_tagline', [
        'label'   => __( 'Hero Tagline / Specialty', 'portfolio' ),
        'section' => 'portfolio_hero_settings',
        'type'    => 'text',
    ] );

    $wp_customize->add_setting( 'hero_badge_text', [ 'default' => 'Available for Projects', 'sanitize_callback' => 'sanitize_text_field' ] );
    $wp_customize->add_control( 'hero_badge_text', [
        'label'   => __( 'Hero Availability Badge Text', 'portfolio' ),
        'section' => 'portfolio_hero_settings',
        'type'    => 'text',
    ] );

    $wp_customize->add_setting( 'hero_bio', [
        'default'           => 'I build transparent, highly-optimized campaigns designed to maximize traffic, generate qualified leads, and grow revenue as a certified SEO Specialist, Google Ads Expert, and Social Media Marketing Strategist.',
        'sanitize_callback' => 'sanitize_textarea_field'
    ] );
    $wp_customize->add_control( 'hero_bio', [
        'label'   => __( 'Hero Biography / Summary', 'portfolio' ),
        'section' => 'portfolio_hero_settings',
        'type'    => 'textarea',
    ] );

    // --- E. EDM Software Hub Customizer ---
    $wp_customize->add_section( 'edm_hub_settings', [
        'title'    => __( 'EDM Software Hub Settings', 'portfolio' ),
        'priority' => 44,
    ] );
    $wp_customize->add_setting( 'edm_logo' );
    $wp_customize->add_control( new WP_Customize_Image_Control( $wp_customize, 'edm_logo', [
        'label'       => __( 'Upload EDM 3D Logo', 'portfolio' ),
        'section'     => 'edm_hub_settings',
    ] ) );

    $wp_customize->add_setting( 'edm_show_announcement', [ 'default' => true, 'sanitize_callback' => 'wp_validate_boolean' ] );
    $wp_customize->add_control( 'edm_show_announcement', [
        'label'   => __( 'Show Top Announcement Bar', 'portfolio' ),
        'section' => 'edm_hub_settings',
        'type'    => 'checkbox',
    ] );

    $wp_customize->add_setting( 'edm_announcement_text', [ 'default' => '', 'sanitize_callback' => 'sanitize_text_field' ] );
    $wp_customize->add_control( 'edm_announcement_text', [
        'label'       => __( 'Custom Announcement Text', 'portfolio' ),
        'description' => __( 'Leave empty for default release announcement.', 'portfolio' ),
        'section'     => 'edm_hub_settings',
        'type'        => 'text',
    ] );

    // --- F. Header & Footer Builder ---
    $wp_customize->add_section( 'portfolio_header_footer', [
        'title'    => __( 'Header & Footer Logos', 'portfolio' ),
        'priority' => 45,
    ] );
    $wp_customize->add_setting( 'custom_logo' );
    $wp_customize->add_control( new WP_Customize_Image_Control( $wp_customize, 'custom_logo', [
        'label'   => __( 'Upload Logo', 'portfolio' ),
        'section' => 'portfolio_header_footer',
    ] ) );

    // --- G. Background Watermark ---
    $wp_customize->add_setting( 'site_watermark', [ 'default' => '' ] );
    $wp_customize->add_control( 'site_watermark', [
        'label'       => __( 'Custom Watermark Text', 'portfolio' ),
        'description' => __( 'Displays subtle text behind content.', 'portfolio' ),
        'section'     => 'portfolio_header_footer',
        'type'        => 'text',
    ] );

    // --- H. Contact & Socials ---
    $wp_customize->add_section( 'portfolio_contact_settings', [
        'title'    => __( 'Contact & Social Info', 'portfolio' ),
        'priority' => 46,
    ] );
    $wp_customize->add_setting( 'contact_phone', [ 'default' => '01888567189', 'sanitize_callback' => 'sanitize_text_field' ] );
    $wp_customize->add_control( 'contact_phone', [
        'label'   => __( 'Phone Number', 'portfolio' ),
        'section' => 'portfolio_contact_settings',
        'type'    => 'text',
    ] );
    $wp_customize->add_setting( 'contact_email', [ 'default' => 'nfxalamin@gmail.com', 'sanitize_callback' => 'sanitize_email' ] );
    $wp_customize->add_control( 'contact_email', [
        'label'   => __( 'Email Address', 'portfolio' ),
        'section' => 'portfolio_contact_settings',
        'type'    => 'email',
    ] );
}
add_action( 'customize_register', 'portfolio_theme_customizer' );

// ─────────────────────────────────────────────────────────────
// 2. INJECT WATERMARK INTO FOOTER
// ─────────────────────────────────────────────────────────────
function portfolio_render_watermark() {
    if ( portfolio_is_edm_dashboard_route() || portfolio_is_edm_public_route() ) {
        return; // Exclude watermark on EDM and Dashboard pages
    }
    $watermark = get_theme_mod( 'site_watermark', '' );
    if ( ! empty( $watermark ) ) {
        echo '<div class="site-watermark">' . esc_html( $watermark ) . '</div>';
    }
}
add_action( 'wp_footer', 'portfolio_render_watermark' );

// ─────────────────────────────────────────────────────────────
// 3. REGISTER PORTFOLIO CUSTOM POST TYPE & TAXONOMY
// ─────────────────────────────────────────────────────────────
function portfolio_register_cpt() {
    register_post_type('portfolio', [
        'labels'      => [
            'name'          => __( 'Portfolio Projects', 'portfolio' ),
            'singular_name' => __( 'Project', 'portfolio' )
        ],
        'public'      => true,
        'has_archive' => true,
        'menu_icon'   => 'dashicons-portfolio',
        'supports'    => [ 'title', 'editor', 'thumbnail', 'excerpt' ],
    ]);

    register_taxonomy('portfolio_category', 'portfolio', [
        'labels'       => [ 'name' => 'Project Categories' ],
        'hierarchical' => true,
    ]);
}
add_action('init', 'portfolio_register_cpt');

// ─────────────────────────────────────────────────────────────
// 4. REGISTER TESTIMONIALS CUSTOM POST TYPE
// ─────────────────────────────────────────────────────────────
function portfolio_register_testimonial_cpt() {
    register_post_type('testimonial', [
        'labels'      => [
            'name'          => __( 'Testimonials', 'portfolio' ),
            'singular_name' => __( 'Testimonial', 'portfolio' )
        ],
        'public'      => true,
        'has_archive' => false,
        'menu_icon'   => 'dashicons-format-quote',
        'supports'    => [ 'title', 'editor', 'thumbnail' ],
    ]);
}
add_action('init', 'portfolio_register_testimonial_cpt');

// ─────────────────────────────────────────────────────────────
// 5. HELPER FUNCTIONS & ROUTE DETECTORS
// ─────────────────────────────────────────────────────────────
if ( ! function_exists( 'portfolio_get_profile_image' ) ) {
    function portfolio_get_profile_image() {
        $custom_img = get_theme_mod('hero_profile_photo');
        if (!empty($custom_img)) {
            return esc_url($custom_img);
        }
        $themeDir = get_template_directory();
        $candidates = [
            'nf.png',
            'Assets/images/nf.png',
            'assets/images/nf.png',
            'Assets/images/profile.png',
            'assets/images/profile.png'
        ];
        foreach ($candidates as $cand) {
            if (file_exists($themeDir . '/' . $cand)) {
                return esc_url(get_template_directory_uri() . '/' . $cand);
            }
        }
        return esc_url(get_template_directory_uri() . '/nf.png');
    }
}

if ( ! function_exists( 'portfolio_get_hero_portrait_image' ) ) {
    function portfolio_get_hero_portrait_image() {
        return portfolio_get_profile_image();
    }
}

function edm_get_latest_version() {
    if (class_exists('EdmManifestManager')) {
        $manifest = EdmManifestManager::getLiveManifest();
        if (!empty($manifest['current_version'])) {
            return $manifest['current_version'];
        }
        if (!empty($manifest['version'])) {
            return $manifest['version'];
        }
    }
    return apply_filters('edm_latest_version', '2.1.0');
}

function edm_get_download_url() {
    $custom_url = get_theme_mod('edm_download_url', '');
    if (!empty($custom_url)) {
        return esc_url($custom_url);
    }

    // Dynamic File Explorer Scanner: find newest .exe in downloads/
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $exeFiles = glob($downloadsDir . '/*.exe');
        if (!empty($exeFiles)) {
            // Sort by modified time descending to get the newest file
            usort($exeFiles, function($a, $b) {
                return filemtime($b) - filemtime($a);
            });
            $newest = basename($exeFiles[0]);
            return esc_url(get_template_directory_uri() . '/downloads/' . $newest);
        }
    }

    if (class_exists('EdmManifestManager')) {
        $manifest = EdmManifestManager::getLiveManifest();
        if (!empty($manifest['files']['installer']['relative_url'])) {
            return esc_url(get_template_directory_uri() . '/' . ltrim($manifest['files']['installer']['relative_url'], '/'));
        }
        if (!empty($manifest['artifacts']['installer']['relativePath'])) {
            return esc_url(get_template_directory_uri() . '/' . ltrim($manifest['artifacts']['installer']['relativePath'], '/'));
        }
    }
    return esc_url(get_template_directory_uri() . '/downloads/EDM-Setup-v2.1.0.exe');
}

function edm_get_extension_url($browser = 'chrome') {
    $browser = strtolower($browser);
    $downloadsDir = get_template_directory() . '/downloads';
    
    // Dynamic File Explorer Scanner for browser extension archives
    if (is_dir($downloadsDir)) {
        $matches = glob($downloadsDir . '/*' . $browser . '*.zip');
        if (!empty($matches)) {
            return esc_url(get_template_directory_uri() . '/downloads/' . basename($matches[0]));
        }
    }

    if (class_exists('EdmManifestManager')) {
        $manifest = EdmManifestManager::getLiveManifest();
        $key = ($browser === 'firefox') ? 'firefox_extension' : (($browser === 'edge') ? 'edge_extension' : 'chrome_extension');
        if (!empty($manifest['files'][$key]['relative_url'])) {
            return esc_url(get_template_directory_uri() . '/' . ltrim($manifest['files'][$key]['relative_url'], '/'));
        }
    }
    if ($browser === 'firefox') {
        return esc_url(get_template_directory_uri() . '/downloads/edm-firefox-extension-v1.0.0.zip');
    } elseif ($browser === 'edge') {
        return esc_url(get_template_directory_uri() . '/downloads/edm-edge-extension-v1.0.0.zip');
    }
    return esc_url(get_template_directory_uri() . '/downloads/edm-chrome-extension-v1.0.0.zip');
}

function edm_get_cv_url() {
    $custom_cv = get_theme_mod('portfolio_cv_url', '');
    if (!empty($custom_cv)) {
        return esc_url($custom_cv);
    }
    
    // Dynamic File Explorer Scanner for CV or Resume PDF
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $pdfFiles = glob($downloadsDir . '/*.pdf');
        if (!empty($pdfFiles)) {
            return esc_url(get_template_directory_uri() . '/downloads/' . basename($pdfFiles[0]));
        }
    }
    return esc_url(get_template_directory_uri() . '/downloads/Alamin-Hossain-CV.pdf');
}

function edm_get_download_file_size() {
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $exeFiles = glob($downloadsDir . '/*.exe');
        if (!empty($exeFiles)) {
            usort($exeFiles, function($a, $b) {
                return filemtime($b) - filemtime($a);
            });
            $size = filesize($exeFiles[0]);
            if ($size > 0) {
                return size_format($size, 1);
            }
        }
    }
    if (class_exists('EdmManifestManager')) {
        $manifest = EdmManifestManager::getLiveManifest();
        if (!empty($manifest['files']['installer']['size_human'])) {
            return $manifest['files']['installer']['size_human'];
        }
    }
    return '19.8 MB';
}

function edm_get_download_filename() {
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $exeFiles = glob($downloadsDir . '/*.exe');
        if (!empty($exeFiles)) {
            usort($exeFiles, function($a, $b) {
                return filemtime($b) - filemtime($a);
            });
            return basename($exeFiles[0]);
        }
    }
    return 'EDM-Setup-v2.1.0.exe';
}

function edm_get_download_sha256() {
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $exeFiles = glob($downloadsDir . '/*.exe');
        if (!empty($exeFiles)) {
            usort($exeFiles, function($a, $b) {
                return filemtime($b) - filemtime($a);
            });
            return hash_file('sha256', $exeFiles[0]);
        }
    }
    if (class_exists('EdmManifestManager')) {
        $manifest = EdmManifestManager::getLiveManifest();
        if (!empty($manifest['sha256_hash'])) {
            return $manifest['sha256_hash'];
        }
    }
    return '93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023';
}

function edm_get_extension_file_size($browser = 'chrome') {
    $browser = strtolower($browser);
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $matches = glob($downloadsDir . '/*' . $browser . '*.zip');
        if (!empty($matches)) {
            $size = filesize($matches[0]);
            if ($size > 0) {
                return size_format($size, 1);
            }
        }
    }
    return '87.5 KB';
}

function edm_get_cv_file_size() {
    $downloadsDir = get_template_directory() . '/downloads';
    if (is_dir($downloadsDir)) {
        $pdfFiles = glob($downloadsDir . '/*.pdf');
        if (!empty($pdfFiles)) {
            $size = filesize($pdfFiles[0]);
            if ($size > 0) {
                return size_format($size, 1);
            }
        }
    }
    return '1.2 MB';
}

if ( ! function_exists( 'portfolio_get_about_chair_image' ) ) {
    function portfolio_get_about_chair_image() {
        $themeDir = get_template_directory();
        $candidates = [
            'Assets/images/nf011.png',
            'assets/images/nf011.png',
            'nf011.png',
            'Assets/images/chair.png',
            'assets/images/chair.png'
        ];
        foreach ($candidates as $cand) {
            if (file_exists($themeDir . '/' . $cand)) {
                return esc_url(get_template_directory_uri() . '/' . $cand);
            }
        }
        return esc_url(get_template_directory_uri() . '/Assets/images/nf011.png');
    }
}

function edm_get_contact_email() {
    return 'nfxalamin@gmail.com';
}

function edm_get_contact_phone() {
    return '01888567189';
}

function edm_get_whatsapp_url() {
    return 'https://wa.me/8801888567189';
}

function edm_format_price($bdt_amount, $currency = 'BDT') {
    if (strtoupper($currency) === 'USD') {
        $usd = round($bdt_amount / 120, 2);
        return '$' . number_format($usd, 2);
    }
    return '৳' . number_format($bdt_amount, 0);
}

function edm_clean_string($input) {
    if (is_array($input)) {
        return array_map('edm_clean_string', $input);
    }
    return sanitize_text_field(wp_unslash($input));
}

function edm_page_url($slug) {
    $page = get_page_by_path($slug);
    if ($page) {
        return esc_url(get_permalink($page->ID));
    }
    return esc_url(home_url('/' . $slug . '/'));
}

/**
 * Bulletproof Dashboard Page Detector
 */
function portfolio_is_edm_dashboard_route() {
    if ( is_page_template( 'page-nfdashbord.php' ) || is_page_template( 'page-dashboard.php' ) ) {
        return true;
    }
    if ( is_page( 'nfdashbord' ) || is_page( 'dashboard' ) || is_page( 'dashbord' ) || is_page( 'control-plane' ) ) {
        return true;
    }
    global $post;
    if ( $post && isset($post->ID) ) {
        $template = get_post_meta( $post->ID, '_wp_page_template', true );
        if ( in_array( $template, [ 'page-nfdashbord.php', 'page-dashboard.php' ], true ) ) {
            return true;
        }
        if ( in_array( $post->post_name, [ 'nfdashbord', 'dashboard', 'dashbord', 'control-plane' ], true ) ) {
            return true;
        }
    }
    if ( isset($_SERVER['REQUEST_URI']) ) {
        $uri = $_SERVER['REQUEST_URI'];
        if ( strpos($uri, '/nfdashbord') !== false || strpos($uri, '/dashboard') !== false || strpos($uri, '/dashbord') !== false ) {
            return true;
        }
    }
    return false;
}

/**
 * Bulletproof EDM Public Landing Page Detector
 */
function portfolio_is_edm_public_route() {
    if ( is_page_template( 'page-edm.php' ) || is_page_template( 'page-edmlanding.php' ) || is_page_template( 'page-download.php' ) ) {
        return true;
    }
    if ( is_page( 'edm' ) || is_page( 'download' ) || is_page( 'downloads' ) ) {
        return true;
    }
    global $post;
    if ( $post && isset($post->ID) ) {
        $template = get_post_meta( $post->ID, '_wp_page_template', true );
        if ( in_array( $template, [ 'page-edm.php', 'page-edmlanding.php', 'page-download.php' ], true ) ) {
            return true;
        }
        if ( in_array( $post->post_name, [ 'edm', 'download', 'downloads' ], true ) ) {
            return true;
        }
    }
    if ( isset($_SERVER['REQUEST_URI']) ) {
        $uri = $_SERVER['REQUEST_URI'];
        if ( strpos($uri, '/edm') !== false ) {
            return true;
        }
    }
    return false;
}

// ─────────────────────────────────────────────────────────────
// 6. ISOLATED & MODULAR ENQUEUE ROUTING
// ─────────────────────────────────────────────────────────────
function portfolio_enqueue_assets() {
    $theme_uri     = get_template_directory_uri();
    $theme_version = '2.1.0';

    // ─────────────────────────────────────────────────────────
    // ROUTE A: NF SECRET DASHBOARD (/nfdashbord or /dashboard)
    // ─────────────────────────────────────────────────────────
    if ( portfolio_is_edm_dashboard_route() ) {
        // Google Fonts
        wp_enqueue_style(
            'edm-google-fonts',
            'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700;800;900&family=Space+Grotesk:wght@500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
            [],
            null
        );

        // Lucide Icons
        wp_enqueue_script( 'lucide-icons', 'https://unpkg.com/lucide@latest', [], null, true );

        // Chart.js CDN
        wp_enqueue_script( 'chart-js', 'https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js', [], '4.4.1', true );

        // Dashboard CSS
        wp_enqueue_style( 'edm-global-style', $theme_uri . '/assets/css/global.css', [], $theme_version );
        wp_enqueue_style( 'edm-dashboard-style', $theme_uri . '/assets/css/dashboard-app.css', [ 'edm-global-style' ], $theme_version );
        wp_enqueue_style( 'edm-responsive-style', $theme_uri . '/assets/css/responsive.css', [ 'edm-dashboard-style' ], $theme_version );

        // Mock / Telemetry Data Store
        wp_enqueue_script( 'edm-mock-data', $theme_uri . '/assets/js/mock-data.js', [], $theme_version, true );

        // Dashboard SPA Engine
        wp_enqueue_script( 'edm-dashboard-app', $theme_uri . '/assets/js/dashboard-app.js', [ 'chart-js', 'lucide-icons', 'edm-mock-data' ], $theme_version, true );

        wp_localize_script( 'edm-dashboard-app', 'edmDashboardSettings', [
            'ajaxUrl'   => admin_url( 'admin-ajax.php' ),
            'nonce'     => wp_create_nonce( 'nfdash_auth_nonce' ),
            'homeUrl'   => esc_url( home_url( '/' ) ),
            'siteTitle' => get_bloginfo( 'name' ),
            'apiBase'   => esc_url( home_url( '/wp-json/edm-api/v1/' ) ),
        ] );
    }

    // ─────────────────────────────────────────────────────────
    // ROUTE B: EDM PRODUCT HUB (/edm)
    // ─────────────────────────────────────────────────────────
    elseif ( portfolio_is_edm_public_route() ) {
        // Google Fonts
        wp_enqueue_style(
            'edm-google-fonts',
            'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700;800;900&family=Space+Grotesk:wght@500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
            [],
            null
        );

        // Lucide Icons
        wp_enqueue_script( 'lucide-icons', 'https://unpkg.com/lucide@latest', [], null, true );

        // EDM Landing CSS
        wp_enqueue_style( 'edm-global-style', $theme_uri . '/assets/css/global.css', [], $theme_version );
        wp_enqueue_style( 'edm-landing-style', $theme_uri . '/assets/css/landing.css', [ 'edm-global-style' ], $theme_version );
        wp_enqueue_style( 'edm-responsive-style', $theme_uri . '/assets/css/responsive.css', [ 'edm-landing-style' ], $theme_version );

        // EDM Landing App JS
        wp_enqueue_script( 'edm-landing-app', $theme_uri . '/assets/js/landing-app.js', [ 'lucide-icons' ], $theme_version, true );

        wp_localize_script( 'edm-landing-app', 'edmSiteSettings', [
            'ajaxUrl'      => admin_url( 'admin-ajax.php' ),
            'nonce'        => wp_create_nonce( 'edm_site_nonce' ),
            'homeUrl'      => esc_url( home_url( '/' ) ),
            'dashboardUrl' => esc_url( home_url( '/nfdashbord/' ) ),
            'downloadUrl'  => edm_get_download_url(),
            'version'      => edm_get_latest_version(),
            'apiBase'      => esc_url( home_url( '/wp-json/edm-api/v1/' ) ),
        ] );
    }

    // ─────────────────────────────────────────────────────────
    // ROUTE C: MAIN PORTFOLIO (Home, About, Services, Single)
    // ─────────────────────────────────────────────────────────
    else {
        // Portfolio Master Style
        wp_enqueue_style( 'alamin-portfolio-main-style', get_stylesheet_uri(), [], $theme_version );
        wp_enqueue_style( 'portfolio-global', $theme_uri . '/global-colors.css', [], $theme_version );
        wp_enqueue_style( 'portfolio-dark-light', $theme_uri . '/dark-light-mode.css', [], $theme_version );
        wp_enqueue_style( 'portfolio-hero', $theme_uri . '/hero-image.css', [], $theme_version );
        wp_enqueue_style( 'portfolio-components', $theme_uri . '/components.css', [], $theme_version );
        wp_enqueue_style( 'portfolio-responsive', $theme_uri . '/responsive.css', [], $theme_version );

        // Portfolio Engine JS
        wp_enqueue_script( 'portfolio-main', $theme_uri . '/main.js', [], $theme_version, true );
    }
}
add_action( 'wp_enqueue_scripts', 'portfolio_enqueue_assets' );

// ─────────────────────────────────────────────────────────────
// 7. AUTOMATIC VIRTUAL ROUTE INTERCEPTOR (ZERO 404 ERRORS)
// ─────────────────────────────────────────────────────────────
function portfolio_virtual_route_interceptor( $template ) {
    global $wp_query;

    $request_uri = $_SERVER['REQUEST_URI'] ?? '';
    $path = trim( parse_url( $request_uri, PHP_URL_PATH ), '/' );

    // Strip WordPress subfolder if installed in subdirectory
    $home_path = trim( parse_url( home_url(), PHP_URL_PATH ), '/' );
    if ( ! empty( $home_path ) && strpos( $path, $home_path ) === 0 ) {
        $path = trim( substr( $path, strlen( $home_path ) ), '/' );
    }

    // 1. Secret Dashboard Routes (/nf, /nfdashbord, /nfdashboard, /dashboard, /dashbord, /nf-dashboard, /control-plane)
    if ( in_array( $path, [ 'nf', 'nfdashbord', 'nfdashboard', 'dashboard', 'dashbord', 'nf-dashboard', 'control-plane' ], true ) ) {
        $dash_template = locate_template( [ 'page-nfdashbord.php', 'page-dashboard.php' ] );
        if ( ! empty( $dash_template ) ) {
            if ( $wp_query ) {
                $wp_query->is_404  = false;
                $wp_query->is_page = true;
            }
            status_header( 200 );
            return $dash_template;
        }
    }

    // 2. EDM Sub-pages Routes (/edm-extensions, /edm-download, /edm-features)
    if ( in_array( $path, [ 'edm-extensions', 'edm/extensions', 'extensions' ], true ) ) {
        $ext_template = locate_template( [ 'page-edm-extensions.php' ] );
        if ( ! empty( $ext_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $ext_template;
        }
    }
    if ( in_array( $path, [ 'edm-features', 'edm/features', 'features' ], true ) ) {
        $feat_template = locate_template( [ 'page-edm-features.php' ] );
        if ( ! empty( $feat_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $feat_template;
        }
    }
    if ( in_array( $path, [ 'edm-download', 'edm/download', 'download', 'downloads' ], true ) ) {
        $dl_template = locate_template( [ 'page-edm-download.php' ] );
        if ( ! empty( $dl_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $dl_template;
        }
    }
    if ( in_array( $path, [ 'edm', 'edm-hub' ], true ) ) {
        $edm_template = locate_template( [ 'page-edm.php' ] );
        if ( ! empty( $edm_template ) ) {
            if ( $wp_query ) {
                $wp_query->is_404  = false;
                $wp_query->is_page = true;
            }
            status_header( 200 );
            return $edm_template;
        }
    }

    // 3. Multi-page Portfolio Routes (/about, /services, /portfolio, /contact)
    if ( $path === 'about' || $path === 'about-me' ) {
        $about_template = locate_template( [ 'page-about.php' ] );
        if ( ! empty( $about_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $about_template;
        }
    }
    if ( $path === 'services' ) {
        $services_template = locate_template( [ 'page-services.php' ] );
        if ( ! empty( $services_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $services_template;
        }
    }
    if ( $path === 'portfolio' || $path === 'projects' ) {
        $portfolio_template = locate_template( [ 'page-portfolio.php' ] );
        if ( ! empty( $portfolio_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $portfolio_template;
        }
    }
    if ( $path === 'privacy' || $path === 'privacy-policy' ) {
        $privacy_template = locate_template( [ 'page-privacy.php', 'privacy-policy.php' ] );
        if ( ! empty( $privacy_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $privacy_template;
        }
    }
    if ( $path === 'terms' || $path === 'terms-of-service' || $path === 'eula' ) {
        $terms_template = locate_template( [ 'page-terms.php', 'terms.php' ] );
        if ( ! empty( $terms_template ) ) {
            if ( $wp_query ) { $wp_query->is_404 = false; $wp_query->is_page = true; }
            status_header( 200 );
            return $terms_template;
        }
    }

    return $template;
}
add_filter( 'template_include', 'portfolio_virtual_route_interceptor', 1 );

// ─────────────────────────────────────────────────────────────
// 8. CUSTOM HTTP ERROR TEMPLATES
// ─────────────────────────────────────────────────────────────
function portfolio_custom_error_templates_filter( $template ) {
    if ( is_404() ) {
        $new_template = locate_template( [ '404.php', 'error.php' ] );
        if ( ! empty( $new_template ) ) {
            return $new_template;
        }
    }

    $status = http_response_code();
    if ( $status >= 400 && $status !== 404 ) {
        $custom_template = locate_template( [ "$status.php", 'error.php' ] );
        if ( ! empty( $custom_template ) ) {
            return $custom_template;
        }
    }

    return $template;
}
add_filter( 'template_include', 'portfolio_custom_error_templates_filter', 99 );

// ─────────────────────────────────────────────────────────────
// 8. CONTACT FORM AJAX HANDLER (PORTFOLIO)
// ─────────────────────────────────────────────────────────────
function portfolio_handle_contact_form() {
    if ( ! isset( $_POST['contact_nonce'] ) || ! wp_verify_nonce( $_POST['contact_nonce'], 'contact_form_action' ) ) {
        wp_send_json_error( 'Security validation failed. Please refresh the page and try again.' );
    }

    $name    = sanitize_text_field( $_POST['full_name'] ?? '' );
    $email   = sanitize_email( $_POST['email'] ?? '' );
    $website = sanitize_text_field( $_POST['website'] ?? '' );
    $service = sanitize_text_field( $_POST['service'] ?? '' );
    $details = sanitize_textarea_field( $_POST['details'] ?? '' );

    if ( empty( $name ) || empty( $email ) || empty( $details ) ) {
        wp_send_json_error( 'Please fill in all required fields.' );
    }

    $to      = 'nfxalamin@gmail.com';
    $subject = 'New Portfolio Lead: ' . $service;
    $message = "Name: $name\nEmail: $email\nWebsite: $website\nService: $service\n\nProject Details:\n$details";
    $headers = [ 'Reply-To: ' . $name . ' <' . $email . '>' ];

    if ( wp_mail( $to, $subject, $message, $headers ) ) {
        wp_send_json_success( 'Message sent successfully. Alamin will get in touch shortly!' );
    } else {
        wp_send_json_error( 'Message failed to send. Please contact directly at nfxalamin@gmail.com or 01888567189.' );
    }
}
add_action( 'wp_ajax_send_contact_form', 'portfolio_handle_contact_form' );
add_action( 'wp_ajax_nopriv_send_contact_form', 'portfolio_handle_contact_form' );

// ─────────────────────────────────────────────────────────────
// 9. AUTOMATIC FAVICON INJECTOR
// ─────────────────────────────────────────────────────────────
function portfolio_inject_favicon() {
    $logo_url = get_template_directory_uri() . '/edm-logo.png';
    echo '<link rel="icon" type="image/png" sizes="128x128" href="' . esc_url( $logo_url ) . '">' . "\n";
    echo '<link rel="apple-touch-icon" sizes="180x180" href="' . esc_url( $logo_url ) . '">' . "\n";
}
add_action( 'wp_head', 'portfolio_inject_favicon', 2 );
add_action( 'admin_head', 'portfolio_inject_favicon', 2 );

// ─────────────────────────────────────────────────────────────
// 10. ROBOTS.TXT PROTECTION & SITEMAP DECLARATION
// ─────────────────────────────────────────────────────────────
function portfolio_custom_robots_txt( $output, $public ) {
    $site_url = home_url();
    $custom = "User-agent: *\n";
    $custom .= "Disallow: /wp-admin/\n";
    $custom .= "Disallow: /nf/\n";
    $custom .= "Disallow: /nfdashbord/\n";
    $custom .= "Disallow: /dashboard/\n";
    $custom .= "Disallow: /controlplane/\n";
    $custom .= "Allow: /wp-admin/admin-ajax.php\n";
    $custom .= "Allow: /edm/\n";
    $custom .= "Allow: /edm-extensions/\n";
    $custom .= "Allow: /edm-download/\n";
    $custom .= "Allow: /edm-features/\n";
    $custom .= "Sitemap: " . esc_url( $site_url . '/sitemap.xml' ) . "\n";
    return $custom;
}
add_filter( 'robots_txt', 'portfolio_custom_robots_txt', 10, 2 );

// ─────────────────────────────────────────────────────────────
// 11. DYNAMIC XML SITEMAP GENERATOR ROUTE (/sitemap.xml)
// ─────────────────────────────────────────────────────────────
function portfolio_render_xml_sitemap() {
    $req_uri = isset( $_SERVER['REQUEST_URI'] ) ? sanitize_text_field( wp_unslash( $_SERVER['REQUEST_URI'] ) ) : '';
    $path = trim( parse_url( $req_uri, PHP_URL_PATH ), '/' );

    if ( $path === 'sitemap.xml' || $path === 'sitemap' ) {
        header( 'Content-Type: application/xml; charset=utf-8' );
        header( 'X-Robots-Tag: noindex, follow', true );
        
        $base = rtrim( home_url(), '/' );
        $routes = [
            [ 'loc' => $base . '/', 'priority' => '1.0', 'changefreq' => 'weekly' ],
            [ 'loc' => $base . '/edm/', 'priority' => '1.0', 'changefreq' => 'daily' ],
            [ 'loc' => $base . '/edm-extensions/', 'priority' => '0.9', 'changefreq' => 'weekly' ],
            [ 'loc' => $base . '/edm-download/', 'priority' => '0.9', 'changefreq' => 'daily' ],
            [ 'loc' => $base . '/edm-features/', 'priority' => '0.8', 'changefreq' => 'monthly' ],
            [ 'loc' => $base . '/about/', 'priority' => '0.8', 'changefreq' => 'monthly' ],
            [ 'loc' => $base . '/services/', 'priority' => '0.8', 'changefreq' => 'monthly' ],
            [ 'loc' => $base . '/portfolio/', 'priority' => '0.8', 'changefreq' => 'weekly' ],
            [ 'loc' => $base . '/contact/', 'priority' => '0.8', 'changefreq' => 'monthly' ],
        ];

        echo '<?xml version="1.0" encoding="UTF-8"?>' . "\n";
        echo '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' . "\n";
        foreach ( $routes as $r ) {
            echo '  <url>' . "\n";
            echo '    <loc>' . esc_url( $r['loc'] ) . '</loc>' . "\n";
            echo '    <lastmod>' . esc_html( gmdate( 'Y-m-d' ) ) . '</lastmod>' . "\n";
            echo '    <changefreq>' . esc_html( $r['changefreq'] ) . '</changefreq>' . "\n";
            echo '    <priority>' . esc_html( $r['priority'] ) . '</priority>' . "\n";
            echo '  </url>' . "\n";
        }
        echo '</urlset>' . "\n";
        exit;
    }
}
add_action( 'init', 'portfolio_render_xml_sitemap', 1 );