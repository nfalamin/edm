<?php
/**
 * Landing Page: Features Overview Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section" id="features">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Core Capabilities', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Engineered for Maximum Speed & Unbreakable Reliability', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Every feature in EDM is designed for performance enthusiasts who demand pure bandwidth throughput and zero bloatware.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="features-grid-3">
            <!-- 1. 32x Turbo Acceleration -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #6366F1 0%, #4F46E5 100%);">
                        <i data-lucide="zap"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('32x Socket Turbo Multi-Threading', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Splits files dynamically into 32 simultaneous HTTP/HTTPS range segments, saturating gigabit fiber connections and bypassing restrictive per-connection server caps.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: HttpMultiPartEngine.cs', 'edm-theme'); ?></span>
            </div>

            <!-- 2. Dynamic 4K Stream Ripper -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #EC4899 0%, #BE185D 100%);">
                        <i data-lucide="video"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('4K & 8K Video Stream Ripper', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Automatically detects high-bitrate video streams, DASH manifests, and M3U8 playlists. Downloads parallel video and audio tracks, merging them seamlessly via FFmpeg.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: VideoCandidateDetector.cs', 'edm-theme'); ?></span>
            </div>

            <!-- 3. Durable Crash-Proof Resume -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #10B981 0%, #059669 100%);">
                        <i data-lucide="refresh-cw"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('Durable Crash-Proof Resume', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Atomic state persistence ensures zero corrupted files. If power is lost or the network drops, EDM truncates unverified trailing bytes and resumes exactly at the saved offset.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: DurableMetadataManager.cs', 'edm-theme'); ?></span>
            </div>

            <!-- 4. Zero-Click Browser Extension -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #38BDF8 0%, #0284C7 100%);">
                        <i data-lucide="puzzle"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('Zero-Click Browser Integration', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Manifest V3 browser extensions for Chrome, Microsoft Edge, and Firefox capture download clicks and stream URLs with zero latency via Native Messaging.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: NativeMessagingHost.exe', 'edm-theme'); ?></span>
            </div>

            <!-- 5. Smart Queue & Scheduler -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #F59E0B 0%, #D97706 100%);">
                        <i data-lucide="calendar"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('Smart Queue & Bandwidth Limiter', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Set scheduled download start and stop times, prioritize critical files, configure speed caps during work hours, and trigger automatic PC sleep or shutdown.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: QueueSchedulerService.cs', 'edm-theme'); ?></span>
            </div>

            <!-- 6. Antivirus & Hash Verification -->
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #8B5CF6 0%, #6D28D9 100%);">
                        <i data-lucide="shield-check"></i>
                    </div>
                    <h3 class="feature-card-title"><?php esc_html_e('Automated Hash & Antivirus Guard', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc">
                        <?php esc_html_e('Calculates cryptographic SHA-256 and MD5 checksums instantly upon download completion and triggers automatic Windows Defender scans before file execution.', 'edm-theme'); ?>
                    </p>
                </div>
                <span class="feature-tech-tag"><?php esc_html_e('Module: SecurityScanner.cs', 'edm-theme'); ?></span>
            </div>
        </div>

        <div class="section-footer-cta">
            <a href="<?php echo esc_url(home_url('/features/')); ?>" class="btn btn-outline">
                <span><?php esc_html_e('Explore All 24+ Engineering Features', 'edm-theme'); ?></span>
                <i data-lucide="arrow-right" style="width: 16px; height: 16px;"></i>
            </a>
        </div>
    </div>
</section>
