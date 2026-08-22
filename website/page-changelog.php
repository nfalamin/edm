<?php
/**
 * Template Name: Changelog Page
 * Description: Template for What's New & Version History Log.
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
        <?php edm_render_breadcrumbs(esc_html__('Release Notes', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Release Notes', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('What’s New in Exclusive Download Manager', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Full release history, kernel optimizations, and stability improvements across every published version.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container container-narrow">
        <div class="timeline-wrap">
            <!-- Release 2.1.0 -->
            <div class="timeline-entry">
                <div class="timeline-badge"><span class="badge-dot-live"></span></div>
                <div class="timeline-card">
                    <div class="timeline-header">
                        <span class="timeline-ver">v2.1.0</span>
                        <span class="timeline-date"><?php esc_html_e('Latest Stable Build', 'edm-theme'); ?></span>
                    </div>
                    <ul class="timeline-list">
                        <li><strong><?php esc_html_e('32-Socket Turbo Kernel:', 'edm-theme'); ?></strong> <?php esc_html_e('Integrated parallel chunking with adaptive socket throttling and 3-second hysteresis.', 'edm-theme'); ?></li>
                        <li><strong><?php esc_html_e('4K/8K Media Ripper:', 'edm-theme'); ?></strong> <?php esc_html_e('Automated multi-stream DASH/M3U8 download and background FFmpeg stitching.', 'edm-theme'); ?></li>
                        <li><strong><?php esc_html_e('Durable Atomic Flush:', 'edm-theme'); ?></strong> <?php esc_html_e('OS-level byte offset persistence with crash-proof file truncation on unexpected shutdown.', 'edm-theme'); ?></li>
                        <li><strong><?php esc_html_e('Manifest V3 Extension:', 'edm-theme'); ?></strong> <?php esc_html_e('Zero-latency Native Messaging bridge with automatic Chrome & Edge takeover.', 'edm-theme'); ?></li>
                    </ul>
                </div>
            </div>

            <!-- Release 2.0.9 -->
            <div class="timeline-entry">
                <div class="timeline-badge"></div>
                <div class="timeline-card">
                    <div class="timeline-header">
                        <span class="timeline-ver">v2.0.9</span>
                        <span class="timeline-date">June 2025</span>
                    </div>
                    <ul class="timeline-list">
                        <li><?php esc_html_e('Implemented Windows Defender automatic checksum validation after download completion.', 'edm-theme'); ?></li>
                        <li><?php esc_html_e('Added SOCKS5/HTTP custom proxy routing configurations.', 'edm-theme'); ?></li>
                        <li><?php esc_html_e('Improved memory pooling during 50+ concurrent batch downloads.', 'edm-theme'); ?></li>
                    </ul>
                </div>
            </div>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
