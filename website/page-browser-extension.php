<?php
/**
 * Template Name: Browser Extension Page
 * Description: Template for EDM Browser Extension integration & installation guides.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<!-- Subpage Banner -->
<section class="page-banner">
    <div class="hero-glow-bg"></div>
    <div class="container">
        <?php edm_render_breadcrumbs(esc_html__('Browser Extension', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Seamless Integration', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('EDM Zero-Click Browser Extensions', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Native Messaging bridge connecting Google Chrome, Microsoft Edge, Mozilla Firefox, Brave, and Opera to the desktop core.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- Browser Grid Section -->
<?php get_template_part('template-parts/landing/extension-showcase'); ?>

<!-- Video Grabber Feature -->
<?php get_template_part('template-parts/landing/video-grabber'); ?>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
