<?php
/**
 * Template Name: Features Page
 * Description: Template for displaying all EDM features & capabilities.
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
        <?php edm_render_breadcrumbs(esc_html__('Features', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Comprehensive Engineering', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Exclusive Download Manager Capabilities', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Every feature below is implemented and verified in the EDM .NET 10 WPF desktop core engine.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- Features Grid Section -->
<?php get_template_part('template-parts/landing/features-overview'); ?>

<!-- 32x Turbo Section -->
<?php get_template_part('template-parts/landing/turbo-diagram'); ?>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
