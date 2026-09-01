<?php
/**
 * Template Name: EDM Product Hub
 * Description: Dedicated EDM Landing Page template for /edm (e.g. yourdomain.xyz/edm).
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('edm');
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

<!-- 6. COMPREHENSIVE BENCHMARK & COMPARISON MATRIX -->
<?php get_template_part('template-parts/landing/comparison-matrix'); ?>

<!-- 7. QUICK 3-STEP INSTALLATION & GETTING STARTED GUIDE -->
<?php get_template_part('template-parts/landing/install-guide'); ?>

<!-- 8. 15-STEP COMPREHENSIVE KNOWLEDGE & SEARCH PLAYBOOK -->
<?php get_template_part('template-parts/landing/fifteen-steps-guide'); ?>

<!-- 9. PRICING & LICENSING MATRIX -->
<?php get_template_part('template-parts/landing/pricing-matrix'); ?>

<!-- 10. VERIFIED CUSTOMER REVIEWS & 10,000+ USER SOCIAL PROOF -->
<?php get_template_part('template-parts/landing/reviews-section'); ?>

<!-- 11. SYSTEM REQUIREMENTS & SPECIFICATIONS -->
<?php get_template_part('template-parts/landing/system-specs'); ?>

<!-- 12. FREQUENTLY ASKED QUESTIONS ACCORDION -->
<?php get_template_part('template-parts/landing/faq-section'); ?>

<!-- 13. FINAL HIGH-CONVERSION DOWNLOAD CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer('edm');
