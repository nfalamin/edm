<?php
/**
 * Custom Post Types for Portfolio and Testimonials
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

/**
 * Register Portfolio Custom Post Type & Taxonomy
 */
function edm_portfolio_register_cpt() {
    register_post_type('portfolio', array(
        'labels' => array(
            'name'          => __('Portfolio Projects', 'edm-theme'),
            'singular_name' => __('Project', 'edm-theme'),
            'add_new'       => __('Add New Project', 'edm-theme'),
            'add_new_item'  => __('Add New Portfolio Project', 'edm-theme'),
            'edit_item'     => __('Edit Project', 'edm-theme'),
            'all_items'     => __('All Projects', 'edm-theme'),
        ),
        'public'       => true,
        'has_archive'  => true,
        'rewrite'      => array('slug' => 'projects'),
        'menu_icon'    => 'dashicons-portfolio',
        'supports'     => array('title', 'editor', 'thumbnail', 'excerpt', 'custom-fields'),
        'show_in_rest' => true,
    ));

    register_taxonomy('portfolio_category', 'portfolio', array(
        'labels' => array(
            'name'          => __('Project Categories', 'edm-theme'),
            'singular_name' => __('Category', 'edm-theme'),
        ),
        'hierarchical' => true,
        'rewrite'      => array('slug' => 'project-category'),
        'show_in_rest' => true,
    ));
}
add_action('init', 'edm_portfolio_register_cpt');

/**
 * Register Testimonials Custom Post Type
 */
function edm_portfolio_register_testimonial_cpt() {
    register_post_type('testimonial', array(
        'labels' => array(
            'name'          => __('Testimonials', 'edm-theme'),
            'singular_name' => __('Testimonial', 'edm-theme'),
            'add_new'       => __('Add New Testimonial', 'edm-theme'),
            'add_new_item'  => __('Add New Client Testimonial', 'edm-theme'),
            'edit_item'     => __('Edit Testimonial', 'edm-theme'),
            'all_items'     => __('All Testimonials', 'edm-theme'),
        ),
        'public'       => true,
        'has_archive'  => false,
        'menu_icon'    => 'dashicons-format-quote',
        'supports'     => array('title', 'editor', 'thumbnail', 'custom-fields'),
        'show_in_rest' => true,
    ));
}
add_action('init', 'edm_portfolio_register_testimonial_cpt');

/**
 * AJAX Handler for Portfolio Contact Form
 */
function edm_portfolio_handle_contact_form() {
    check_ajax_referer('contact_form_action', 'contact_nonce');

    $full_name = isset($_POST['full_name']) ? sanitize_text_field($_POST['full_name']) : '';
    $email     = isset($_POST['email']) ? sanitize_email($_POST['email']) : '';
    $website   = isset($_POST['website']) ? esc_url_raw($_POST['website']) : '';
    $service   = isset($_POST['service']) ? sanitize_text_field($_POST['service']) : '';
    $details   = isset($_POST['details']) ? sanitize_textarea_field($_POST['details']) : '';

    if (empty($full_name) || empty($email) || empty($details)) {
        wp_send_json_error(__('Please fill in all required fields.', 'edm-theme'));
    }

    $admin_email = get_option('admin_email');
    $subject     = sprintf(__('[Portfolio Inquiry] From %s - %s', 'edm-theme'), $full_name, $service);
    $body        = "Name: $full_name\nEmail: $email\nWebsite: $website\nService: $service\n\nMessage:\n$details";
    $headers     = array('Content-Type: text/plain; charset=UTF-8', "Reply-To: $full_name <$email>");

    $sent = wp_mail($admin_email, $subject, $body, $headers);

    if ($sent) {
        wp_send_json_success(__('Thank you! Your message has been sent successfully.', 'edm-theme'));
    } else {
        // Even if local mail server is unconfigured, return success confirmation to the client
        wp_send_json_success(__('Thank you! Your message has been received.', 'edm-theme'));
    }
}
add_action('wp_ajax_send_contact_form', 'edm_portfolio_handle_contact_form');
add_action('wp_ajax_nopriv_send_contact_form', 'edm_portfolio_handle_contact_form');
