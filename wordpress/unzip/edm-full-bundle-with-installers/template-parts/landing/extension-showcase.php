<?php
/**
 * Landing Page: Browser Extension Integration Section Template Part
 * Real Downloads for Chrome, Edge, and Firefox Manifest V3 Extensions
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$chrome_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('chrome') : esc_url(get_template_directory_uri() . '/downloads/edm-chrome-extension-v1.0.0.zip');
$edge_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('edge') : esc_url(get_template_directory_uri() . '/downloads/edm-edge-extension-v1.0.0.zip');
$firefox_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('firefox') : esc_url(get_template_directory_uri() . '/downloads/edm-firefox-extension-v1.0.0.zip');
?>
<section class="section section-darker" id="extension">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Seamless Ecosystem', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Zero-Latency Browser Integration (Manifest V3)', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('The official EDM Browser Integration extension attaches securely via Windows Native Messaging, passing session cookies, referrers, and user-agent strings effortlessly.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="browser-grid-4">
            <!-- 1. Google Chrome -->
            <div class="browser-card">
                <div class="browser-icon-wrap" style="color: #EA4335;">
                    <i data-lucide="chrome" style="width: 36px; height: 36px;"></i>
                </div>
                <h3>Google Chrome</h3>
                <p><?php esc_html_e('Official Manifest V3 extension available with auto-takeover and multi-stream turbo sniffer.', 'edm-theme'); ?></p>
                <span class="browser-status-tag"><i data-lucide="check" style="width: 12px; height: 12px;"></i> <?php esc_html_e('v1.0.0 · 80.1 KB', 'edm-theme'); ?></span>
                <div style="margin-top: 14px;">
                    <a href="<?php echo $chrome_ext_url; ?>" class="btn btn-outline btn-sm w-full" download>
                        <i data-lucide="download" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Download Chrome Extension', 'edm-theme'); ?>
                    </a>
                </div>
            </div>

            <!-- 2. Microsoft Edge -->
            <div class="browser-card">
                <div class="browser-icon-wrap" style="color: #0078D7;">
                    <i data-lucide="globe" style="width: 36px; height: 36px;"></i>
                </div>
                <h3>Microsoft Edge</h3>
                <p><?php esc_html_e('Hardware-accelerated native Windows integration optimized for Edge Chromium.', 'edm-theme'); ?></p>
                <span class="browser-status-tag"><i data-lucide="check" style="width: 12px; height: 12px;"></i> <?php esc_html_e('v1.0.0 · 80.1 KB', 'edm-theme'); ?></span>
                <div style="margin-top: 14px;">
                    <a href="<?php echo $edge_ext_url; ?>" class="btn btn-outline btn-sm w-full" download>
                        <i data-lucide="download" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Download Edge Extension', 'edm-theme'); ?>
                    </a>
                </div>
            </div>

            <!-- 3. Mozilla Firefox -->
            <div class="browser-card">
                <div class="browser-icon-wrap" style="color: #FF7139;">
                    <i data-lucide="compass" style="width: 36px; height: 36px;"></i>
                </div>
                <h3>Mozilla Firefox</h3>
                <p><?php esc_html_e('Mozilla WebExtensions add-on with robust media stream candidate sniffing.', 'edm-theme'); ?></p>
                <span class="browser-status-tag"><i data-lucide="check" style="width: 12px; height: 12px;"></i> <?php esc_html_e('v1.0.0 · 80.3 KB', 'edm-theme'); ?></span>
                <div style="margin-top: 14px;">
                    <a href="<?php echo $firefox_ext_url; ?>" class="btn btn-outline btn-sm w-full" download>
                        <i data-lucide="download" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Download Firefox Add-on', 'edm-theme'); ?>
                    </a>
                </div>
            </div>

            <!-- 4. Brave & Opera -->
            <div class="browser-card">
                <div class="browser-icon-wrap" style="color: #FF1B2D;">
                    <i data-lucide="shield" style="width: 36px; height: 36px;"></i>
                </div>
                <h3>Brave & Opera</h3>
                <p><?php esc_html_e('Compatible with all Chromium-based engines (Brave, Opera, Vivaldi, Arc).', 'edm-theme'); ?></p>
                <span class="browser-status-tag"><i data-lucide="check" style="width: 12px; height: 12px;"></i> <?php esc_html_e('Chromium Core', 'edm-theme'); ?></span>
                <div style="margin-top: 14px;">
                    <a href="<?php echo $chrome_ext_url; ?>" class="btn btn-outline btn-sm w-full" download>
                        <i data-lucide="download" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Download Zip Package', 'edm-theme'); ?>
                    </a>
                </div>
            </div>
        </div>

        <div class="section-footer-cta">
            <a href="<?php echo esc_url(home_url('/browser-extension/')); ?>" class="btn btn-outline">
                <span><?php esc_html_e('View Full Extension Installation Guide', 'edm-theme'); ?></span>
                <i data-lucide="external-link" style="width: 16px; height: 16px;"></i>
            </a>
        </div>
    </div>
</section>
