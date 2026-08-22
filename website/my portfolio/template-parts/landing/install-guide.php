<?php
/**
 * Landing Page: Step-by-Step Installation & Quick Start Guide
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : '#';
?>
<section class="section section-darker" id="install-guide">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Quick Setup', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Get Started in Less Than 60 Seconds', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Simple 3-step installation workflow designed for immediate 32-socket download acceleration.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="install-steps-grid">
            <!-- Step 1 -->
            <div class="install-step-card">
                <div class="step-num-badge">01</div>
                <div class="step-icon-wrap">
                    <i data-lucide="download" style="width: 28px; height: 28px; color: var(--edm-primary-light);"></i>
                </div>
                <h3><?php esc_html_e('1. Download & Run Setup', 'edm-theme'); ?></h3>
                <p><?php esc_html_e('Download the verified EDM Setup installer (.exe). Launch the wizard and follow the one-click native Windows setup.', 'edm-theme'); ?></p>
                <div class="step-cta-link">
                    <a href="<?php echo esc_url($download_url); ?>" class="text-link" download>
                        <span><?php esc_html_e('Download Installer (19.8 MB)', 'edm-theme'); ?></span> &rarr;
                    </a>
                </div>
            </div>

            <!-- Step 2 -->
            <div class="install-step-card">
                <div class="step-num-badge">02</div>
                <div class="step-icon-wrap">
                    <i data-lucide="puzzle" style="width: 28px; height: 28px; color: var(--edm-blue);"></i>
                </div>
                <h3><?php esc_html_e('2. Enable Extension', 'edm-theme'); ?></h3>
                <p><?php esc_html_e('Upon first launch, EDM automatically prompts to link with your default browser (Chrome, Edge, or Firefox).', 'edm-theme'); ?></p>
                <div class="step-cta-link">
                    <a href="#extension" class="text-link">
                        <span><?php esc_html_e('View Supported Browsers', 'edm-theme'); ?></span> &rarr;
                    </a>
                </div>
            </div>

            <!-- Step 3 -->
            <div class="install-step-card">
                <div class="step-num-badge">03</div>
                <div class="step-icon-wrap">
                    <i data-lucide="zap" style="width: 28px; height: 28px; color: var(--edm-green);"></i>
                </div>
                <h3><?php esc_html_e('3. Enjoy 32x Speed', 'edm-theme'); ?></h3>
                <p><?php esc_html_e('Any download link or video URL you click in your browser is automatically intercepted into 32 high-speed sockets.', 'edm-theme'); ?></p>
                <div class="step-cta-link">
                    <a href="#hero" class="text-link">
                        <span><?php esc_html_e('Test Speed Simulator', 'edm-theme'); ?></span> &rarr;
                    </a>
                </div>
            </div>
        </div>
    </div>
</section>
