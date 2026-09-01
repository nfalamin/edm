<?php
/**
 * Landing Page: Final Download Call-To-Action Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$version = edm_get_latest_version();
$download_url = edm_get_download_url();
?>
<section class="section cta-section" id="download-cta">
    <div class="container">
        <div class="cta-banner-card">
            <div class="cta-glow-bg"></div>
            <div class="cta-content">
                <span class="section-badge"><?php esc_html_e('Instant 32x Acceleration', 'edm-theme'); ?></span>
                <h2 class="cta-title"><?php esc_html_e('Experience the Fastest Download Speeds on Windows Today', 'edm-theme'); ?></h2>
                <p class="cta-subtitle">
                    <?php printf(esc_html__('Download EDM v%s for Windows 11 & 10. Clean installer with Authenticode signature and 14-day full Turbo trial.', 'edm-theme'), esc_html($version)); ?>
                </p>

                <div class="cta-buttons-row">
                    <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-lg" download>
                        <i data-lucide="download" style="width: 20px; height: 20px;"></i>
                        <span><?php printf(esc_html__('Download EDM Setup (%s)', 'edm-theme'), function_exists('edm_get_download_file_size') ? esc_html(edm_get_download_file_size()) : '19.8 MB'); ?></span>
                    </a>
                    <a href="<?php echo esc_url(home_url('/nf/')); ?>" class="btn btn-outline btn-lg">
                        <i data-lucide="layout-dashboard" style="width: 20px; height: 20px;"></i>
                        <span><?php esc_html_e('Open Control Plane', 'edm-theme'); ?></span>
                    </a>
                </div>

                <div class="cta-checksum-note">
                    <span><?php printf(esc_html__('SHA-256 Checksum Verified · Size: %s · Zero Telemetry/Adware', 'edm-theme'), function_exists('edm_get_download_file_size') ? esc_html(edm_get_download_file_size()) : '19.8 MB'); ?></span>
                </div>
            </div>
        </div>
    </div>
</section>
