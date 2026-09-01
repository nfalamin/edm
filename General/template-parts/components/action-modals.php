<?php
/**
 * Global Frontend Action Modals (Download Popup & Sniffer Result)
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = edm_get_download_url();
$version = edm_get_latest_version();
?>
<!-- 1. DOWNLOAD SUCCESS & SETUP ASSISTANT MODAL -->
<div class="modal-backdrop" id="modal-download" role="dialog" aria-modal="true" style="display: none;">
    <div class="modal-dialog" style="max-width: 520px;">
        <div class="modal-header">
            <span class="modal-title" style="display: flex; align-items: center; gap: 8px;">
                <i data-lucide="check-circle" style="color: var(--edm-green); width: 18px; height: 18px;"></i> 
                <span><?php printf(esc_html__('EDM v%s Download Initialized', 'edm-theme'), esc_html($version)); ?></span>
            </span>
            <button type="button" class="btn-theme-toggle" onclick="if(window.edmSite) window.edmSite.closeModal('modal-download');" aria-label="<?php esc_attr_e('Close', 'edm-theme'); ?>">
                <i data-lucide="x"></i>
            </button>
        </div>
        
        <div class="modal-body" style="padding: 24px;">
            <div style="text-align: center; margin-bottom: 20px;">
                <div style="width: 52px; height: 52px; border-radius: 50%; background: rgba(16, 185, 129, 0.12); color: var(--edm-green); border: 1px solid rgba(16, 185, 129, 0.3); display: flex; align-items: center; justify-content: center; margin: 0 auto 12px auto; box-shadow: 0 0 20px rgba(16, 185, 129, 0.2);">
                    <i data-lucide="download" style="width: 24px; height: 24px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; color: var(--edm-text-main); margin-bottom: 4px;"><?php esc_html_e('Thank you for downloading EDM!', 'edm-theme'); ?></h3>
                <p style="font-size: 13px; color: var(--edm-text-muted);">
                    <?php esc_html_e('Your 32-socket accelerated installer is saving to your default downloads folder.', 'edm-theme'); ?>
                </p>
            </div>

            <!-- Quick 3-Step Setup Guide -->
            <div style="background: var(--edm-bg-subtle); border: 1px solid var(--edm-border); border-radius: var(--edm-radius-lg); padding: 16px; margin-bottom: 18px;">
                <div style="font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.5px; color: var(--edm-primary-light); margin-bottom: 12px;">
                    <?php esc_html_e('Next Steps (30-Second Setup):', 'edm-theme'); ?>
                </div>
                <div style="display: flex; flex-direction: column; gap: 10px; font-size: 12.5px; color: var(--edm-text-main);">
                    <div style="display: flex; gap: 10px; align-items: flex-start;">
                        <span style="width: 20px; height: 20px; border-radius: 50%; background: var(--edm-primary-soft); color: var(--edm-primary-light); font-weight: 800; font-size: 11px; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">1</span>
                        <span><?php esc_html_e('Open the downloaded file', 'edm-theme'); ?> <code>EDM-Setup.exe</code> <?php esc_html_e('from your browser bar.', 'edm-theme'); ?></span>
                    </div>
                    <div style="display: flex; gap: 10px; align-items: flex-start;">
                        <span style="width: 20px; height: 20px; border-radius: 50%; background: var(--edm-primary-soft); color: var(--edm-primary-light); font-weight: 800; font-size: 11px; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">2</span>
                        <span><?php esc_html_e('Follow the rapid setup wizard to register Windows shell hooks.', 'edm-theme'); ?></span>
                    </div>
                    <div style="display: flex; gap: 10px; align-items: flex-start;">
                        <span style="width: 20px; height: 20px; border-radius: 50%; background: var(--edm-primary-soft); color: var(--edm-primary-light); font-weight: 800; font-size: 11px; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">3</span>
                        <span><?php esc_html_e('Install the Chrome, Edge, or Firefox extension for zero-click downloads.', 'edm-theme'); ?></span>
                    </div>
                </div>
            </div>

            <div style="font-size: 11.5px; color: var(--edm-text-muted); text-align: center;">
                <?php esc_html_e('Didn\'t start automatically?', 'edm-theme'); ?> 
                <a href="<?php echo esc_url($download_url); ?>" download style="color: var(--edm-primary-light); font-weight: 700; text-decoration: underline;"><?php esc_html_e('Click here to retry download', 'edm-theme'); ?></a>
            </div>
        </div>

        <div class="modal-footer" style="padding: 16px 24px; border-top: 1px solid var(--edm-border); display: flex; justify-content: space-between; align-items: center;">
            <a href="<?php echo esc_url(home_url('/browser-extension/')); ?>" class="btn btn-outline btn-sm">
                <i data-lucide="puzzle" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('Get Browser Extensions', 'edm-theme'); ?></span>
            </a>
            <button type="button" class="btn btn-primary btn-sm" onclick="if(window.edmSite) window.edmSite.closeModal('modal-download');">
                <span><?php esc_html_e('Got It, Continue', 'edm-theme'); ?></span>
            </button>
        </div>
    </div>
</div>

<!-- 2. STREAM SNIFFER TEST RESULT MODAL -->
<div class="modal-backdrop" id="modal-sniffer-result" role="dialog" aria-modal="true" style="display: none;">
    <div class="modal-dialog">
        <div class="modal-header">
            <span class="modal-title"><i data-lucide="zap" style="color: var(--edm-green);"></i> <?php esc_html_e('Stream Captured Successfully!', 'edm-theme'); ?></span>
            <button type="button" class="btn-theme-toggle" onclick="if(window.edmSite) window.edmSite.closeModal('modal-sniffer-result');" aria-label="<?php esc_attr_e('Close', 'edm-theme'); ?>">
                <i data-lucide="x"></i>
            </button>
        </div>
        <div class="modal-body">
            <div style="background: var(--edm-bg-subtle); padding: 14px; border-radius: var(--edm-radius-md); border: 1px solid var(--edm-border); margin-bottom: 16px;">
                <div style="font-size: 11px; color: var(--edm-text-muted);"><?php esc_html_e('Parsed Stream URL:', 'edm-theme'); ?></div>
                <code style="font-size: 12px; color: var(--edm-primary-light); word-break: break-all;" id="sniffer-detected-url">https://stream.media/video_4k_master.m3u8</code>
            </div>
            <ul style="list-style: none; display: flex; flex-direction: column; gap: 8px; font-size: 12.5px;">
                <li><strong><?php esc_html_e('Protocol:', 'edm-theme'); ?></strong> HTTP/2 Multi-Range</li>
                <li><strong><?php esc_html_e('Allocated Streams:', 'edm-theme'); ?></strong> 32 Threads</li>
                <li><strong><?php esc_html_e('Estimated Transfer Rate:', 'edm-theme'); ?></strong> ~28.5 MB/s</li>
            </ul>
        </div>
        <div class="modal-footer">
            <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary" download>
                <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                <span><?php esc_html_e('Download with 32x Turbo in EDM', 'edm-theme'); ?></span>
            </a>
        </div>
    </div>
</div>
