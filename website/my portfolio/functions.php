<?php
/**
 * Portfolio Premium - Core Functions & Customizer API
 */

if ( ! defined( 'ABSPATH' ) ) exit;

// 0. Setup Core Theme Features
function portfolio_theme_setup() {
    // Let WordPress manage the document title.
    add_theme_support( 'title-tag' );
    // Enable support for Post Thumbnails on posts and pages.
    add_theme_support( 'post-thumbnails' );
    // Add HTML5 markup support
    add_theme_support( 'html5', [ 'search-form', 'comment-form', 'comment-list', 'gallery', 'caption', 'style', 'script' ] );
}
add_action( 'after_setup_theme', 'portfolio_theme_setup' );

// 1. Register Theme Customizer Settings
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

    // --- D. Header & Footer Builder ---
    $wp_customize->add_section( 'portfolio_header_footer', [
        'title'    => __( 'Header & Footer Logos', 'portfolio' ),
        'priority' => 45,
    ] );
    $wp_customize->add_setting( 'custom_logo' );
    $wp_customize->add_control( new WP_Customize_Image_Control( $wp_customize, 'custom_logo', [
        'label'   => __( 'Upload Logo', 'portfolio' ),
        'section' => 'portfolio_header_footer',
    ] ) );

    // --- E. Background Watermark ---
    $wp_customize->add_setting( 'site_watermark', [ 'default' => '' ] );
    $wp_customize->add_control( 'site_watermark', [
        'label'       => __( 'Custom Watermark Text', 'portfolio' ),
        'description' => __( 'Displays subtle text behind content.', 'portfolio' ),
        'section'     => 'portfolio_header_footer',
        'type'        => 'text',
    ] );
}
add_action( 'customize_register', 'portfolio_theme_customizer' );


// 3. Inject Watermark into Footer
function portfolio_render_watermark() {
    $watermark = get_theme_mod( 'site_watermark', '' );
    if ( ! empty( $watermark ) ) {
        echo '<div class="site-watermark">' . esc_html( $watermark ) . '</div>';
    }
}
add_action( 'wp_footer', 'portfolio_render_watermark' );


// 4. Enqueue Gutenberg Editor Assets for custom block handling
// function portfolio_gutenberg_blocks() {
//     wp_enqueue_script(
//         'portfolio-blocks',
//         get_template_directory_uri() . '/assets/js/gutenberg-blocks.js',
//         ['wp-blocks', 'wp-element', 'wp-editor', 'wp-components']
//     );
// }
// add_action( 'enqueue_block_editor_assets', 'portfolio_gutenberg_blocks' );


// 5. Register Portfolio Custom Post Type & Taxonomy
function portfolio_register_cpt() {
    // Register Custom Post Type
    register_post_type('portfolio', [
        'labels'      => [
            'name'          => __( 'Portfolio Projects', 'portfolio' ),
            'singular_name' => __( 'Project', 'portfolio' )
        ],
        'public'      => true,
        'has_archive' => true,
        'menu_icon'   => 'dashicons-portfolio',
        'supports'    => [ 'title', 'editor', 'thumbnail', 'excerpt' ], // Enables Featured Image
    ]);

    // Register Portfolio Categories (Taxonomy) for the Tabs
    register_taxonomy('portfolio_category', 'portfolio', [
        'labels'       => [ 'name' => 'Project Categories' ],
        'hierarchical' => true, // Works like regular categories
    ]);
}
add_action('init', 'portfolio_register_cpt');

// 6. Register Testimonials Custom Post Type
function portfolio_register_testimonial_cpt() {
    register_post_type('testimonial', [
        'labels'      => [
            'name'          => __( 'Testimonials', 'portfolio' ),
            'singular_name' => __( 'Testimonial', 'portfolio' )
        ],
        'public'      => true,
        'has_archive' => false,
        'menu_icon'   => 'dashicons-format-quote',
        'supports'    => [ 'title', 'editor', 'thumbnail' ], // Title for Name, Editor for Review, Thumbnail for Image
    ]);
}
add_action('init', 'portfolio_register_testimonial_cpt');

// Developer Note: For the 'Client Role' and 'Rating' fields, using a plugin like Advanced Custom Fields (ACF) is highly recommended. 
// Create a field group for Testimonials with a text field named 'client_role' and a number field named 'rating'. 
// The code on the front page will automatically pull data from these fields.

// 7. Enqueue Separated CSS and JS files
function portfolio_enqueue_assets() {
    $theme_uri = get_template_directory_uri();
    
    // CSS
    wp_enqueue_style( 'portfolio-global', $theme_uri . '/global-colors.css', [], '1.0' );
    wp_enqueue_style( 'portfolio-dark-light', $theme_uri . '/dark-light-mode.css', [], '1.0' );
    wp_enqueue_style( 'portfolio-hero', $theme_uri . '/hero-image.css', [], '1.0' );
    wp_enqueue_style( 'portfolio-components', $theme_uri . '/components.css', [], '1.0' );
    wp_enqueue_style( 'portfolio-responsive', $theme_uri . '/responsive.css', [], '1.0' ); // Mobile Responsive File
    
    // JavaScript
    wp_enqueue_script( 'portfolio-main', $theme_uri . '/main.js', [], '1.0', true );
}
add_action( 'wp_enqueue_scripts', 'portfolio_enqueue_assets' );

// 8. Custom HTTP error templates
function portfolio_custom_error_templates_filter( $template ) {
    // is_404() is the most reliable check for 404 pages.
    if ( is_404() ) {
        $new_template = locate_template( [ '404.php', 'error.php' ] );
        if ( ! empty( $new_template ) ) {
            return $new_template;
        }
    }

    // For other HTTP status codes, WordPress doesn't have built-in conditionals like is_403().
    // These errors (401, 403, 500, 503) are often handled by the server before WordPress's
    // template loader is even reached. A fatal PHP error (500) would prevent this code from running.
    // The original implementation had calls to non-existent functions (e.g., is_403()), which would cause a fatal error.
    // By checking the HTTP response code directly, we can robustly handle errors set by WordPress or plugins.
    $status = http_response_code();

    // We check for client and server errors, but exclude 404 since it's already handled.
    if ( $status >= 400 && $status !== 404 ) {
        $custom_template = locate_template( [ "$status.php", 'error.php' ] );
        if ( ! empty( $custom_template ) ) {
            return $custom_template;
        }
    }

    return $template;
}
add_filter( 'template_include', 'portfolio_custom_error_templates_filter', 99 );

// 9. Handle Contact Form AJAX Submission
function portfolio_handle_contact_form() {
    // Security Check: Verify Nonce
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

    $to      = get_option( 'admin_email' ); // The email address configured in WP Dashboard
    $subject = 'New Portfolio Lead: ' . $service;
    $message = "Name: $name\nEmail: $email\nWebsite: $website\nService: $service\n\nProject Details:\n$details";
    $headers = [ 'Reply-To: ' . $name . ' <' . $email . '>' ];

    if ( wp_mail( $to, $subject, $message, $headers ) ) {
        wp_send_json_success( 'Message sent successfully.' );
    } else {
        wp_send_json_error( 'Message failed to send. Please check mail server settings.' );
    }
}
add_action( 'wp_ajax_send_contact_form', 'portfolio_handle_contact_form' );
add_action( 'wp_ajax_nopriv_send_contact_form', 'portfolio_handle_contact_form' );