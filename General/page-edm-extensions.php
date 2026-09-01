<?php
/**
 * Template Name: EDM Browser Extensions Hub
 * Description: Dedicated page detailing Chrome, Edge, Firefox, Brave, and Opera Manifest V3 extensions.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('edm');

$chrome_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('chrome') : esc_url(get_template_directory_uri() . '/downloads/edm-chrome-extension-v1.0.0.zip');
$edge_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('edge') : esc_url(get_template_directory_uri() . '/downloads/edm-edge-extension-v1.0.0.zip');
$firefox_ext_url = function_exists('edm_get_extension_url') ? edm_get_extension_url('firefox') : esc_url(get_template_directory_uri() . '/downloads/edm-firefox-extension-v1.0.0.zip');
?>

<div class="page-container py-16">
    <div class="container">
        
        <!-- Breadcrumb & Header -->
        <div class="section-header text-center" style="margin-bottom: 48px;">
            <div style="display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 16px;">
                <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="text-link">&larr; <?php esc_html_e('Back to EDM Hub', 'edm-theme'); ?></a>
                <span style="color: var(--edm-text-muted);">/</span>
                <span style="color: var(--edm-primary-light); font-weight: 700;"><?php esc_html_e('Browser Extensions Hub', 'edm-theme'); ?></span>
            </div>

            <div style="display: flex; align-items: center; justify-content: center; gap: 12px; margin-bottom: 12px;">
                <img src="<?php echo esc_url(get_template_directory_uri() . '/edm-logo.png'); ?>" alt="EDM Logo" style="width: 44px; height: 44px; object-fit: contain;">
                <span class="section-badge"><?php esc_html_e('MANIFEST V3 CERTIFIED', 'edm-theme'); ?></span>
            </div>

            <h1 class="hero-title" style="font-size: 38px;">
                <?php esc_html_e('Seamless Browser Integration Suite', 'edm-theme'); ?><br>
                <span class="gradient-text"><?php esc_html_e('Zero-Click Download Takeover & 4K Media Sniffer', 'edm-theme'); ?></span>
            </h1>
            <p class="section-subtitle" style="max-width: 780px; margin: 16px auto 0;">
                <?php esc_html_e('Attach EDM directly to Google Chrome, Microsoft Edge, Mozilla Firefox, Brave, and Opera via native Windows IPC messaging for instant 32-socket acceleration.', 'edm-theme'); ?>
            </p>
        </div>

        <!-- 4-Browser Download Cards Grid -->
        <div class="browser-grid-4" style="margin-bottom: 56px;">
            
            <!-- Chrome -->
            <div class="browser-card" style="padding: 32px 24px;">
                <div class="browser-icon-wrap" style="color: #EA4335; margin-bottom: 16px;">
                    <i data-lucide="chrome" style="width: 44px; height: 44px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; color: #fff;">Google Chrome</h3>
                <p style="font-size: 13px; color: var(--edm-text-secondary); line-height: 1.6; margin: 10px 0 16px;">
                    <?php esc_html_e('Complete Manifest V3 extension with native video sniffer bar, right-click batch download menu, and automatic cookie forwarding.', 'edm-theme'); ?>
                </p>
                <span class="browser-status-tag" style="margin-bottom: 20px; display: inline-flex;"><i data-lucide="check" style="width: 12px; height: 12px;"></i> v1.0.0 · 80.1 KB</span>
                <a href="<?php echo $chrome_ext_url; ?>" class="btn btn-primary btn-full" download>
                    <i data-lucide="download" style="width: 16px; height: 16px;"></i>
                    <span><?php esc_html_e('Download Chrome Extension (.zip)', 'edm-theme'); ?></span>
                </a>
            </div>

            <!-- Edge -->
            <div class="browser-card" style="padding: 32px 24px;">
                <div class="browser-icon-wrap" style="color: #0078D7; margin-bottom: 16px;">
                    <i data-lucide="globe" style="width: 44px; height: 44px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; color: #fff;">Microsoft Edge</h3>
                <p style="font-size: 13px; color: var(--edm-text-secondary); line-height: 1.6; margin: 10px 0 16px;">
                    <?php esc_html_e('Hardware-accelerated native Edge Chromium integration with instant PDF/ISO takeover and silent background bridge.', 'edm-theme'); ?>
                </p>
                <span class="browser-status-tag" style="margin-bottom: 20px; display: inline-flex;"><i data-lucide="check" style="width: 12px; height: 12px;"></i> v1.0.0 · 80.1 KB</span>
                <a href="<?php echo $edge_ext_url; ?>" class="btn btn-primary btn-full" download>
                    <i data-lucide="download" style="width: 16px; height: 16px;"></i>
                    <span><?php esc_html_e('Download Edge Extension (.zip)', 'edm-theme'); ?></span>
                </a>
            </div>

            <!-- Firefox -->
            <div class="browser-card" style="padding: 32px 24px;">
                <div class="browser-icon-wrap" style="color: #FF7139; margin-bottom: 16px;">
                    <i data-lucide="compass" style="width: 44px; height: 44px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; color: #fff;">Mozilla Firefox</h3>
                <p style="font-size: 13px; color: var(--edm-text-secondary); line-height: 1.6; margin: 10px 0 16px;">
                    <?php esc_html_e('WebExtensions framework module with advanced HLS (.m3u8) video candidate sniffer and private browsing support.', 'edm-theme'); ?>
                </p>
                <span class="browser-status-tag" style="margin-bottom: 20px; display: inline-flex;"><i data-lucide="check" style="width: 12px; height: 12px;"></i> v1.0.0 · 80.3 KB</span>
                <a href="<?php echo $firefox_ext_url; ?>" class="btn btn-primary btn-full" download>
                    <i data-lucide="download" style="width: 16px; height: 16px;"></i>
                    <span><?php esc_html_e('Download Firefox Add-on (.zip)', 'edm-theme'); ?></span>
                </a>
            </div>

            <!-- Brave & Opera -->
            <div class="browser-card" style="padding: 32px 24px;">
                <div class="browser-icon-wrap" style="color: #FF1B2D; margin-bottom: 16px;">
                    <i data-lucide="shield" style="width: 44px; height: 44px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; color: #fff;">Brave & Opera</h3>
                <p style="font-size: 13px; color: var(--edm-text-secondary); line-height: 1.6; margin: 10px 0 16px;">
                    <?php esc_html_e('Full Chromium binary compatibility. Works natively on Brave, Opera, Vivaldi, Arc Browser, and Chromium builds.', 'edm-theme'); ?>
                </p>
                <span class="browser-status-tag" style="margin-bottom: 20px; display: inline-flex;"><i data-lucide="check" style="width: 12px; height: 12px;"></i> Chromium Core</span>
                <a href="<?php echo $chrome_ext_url; ?>" class="btn btn-secondary btn-full" download>
                    <i data-lucide="download" style="width: 16px; height: 16px;"></i>
                    <span><?php esc_html_e('Download Chromium Package', 'edm-theme'); ?></span>
                </a>
            </div>

        </div>

        <!-- Step-by-Step Installation Guide for Extensions -->
        <div class="glass-panel" style="padding: 40px 36px; border-radius: 24px; border: 1px solid var(--edm-border); margin-bottom: 48px;">
            <h2 style="font-size: 24px; font-weight: 800; color: #fff; margin-bottom: 24px; display: flex; align-items: center; gap: 10px;">
                <i data-lucide="help-circle" style="color: var(--edm-primary-light);"></i>
                <?php esc_html_e('How to Install & Enable the Extension in Under 30 Seconds', 'edm-theme'); ?>
            </h2>

            <div class="install-steps-grid" style="margin-top: 24px;">
                <div class="install-step-card">
                    <div class="step-num-badge">01</div>
                    <div class="step-icon-wrap"><i data-lucide="folder-archive" style="color: var(--edm-primary-light);"></i></div>
                    <h3>1. Extract ZIP Archive</h3>
                    <p>Download the extension .zip for your browser and extract the files into a permanent folder (e.g. <code>C:\EDM\extension</code>).</p>
                </div>
                <div class="install-step-card">
                    <div class="step-num-badge">02</div>
                    <div class="step-icon-wrap"><i data-lucide="toggle-right" style="color: var(--edm-blue);"></i></div>
                    <h3>2. Enable Developer Mode</h3>
                    <p>Open <code>chrome://extensions</code> or <code>edge://extensions</code> in your browser and toggle <strong>Developer mode</strong> on (top right).</p>
                </div>
                <div class="install-step-card">
                    <div class="step-num-badge">03</div>
                    <div class="step-icon-wrap"><i data-lucide="upload-cloud" style="color: var(--edm-green);"></i></div>
                    <h3>3. Load Unpacked</h3>
                    <p>Click <strong>"Load unpacked"</strong>, select the extracted extension folder, and EDM will immediately begin capturing all downloads.</p>
                </div>
            </div>
        </div>

        <!-- Extension Core Capabilities Matrix -->
        <div class="glass-panel" style="padding: 36px; border-radius: 24px; border: 1px solid var(--edm-border);">
            <h2 style="font-size: 22px; font-weight: 800; color: #fff; margin-bottom: 20px;">
                <?php esc_html_e('Extension Security & Architecture Guarantees', 'edm-theme'); ?>
            </h2>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px;">
                <div style="display: flex; gap: 12px;">
                    <i data-lucide="shield-check" style="color: var(--edm-green); width: 24px; height: 24px; shrink: 0;"></i>
                    <div>
                        <strong style="color: #fff; display: block; margin-bottom: 4px;">Zero Tracking / Zero Telemetry</strong>
                        <span style="color: var(--edm-text-secondary); font-size: 12.5px;">The extension communicates exclusively with your local Windows EDM process via Native Messaging. No data is sent to external cloud servers.</span>
                    </div>
                </div>
                <div style="display: flex; gap: 12px;">
                    <i data-lucide="cpu" style="color: var(--edm-blue); width: 24px; height: 24px; shrink: 0;"></i>
                    <div>
                        <strong style="color: #fff; display: block; margin-bottom: 4px;">Lightweight Background Service Worker</strong>
                        <span style="color: var(--edm-text-secondary); font-size: 12.5px;">Built strictly under Manifest V3 specifications using non-persistent service workers for zero impact on browser RAM and battery life.</span>
                    </div>
                </div>
                <div style="display: flex; gap: 12px;">
                    <i data-lucide="lock" style="color: var(--edm-primary-light); width: 24px; height: 24px; shrink: 0;"></i>
                    <div>
                        <strong style="color: #fff; display: block; margin-bottom: 4px;">Encrypted Session Handshake</strong>
                        <span style="color: var(--edm-text-secondary); font-size: 12.5px;">Forwards authenticated cookies, authorization bearer tokens, and referrers securely so protected private downloads complete seamlessly.</span>
                    </div>
                </div>
            </div>
        </div>

    </div>
</div>

<?php
get_footer('edm');
