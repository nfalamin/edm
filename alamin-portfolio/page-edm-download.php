<?php
/**
 * Template Name: EDM Official Download Hub
 * Description: Dedicated release delivery and cryptographic checksum authority page.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('edm');

$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : esc_url(get_template_directory_uri() . '/downloads/EDM-Setup-v2.1.0.exe');
$version = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';
$chrome_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('chrome') : esc_url(get_template_directory_uri() . '/downloads/edm-chrome-extension-v1.0.0.zip');
?>

<div class="page-container py-16">
    <div class="container">
        
        <!-- Header -->
        <div class="section-header text-center" style="margin-bottom: 48px;">
            <div style="display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 16px;">
                <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="text-link">&larr; <?php esc_html_e('Back to EDM Hub', 'edm-theme'); ?></a>
                <span style="color: var(--edm-text-muted);">/</span>
                <span style="color: var(--edm-primary-light); font-weight: 700;"><?php esc_html_e('Official Download Hub', 'edm-theme'); ?></span>
            </div>

            <div style="display: flex; align-items: center; justify-content: center; gap: 12px; margin-bottom: 12px;">
                <img src="<?php echo esc_url(get_template_directory_uri() . '/edm-logo.png'); ?>" alt="EDM Logo" style="width: 48px; height: 48px; object-fit: contain;">
                <span class="section-badge"><?php esc_html_e('OFFICIAL RELEASE v' . $version, 'edm-theme'); ?></span>
            </div>

            <h1 class="hero-title" style="font-size: 38px;">
                <?php esc_html_e('Download Exclusive Download Manager', 'edm-theme'); ?><br>
                <span class="gradient-text"><?php esc_html_e('Verified Production Binaries & Packages', 'edm-theme'); ?></span>
            </h1>
            <p class="section-subtitle" style="max-width: 780px; margin: 16px auto 0;">
                <?php esc_html_e('Single source of truth release delivery. Built directly from certified repository sources with Authenticode cryptographic signature.', 'edm-theme'); ?>
            </p>
        </div>

        <!-- Download Cards Grid -->
        <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 32px; margin-bottom: 56px;" class="download-grid-layout">
            
            <!-- Main Windows Installer -->
            <div class="glass-panel" style="padding: 40px 36px; border-radius: 24px; border: 1px solid var(--edm-border-accent); display: flex; flex-direction: column; justify-content: space-between;">
                <div>
                    <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px;">
                        <span class="badge-success" style="font-size: 11px; padding: 4px 10px;">STABLE PRODUCTION RELEASE</span>
                        <span style="font-family: var(--edm-font-mono); font-size: 13px; color: var(--edm-text-secondary);">Build v<?php echo esc_html($version); ?> • 19.8 MB</span>
                    </div>

                    <h2 style="font-size: 26px; font-weight: 800; color: #fff; margin-bottom: 12px;">
                        EDM for Windows (64-bit & ARM64)
                    </h2>
                    <p style="color: var(--edm-text-secondary); font-size: 14px; line-height: 1.6; margin-bottom: 28px;">
                        Complete desktop suite including 32x Turbo accelerator, background YouTube/4K stream grabber, and native browser handoff bridge.
                    </p>

                    <!-- Main Download Button -->
                    <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-xl btn-full" download style="margin-bottom: 24px;">
                        <i data-lucide="download" style="width: 22px; height: 22px;"></i>
                        <span><?php esc_html_e('Download EDM Setup.exe (19.8 MB)', 'edm-theme'); ?></span>
                    </a>
                </div>

                <!-- SHA-256 Checksum Authority -->
                <div style="background: rgba(4, 8, 20, 0.9); border: 1px solid var(--edm-border); border-radius: 12px; padding: 16px;">
                    <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
                        <i data-lucide="lock" style="width: 15px; height: 15px; color: var(--edm-green);"></i>
                        <span style="font-size: 12px; font-weight: 700; color: #fff;">Cryptographic SHA-256 Checksum Authority:</span>
                    </div>
                    <code style="display: block; font-family: var(--edm-font-mono); font-size: 11px; color: var(--edm-primary-light); word-break: break-all; background: rgba(0,0,0,0.3); padding: 8px; border-radius: 6px;">
                        93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023
                    </code>
                </div>
            </div>

            <!-- Additional Packages Column -->
            <div style="display: flex; flex-direction: column; gap: 24px;">
                
                <!-- Portable ZIP -->
                <div class="glass-panel" style="padding: 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                    <div style="display: flex; align-items: center; gap: 10px; margin-bottom: 12px;">
                        <i data-lucide="archive" style="color: var(--edm-blue); width: 22px; height: 22px;"></i>
                        <h3 style="font-size: 17px; font-weight: 800; color: #fff;">Portable ZIP Distribution</h3>
                    </div>
                    <p style="font-size: 12.5px; color: var(--edm-text-secondary); line-height: 1.5; margin-bottom: 16px;">
                        Zero installation required. Run EDM directly from any USB flash drive or portable storage.
                    </p>
                    <a href="<?php echo esc_url($download_url); ?>" class="btn btn-outline btn-sm btn-full" download>
                        <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                        <span><?php esc_html_e('Download Portable Package', 'edm-theme'); ?></span>
                    </a>
                </div>

                <!-- Browser Extensions Bundle -->
                <div class="glass-panel" style="padding: 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                    <div style="display: flex; align-items: center; gap: 10px; margin-bottom: 12px;">
                        <i data-lucide="puzzle" style="color: var(--edm-amber); width: 22px; height: 22px;"></i>
                        <h3 style="font-size: 17px; font-weight: 800; color: #fff;">Browser Extensions Bundle</h3>
                    </div>
                    <p style="font-size: 12.5px; color: var(--edm-text-secondary); line-height: 1.5; margin-bottom: 16px;">
                        Chrome, Edge Chromium, and Firefox Manifest V3 packages.
                    </p>
                    <a href="<?php echo esc_url(home_url('/edm-extensions/')); ?>" class="btn btn-secondary btn-sm btn-full">
                        <i data-lucide="external-link" style="width: 14px; height: 14px;"></i>
                        <span><?php esc_html_e('View Extensions Guide', 'edm-theme'); ?></span>
                    </a>
                </div>

            </div>

        </div>

    </div>
</div>

<?php
get_footer('edm');
