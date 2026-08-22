<?php
/**
 * Template Name: Screenshots Page
 * Description: Template for UI screenshots & interactive visual gallery.
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
        <?php edm_render_breadcrumbs(esc_html__('Screenshots', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Visual Interface', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Modern Dark & Light WPF Interface Previews', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Designed with GPU-accelerated WPF rendering, clean typography, and intuitive download control queues.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container">
        <div class="screenshots-grid-2">
            <div class="screenshot-card">
                <div class="screenshot-img-wrap">
                    <img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/dashboard_preview.jpg'); ?>" alt="EDM Main Window Preview" loading="lazy" />
                </div>
                <div class="screenshot-caption">
                    <h3><?php esc_html_e('Main Download Queue & Real-time Graph', 'edm-theme'); ?></h3>
                    <p><?php esc_html_e('Comprehensive download queues with live speed graphs, progress bars, and categorization.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="screenshot-card">
                <div class="screenshot-img-wrap">
                    <img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/progress_turbo.jpg'); ?>" alt="32 Socket Turbo Preview" loading="lazy" />
                </div>
                <div class="screenshot-caption">
                    <h3><?php esc_html_e('32 Concurrent Range Slices Visualizer', 'edm-theme'); ?></h3>
                    <p><?php esc_html_e('Live progress monitors showing parallel connection chunks and adaptive throttle metrics.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="screenshot-card">
                <div class="screenshot-img-wrap">
                    <img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/browser_sniffer.jpg'); ?>" alt="Browser Sniffer Preview" loading="lazy" />
                </div>
                <div class="screenshot-caption">
                    <h3><?php esc_html_e('Zero-Click Video Sniffer Overlay', 'edm-theme'); ?></h3>
                    <p><?php esc_html_e('One-click capture dropdown on streaming portals with resolution and bitrate filters.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="screenshot-card">
                <div class="screenshot-img-wrap">
                    <img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/media_preview.jpg'); ?>" alt="Media Downloader Preview" loading="lazy" />
                </div>
                <div class="screenshot-caption">
                    <h3><?php esc_html_e('4K Media Ripper & Audio Extractor', 'edm-theme'); ?></h3>
                    <p><?php esc_html_e('Automated multi-threaded segment stitching with FFmpeg audio/video multiplexing.', 'edm-theme'); ?></p>
                </div>
            </div>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
