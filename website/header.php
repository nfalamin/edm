<?php
/**
 * Global Header Template
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?><!DOCTYPE html>
<html <?php language_attributes(); ?>>
<head>
    <meta charset="<?php bloginfo('charset'); ?>">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <link rel="profile" href="https://gmpg.org/xfn/11">
    
    <!-- Open Graph Metadata Fallback -->
    <meta property="og:type" content="website">
    <meta property="og:site_name" content="<?php bloginfo('name'); ?>">
    <meta name="twitter:card" content="summary_large_image">

    <?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
<?php wp_body_open(); ?>

<div id="page" class="site-wrapper">
    <a class="skip-link screen-reader-text" href="#primary">
        <?php esc_html_e('Skip to content', 'edm-theme'); ?>
    </a>

    <?php 
    // Top Announcement Bar
    get_template_part('template-parts/header/announcement-bar'); 

    // Sticky Glassmorphic Navbar
    get_template_part('template-parts/header/navigation'); 
    ?>
    <main id="primary" class="site-main">
