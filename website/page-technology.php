<?php
/**
 * Template Name: Technology Page
 * Description: Template for the 32x Turbo Socket Technology Whitepaper.
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
        <?php edm_render_breadcrumbs(esc_html__('32x Turbo Technology', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Architecture Deep-Dive', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('32x Turbo Multi-Socket Architecture', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Technical analysis of concurrent HTTP range-segmenting, sparse disk allocation, and durable atomic flush.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- Turbo Visualizer Section -->
<?php get_template_part('template-parts/landing/turbo-diagram'); ?>

<!-- Technical Deep Dive Cards -->
<section class="section">
    <div class="container">
        <div class="features-grid-3">
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #6366F1 0%, #4F46E5 100%);"><i data-lucide="cpu"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Sparse Disk Pre-Allocation', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Uses Windows SetEndOfFile to reserve unfragmented cluster ranges on NTFS/ReFS, eliminating disk write bottlenecks during 100+ MB/s transfers.', 'edm-theme'); ?></p>
                </div>
                <span class="feature-tech-tag">StorageManager.cs</span>
            </div>

            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #10B981 0%, #059669 100%);"><i data-lucide="shield"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Atomic State Flush', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Persists verified byte markers using OS FlushFileBuffers, ensuring zero byte loss even across abrupt power cycles or OS kernel panics.', 'edm-theme'); ?></p>
                </div>
                <span class="feature-tech-tag">DurableMetadataManager.cs</span>
            </div>

            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #38BDF8 0%, #0284C7 100%);"><i data-lucide="sliders"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Adaptive Hysteresis Throttling', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Dynamically measures RTT packet latency and reduces socket count with 3-second smoothing if the upstream server returns HTTP 429.', 'edm-theme'); ?></p>
                </div>
                <span class="feature-tech-tag">AdaptiveConnectionController.cs</span>
            </div>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
