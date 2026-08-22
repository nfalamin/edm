<?php
/**
 * Template Name: Download Page
 * Description: Template for EDM Windows Installer Download Hub & Checksums.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$version = edm_get_latest_version();
$download_url = edm_get_download_url();

get_header();
?>

<!-- Subpage Banner -->
<section class="page-banner">
    <div class="hero-glow-bg"></div>
    <div class="container">
        <?php edm_render_breadcrumbs(esc_html__('Download', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Verified Production Build', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php printf(esc_html__('Download EDM v%s for Windows', 'edm-theme'), esc_html($version)); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Native 64-bit and ARM64 installer with Microsoft Authenticode digital signature and zero telemetry bloat.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<!-- Download Hub Card -->
<section class="section">
    <div class="container">
        <div class="download-hub-card">
            <div class="hub-header">
                <div class="hub-icon-box"><i data-lucide="download" style="width: 28px; height: 28px; color: #fff;"></i></div>
                <div>
                    <h2><?php printf(esc_html__('Exclusive Download Manager v%s (Setup.exe)', 'edm-theme'), esc_html($version)); ?></h2>
                    <p class="hub-meta-text"><?php esc_html_e('Full Installer · Size: 19.8 MB · Windows 11, 10, 8.1, 7 (64-bit & ARM64)', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="hub-actions">
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-lg" download>
                    <i data-lucide="download" style="width: 18px; height: 18px;"></i>
                    <span><?php esc_html_e('Download EDM Setup.exe (19.8 MB)', 'edm-theme'); ?></span>
                </a>
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-outline btn-lg" download>
                    <i data-lucide="shield" style="width: 18px; height: 18px;"></i>
                    <span><?php esc_html_e('Direct Primary Download', 'edm-theme'); ?></span>
                </a>
            </div>

            <!-- Checksums Section -->
            <div class="hub-checksums-box" id="checksums">
                <h4><i data-lucide="lock" style="width: 15px; height: 15px; color: var(--edm-green);"></i> <?php esc_html_e('Cryptographic Checksum Verification', 'edm-theme'); ?></h4>
                <div class="checksum-row">
                    <span class="chk-label">SHA-256:</span>
                    <code class="chk-val">93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023</code>
                </div>
                <div class="checksum-row">
                    <span class="chk-label">File:</span>
                    <code class="chk-val">EDM-Setup-v2.1.0.exe (19,807,971 bytes)</code>
                </div>
            </div>
        </div>
    </div>
</section>

<!-- System Requirements Matrix -->
<?php get_template_part('template-parts/landing/system-specs'); ?>

<?php
get_footer();
