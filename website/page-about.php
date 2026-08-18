<?php
/**
 * Template Name: About Page
 * Description: Template for About EDM Engineering & Vision.
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
        <?php edm_render_breadcrumbs(esc_html__('About EDM', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Engineering & Vision', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('About Exclusive Download Manager', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Building the next generation of high-speed desktop download software with modern architecture and zero compromise.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container container-narrow">
        <div class="legal-doc-card">
            <h2>The Mission</h2>
            <p>Traditional download managers were designed two decades ago and have become bloated with intrusive ads, complex unmodern UIs, and stale networking backends. EDM was engineered from scratch on modern .NET 10 and WPF to deliver pure speed, clean design, and uncompromised privacy.</p>

            <h2>Core Principles</h2>
            <ul>
                <li><strong>Maximum Speed:</strong> 32-socket multi-threading designed to maximize fiber bandwidth without crashing.</li>
                <li><strong>Zero Bloat:</strong> No third-party bundleware, no intrusive ads, and no hidden browser crypto miners.</li>
                <li><strong>Privacy First:</strong> Masked telemetry, local file operations, and zero tracking of downloaded contents.</li>
            </ul>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
