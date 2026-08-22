<?php
/**
 * Landing Page: Hero & 32-Socket Simulator Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$version = edm_get_latest_version();
$download_url = edm_get_download_url();
?>
<!-- ══════════════════════════════════════════════════════════════
     HERO SECTION (CONVERSION & SPEED FOCUS)
     ══════════════════════════════════════════════════════════════ -->
<section class="hero-section" id="hero">
    <div class="hero-glow-bg"></div>
    <div class="container">
        <div class="hero-content">
            <!-- Floating Platform Pills -->
            <div class="floating-pills-wrap">
                <div class="floating-pill" style="background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); color: #10B981;"><i data-lucide="users" style="width: 13px; height: 13px;"></i> 10,000+ Active Users</div>
                <div class="floating-pill" style="background: rgba(245, 158, 11, 0.15); border-color: rgba(245, 158, 11, 0.35); color: #F59E0B;"><i data-lucide="star" style="width: 13px; height: 13px;"></i> 4.9/5 ★ (1,840+ Reviews)</div>
                <div class="floating-pill"><i data-lucide="monitor" style="width: 13px; height: 13px; color: #38BDF8;"></i> Windows 11 / 10 / 8.1 / 7</div>
                <div class="floating-pill"><i data-lucide="cpu" style="width: 13px; height: 13px; color: #10B981;"></i> 32-Socket Turbo</div>
                <div class="floating-pill"><i data-lucide="video" style="width: 13px; height: 13px; color: #EC4899;"></i> 4K / 8K Video Ripper</div>
                <div class="floating-pill"><i data-lucide="puzzle" style="width: 13px; height: 13px; color: #F59E0B;"></i> Chrome & Edge MV3</div>
            </div>

            <div class="hero-pill-badge">
                <i data-lucide="sparkles" style="width: 14px; height: 14px;"></i>
                <span id="hero-pill-text"><?php printf(esc_html__('Exclusive Download Manager • Production Build v%s', 'edm-theme'), esc_html($version)); ?></span>
            </div>

            <h1 class="hero-title">
                <?php esc_html_e('The Fastest Download Manager for Windows', 'edm-theme'); ?><br>
                <span class="gradient-text"><?php esc_html_e('Engineered for Unmatched Speed & Control', 'edm-theme'); ?></span>
            </h1>

            <p class="hero-subtitle">
                <?php esc_html_e('Turbocharge your files, high-bitrate video streams, and large archives with 32 concurrent socket connections, crash-proof durable resume, and zero-click browser auto-interception.', 'edm-theme'); ?>
            </p>

            <!-- URL Sniffer Search Capsule -->
            <div class="url-sniffer-capsule">
                <i data-lucide="link" style="width: 18px; height: 18px; color: var(--edm-primary-light); margin-left: 6px;"></i>
                <input type="text" id="url-sniffer-input" class="sniffer-input" placeholder="<?php esc_attr_e('Paste any download link, YouTube/Vimeo video URL, or ISO link to test 32x sniffer...', 'edm-theme'); ?>">
                <button type="button" class="btn btn-primary" onclick="if(window.edmSite) window.edmSite.handleSniffUrl();">
                    <i data-lucide="zap" style="width: 14px; height: 14px;"></i>
                    <span><?php esc_html_e('Sniff & Turbo Download', 'edm-theme'); ?></span>
                </button>
            </div>

            <!-- Call to Action Buttons -->
            <div class="hero-cta-group">
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-lg" style="box-shadow: 0 0 35px rgba(93, 95, 239, 0.45);" download>
                    <i data-lucide="download" style="width: 18px; height: 18px;"></i>
                    <span><?php esc_html_e('Download EDM for Windows', 'edm-theme'); ?></span>
                </a>
                <a href="<?php echo esc_url(home_url('/edm-features/')); ?>" class="btn btn-secondary btn-lg">
                    <i data-lucide="sliders" style="width: 18px; height: 18px; color: var(--edm-primary-light);"></i>
                    <span><?php esc_html_e('Explore Features', 'edm-theme'); ?></span>
                </a>
            </div>

            <!-- Compatibility Footnote -->
            <div class="hero-compatibility-row">
                <span><i data-lucide="check-circle" style="width: 13px; height: 13px; color: var(--edm-green); display: inline-block; vertical-align: middle;"></i> <?php esc_html_e('Windows 11 / 10 / 8.1 / 7 (64-bit & ARM64)', 'edm-theme'); ?></span>
                <span>•</span>
                <span><?php esc_html_e('Installer Size:', 'edm-theme'); ?> <strong>19.8 MB</strong></span>
                <span>•</span>
                <span><?php esc_html_e('SHA-256 Verified Clean', 'edm-theme'); ?></span>
            </div>
        </div>
    </div>
</section>

<!-- ══════════════════════════════════════════════════════════════
     LIVE DOWNLOAD ENGINE & 32-STREAM SOCKET SIMULATOR
     ══════════════════════════════════════════════════════════════ -->
<section class="preview-section" id="live-simulator">
    <div class="container">
        <div class="product-window-card">
            <div class="window-header">
                <div class="window-dots">
                    <div class="window-dot dot-red"></div>
                    <div class="window-dot dot-yellow"></div>
                    <div class="window-dot dot-green"></div>
                </div>
                <div class="window-title"><?php esc_html_e('EDM — Active 32-Socket Download Accelerator [Live Engine]', 'edm-theme'); ?></div>
                <div style="font-size: 11.5px; color: var(--edm-green); font-weight: 700; display: flex; align-items: center; gap: 5px;">
                    <span style="width: 8px; height: 8px; border-radius: 50%; background: var(--edm-green); display: inline-block; box-shadow: 0 0 8px #10B981;"></span>
                    <span id="engine-status-text"><?php esc_html_e('Engine Online', 'edm-theme'); ?></span>
                </div>
            </div>

            <div class="window-body">
                <div class="simulator-stats-grid">
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Download Speed', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" style="color: var(--edm-primary-light);" id="sim-speed-val">14.8 MB/s</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Active Connections', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" id="sim-streams-val">32 / 32 Streams</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Time Remaining', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" id="sim-time-val">00:38</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Resume Capability', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" style="color: var(--edm-green);"><?php esc_html_e('Supported', 'edm-theme'); ?></div>
                    </div>
                </div>

                <div class="sim-progress-wrap">
                    <div class="sim-file-info">
                        <span>Ubuntu-24.04-LTS-Desktop-x64.iso (5.80 GB)</span>
                        <span id="sim-progress-text"><?php esc_html_e('72% Completed (4.18 GB)', 'edm-theme'); ?></span>
                    </div>
                    <div class="sim-progress-bar-bg">
                        <div class="sim-progress-bar-fill" id="sim-progress-fill" style="width: 72%;"></div>
                    </div>

                    <!-- 32 Connection Threads Grid -->
                    <div class="streams-grid" id="streams-grid"></div>
                </div>

                <div class="simulator-controls">
                    <div style="display: flex; gap: 8px;">
                        <button type="button" class="btn btn-secondary btn-sm" id="btn-sim-pause" onclick="if(window.edmSite) window.edmSite.toggleSimPause();">
                            <i data-lucide="pause" id="sim-pause-icon" style="width: 12px; height: 12px;"></i>
                            <span id="sim-pause-text"><?php esc_html_e('Pause Engine', 'edm-theme'); ?></span>
                        </button>
                        <button type="button" class="btn btn-primary btn-sm" onclick="if(window.edmSite) window.edmSite.boostTurbo();">
                            <i data-lucide="flame" style="width: 12px; height: 12px;"></i>
                            <span><?php esc_html_e('Turbo Boost (48.6 MB/s)', 'edm-theme'); ?></span>
                        </button>
                    </div>
                    <span style="font-size: 11.5px; color: var(--edm-text-muted);"><?php esc_html_e('Dynamic HTTP Range Multi-threading Active', 'edm-theme'); ?></span>
                </div>
            </div>
        </div>
    </div>
</section>
