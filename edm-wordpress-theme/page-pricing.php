<?php
/**
 * Template Name: Pricing Page
 * Description: Template for EDM Pricing, License tiers & Payment FAQ.
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
        <?php edm_render_breadcrumbs(esc_html__('Pricing & Plans', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Honest Software Pricing', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Simple Plans for Enthusiasts & Power Users', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('All licenses include 32x socket speed, automated updates, and zero advertisements.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- Pricing Cards Matrix -->
<?php get_template_part('template-parts/landing/pricing-matrix'); ?>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
