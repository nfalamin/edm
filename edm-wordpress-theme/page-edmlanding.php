<?php
/**
 * Template Name: EDM Landing Page
 * Description: Dedicated Landing Page template for EDM (/edmlanding).
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<!-- 1. HERO SECTION & LIVE TURBO SIMULATOR -->
<?php get_template_part('template-parts/landing/hero'); ?>

<!-- 2. FEATURES OVERVIEW GRID -->
<?php get_template_part('template-parts/landing/features-overview'); ?>

<!-- 3. 32-SOCKET TURBO ARCHITECTURE DIAGRAM -->
<?php get_template_part('template-parts/landing/turbo-diagram'); ?>

<!-- 4. 4K/8K DYNAMIC VIDEO GRABBER & RIPPER -->
<?php get_template_part('template-parts/landing/video-grabber'); ?>

<!-- 5. BROWSER EXTENSION INTEGRATION (MANIFEST V3) -->
<?php get_template_part('template-parts/landing/extension-showcase'); ?>

<!-- 6. PRICING & LICENSING MATRIX -->
<?php get_template_part('template-parts/landing/pricing-matrix'); ?>

<!-- 7. SYSTEM REQUIREMENTS & SPECIFICATIONS -->
<?php get_template_part('template-parts/landing/system-specs'); ?>

<!-- 8. FINAL HIGH-CONVERSION DOWNLOAD CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
