<?php
/**
 * Video Modal Dialog Component
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<div class="video-modal-backdrop" id="video-modal" role="dialog" aria-modal="true" aria-label="<?php esc_attr_e('Video Preview Modal', 'edm-theme'); ?>" style="display: none;">
    <div class="video-modal-card">
        <div class="video-modal-header">
            <div class="modal-title-flex">
                <i data-lucide="play-circle" style="width: 18px; height: 18px; color: var(--edm-primary-light);"></i>
                <h3 id="video-modal-title"><?php esc_html_e('EDM Dynamic Stream Sniffer Demo', 'edm-theme'); ?></h3>
            </div>
            <button class="btn-close-modal" onclick="if(window.edmSite) window.edmSite.closeVideoModal();" aria-label="<?php esc_attr_e('Close Modal', 'edm-theme'); ?>">
                <i data-lucide="x" style="width: 18px; height: 18px;"></i>
            </button>
        </div>
        <div class="video-modal-body">
            <div class="video-placeholder-container">
                <div class="video-placeholder-glow"></div>
                <div class="video-mock-player">
                    <div class="mock-player-controls">
                        <div class="mock-player-play"><i data-lucide="play" style="width: 24px; height: 24px; color: #fff;"></i></div>
                        <div class="mock-player-progress"><div class="mock-player-bar"></div></div>
                        <div class="mock-player-quality">4K · 60fps</div>
                    </div>
                </div>
            </div>
            <div class="video-modal-info">
                <p id="video-modal-desc">
                    <?php esc_html_e('Demonstrating real-time 4K video candidate detection, multi-threaded segment fetching, and automated high-speed stitching.', 'edm-theme'); ?>
                </p>
            </div>
        </div>
    </div>
</div>
