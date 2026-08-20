<?php
/**
 * Landing Page: System Requirements Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section section-darker" id="system-specs">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Verified Specifications', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('System Requirements & Compatibility', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('EDM is engineered specifically for Windows with lightweight native execution and minimal memory footprint.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="specs-table-card">
            <div class="specs-row">
                <div class="specs-col-label"><?php esc_html_e('Supported Operating Systems', 'edm-theme'); ?></div>
                <div class="specs-col-value"><?php esc_html_e('Windows 11, Windows 10, Windows 8.1, Windows 7 (64-bit and ARM64)', 'edm-theme'); ?></div>
            </div>
            <div class="specs-row">
                <div class="specs-col-label"><?php esc_html_e('Runtime Framework', 'edm-theme'); ?></div>
                <div class="specs-col-value"><?php esc_html_e('.NET 10.0 Windows Desktop Runtime (Self-contained, bundled in installer)', 'edm-theme'); ?></div>
            </div>
            <div class="specs-row">
                <div class="specs-col-label"><?php esc_html_e('Memory (RAM) Footprint', 'edm-theme'); ?></div>
                <div class="specs-col-value"><?php esc_html_e('38 MB idle · 85 MB during active 32-socket 4K stream download', 'edm-theme'); ?></div>
            </div>
            <div class="specs-row">
                <div class="specs-col-label"><?php esc_html_e('Storage Disk Space', 'edm-theme'); ?></div>
                <div class="specs-col-value"><?php esc_html_e('25 MB free disk space required for application installation', 'edm-theme'); ?></div>
            </div>
            <div class="specs-row">
                <div class="specs-col-label"><?php esc_html_e('Hardware Acceleration', 'edm-theme'); ?></div>
                <div class="specs-col-value"><?php esc_html_e('DirectX 11 / Direct3D 12 GPU acceleration for fluid WPF UI rendering', 'edm-theme'); ?></div>
            </div>
        </div>
    </div>
</section>
