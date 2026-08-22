<?php
/**
 * Template Name: Terms of Service Page
 * Description: Template for Terms of Service & Software License Agreement.
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
        <?php edm_render_breadcrumbs(esc_html__('Terms of Service', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Legal Agreement', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Terms of Service & License Agreement', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Software licensing terms for Exclusive Download Manager community trials and commercial Pro licenses.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container container-narrow">
        <div class="legal-doc-card">
            <h2>1. License Grant</h2>
            <p>Subject to these terms, EDM grants you a non-exclusive, non-transferable license to install and run the software on the number of authorized Windows devices specified by your purchased plan tier.</p>

            <h2>2. Permitted Use</h2>
            <p>You agree to use EDM in compliance with all applicable local, national, and international laws, including copyright and intellectual property regulations.</p>

            <h2>3. Warranty Disclaimer</h2>
            <p>The software is provided "AS IS", without warranty of any kind, express or implied. EDM developers shall not be liable for any damages arising out of the use or inability to use the software.</p>

            <h2>4. Lifetime License Terms</h2>
            <p>Lifetime licenses include perpetual usage rights for all major and minor version upgrades of Exclusive Download Manager on supported Windows operating systems.</p>
        </div>
    </div>
</section>

<?php
get_footer();
