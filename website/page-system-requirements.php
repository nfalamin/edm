<?php
/**
 * Template Name: System Requirements Page
 * Description: Template for Hardware & Operating System Specifications.
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
        <?php edm_render_breadcrumbs(esc_html__('System Requirements', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Hardware & OS Compatibility', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('EDM System Requirements', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Minimum and recommended specifications for optimal 32-socket download acceleration.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- System Requirements Table Matrix -->
<?php get_template_part('template-parts/landing/system-specs'); ?>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
