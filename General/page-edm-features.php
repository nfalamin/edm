<?php
/**
 * Template Name: EDM Core Features & Architecture Deep Dive
 * Description: Dedicated in-depth architecture whitepaper and feature breakdown page.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('edm');

$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : esc_url(home_url('/downloads/EDM-Setup-v2.1.0.exe'));
$version = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';
?>

<div class="page-container py-16">
    <div class="container">
        
        <!-- Header -->
        <div class="section-header text-center" style="margin-bottom: 56px;">
            <div style="display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 16px;">
                <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="text-link">&larr; <?php esc_html_e('Back to EDM Hub', 'edm-theme'); ?></a>
                <span style="color: var(--edm-text-muted);">/</span>
                <span style="color: var(--edm-primary-light); font-weight: 700;"><?php esc_html_e('Features & Architecture', 'edm-theme'); ?></span>
            </div>

            <div style="display: flex; align-items: center; justify-content: center; gap: 12px; margin-bottom: 12px;">
                <img src="<?php echo esc_url(get_template_directory_uri() . '/edm-logo.png'); ?>" alt="EDM Logo" style="width: 44px; height: 44px; object-fit: contain;">
                <span class="section-badge"><?php esc_html_e('DEEP-DIVE TECHNICAL SPECIFICATIONS', 'edm-theme'); ?></span>
            </div>

            <h1 class="hero-title" style="font-size: 38px;">
                <?php esc_html_e('Engineered for Uncompromised Speed', 'edm-theme'); ?><br>
                <span class="gradient-text"><?php esc_html_e('The Science Behind 32-Socket Download Acceleration', 'edm-theme'); ?></span>
            </h1>
            <p class="section-subtitle" style="max-width: 820px; margin: 16px auto 0;">
                <?php esc_html_e('Explore how EDM’s dynamic multi-part range splitting, low-overhead memory stitching, and native C# Win32 socket core deliver up to 32x faster downloads than standard browsers.', 'edm-theme'); ?>
            </p>
        </div>

        <!-- 6 In-Depth Technical Pillars Grid -->
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 28px; margin-bottom: 64px;">
            
            <!-- Pillar 1 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: var(--edm-primary-soft); border: 1px solid var(--edm-border-accent); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-primary-light);">
                    <i data-lucide="cpu" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">32-Socket Dynamic Range Engine</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    When standard browsers request a file, they use a single HTTP GET stream. If the web server throttles single connections to 2 MB/s, your 100 Mbps fiber line sits idle. EDM dynamically partitions the remote file into up to 32 concurrent byte-ranges, simultaneously pulling data over 32 distinct TCP sockets to maximize your ISP pipeline.
                </p>
            </div>

            <!-- Pillar 2 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: rgba(56, 189, 248, 0.15); border: 1px solid rgba(56, 189, 248, 0.3); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-blue);">
                    <i data-lucide="video" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">4K/8K 60FPS Media Stream Sniffer</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    Modern video streaming platforms split video and audio into separate encrypted HLS (.m3u8) or MPEG-DASH (.mpd) chunk manifests. EDM’s intelligent sniffer automatically captures both video and audio streams, parallel-downloads all segments, and muxes them into a single high-bitrate MP4 with zero quality loss.
                </p>
            </div>

            <!-- Pillar 3 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: rgba(16, 185, 129, 0.15); border: 1px solid rgba(16, 185, 129, 0.3); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-green);">
                    <i data-lucide="database" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">Crash-Proof SQLite Recovery Journal</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    Every downloaded byte chunk is recorded atomically in an embedded SQLite journal. If your PC loses power or your Wi-Fi disconnects mid-download, EDM never restarts from 0%. Upon reconnecting, it verifies SHA-256 chunk hashes and resumes from the exact millisecond it was interrupted.
                </p>
            </div>

            <!-- Pillar 4 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: rgba(245, 158, 11, 0.15); border: 1px solid rgba(245, 158, 11, 0.3); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-amber);">
                    <i data-lucide="gauge" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">Dynamic Speed Limiter & QoS</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    Maintain ultra-low latency for competitive multiplayer gaming and Zoom conference calls while massive 50 GB game updates download in the background. Toggle the Speed Limiter with a single click to allocate specific bandwidth caps (e.g. 5 MB/s) with zero packet drops.
                </p>
            </div>

            <!-- Pillar 5 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: rgba(236, 72, 153, 0.15); border: 1px solid rgba(236, 72, 153, 0.3); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-pink);">
                    <i data-lucide="clock" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">Midnight Off-Peak Scheduler</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    Automate large download queues during off-peak hours when your ISP provides higher uncapped throughput. EDM automatically starts the queue at your designated time (e.g., 2:00 AM) and can safely close connections and shut down your Windows PC when all downloads finish.
                </p>
            </div>

            <!-- Pillar 6 -->
            <div class="glass-panel" style="padding: 32px 28px; border-radius: 20px; border: 1px solid var(--edm-border);">
                <div style="width: 52px; height: 52px; border-radius: 14px; background: rgba(93, 95, 239, 0.15); border: 1px solid rgba(93, 95, 239, 0.4); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; color: var(--edm-primary-light);">
                    <i data-lucide="shield-check" style="width: 26px; height: 26px;"></i>
                </div>
                <h3 style="font-size: 19px; font-weight: 800; color: #fff; margin-bottom: 10px;">Zero Spyware & Clean Authenticode</h3>
                <p style="font-size: 13.5px; color: var(--edm-text-secondary); line-height: 1.6;">
                    Unlike ad-supported download utilities that inject third-party toolbars or browser trackers, EDM is 100% clean and transparent. Every installer binary is digitally signed with an Authenticode certificate and contains zero adware, telemetry spyware, or hidden bundled installers.
                </p>
            </div>

        </div>

        <!-- Call to Action Banner -->
        <div class="playbook-bottom-banner" style="margin-top: 24px;">
            <div class="banner-left">
                <div class="banner-icon-box">
                    <img src="<?php echo esc_url(get_template_directory_uri() . '/edm-logo.png'); ?>" alt="EDM Logo" style="width: 38px; height: 38px; object-fit: contain;">
                </div>
                <div>
                    <h3 style="font-size: 20px; font-weight: 800; color: #fff;"><?php esc_html_e('Experience 32-Socket Acceleration Today', 'edm-theme'); ?></h3>
                    <p style="font-size: 13px; color: var(--edm-text-secondary); margin-top: 4px;"><?php esc_html_e('Download EDM Setup for Windows 11 & 10. 100% clean 19.8 MB installer with 30-day full turbo trial.', 'edm-theme'); ?></p>
                </div>
            </div>
            <div class="banner-right">
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-lg" download>
                    <i data-lucide="download" style="width: 18px; height: 18px;"></i>
                    <span><?php esc_html_e('Download EDM Setup.exe (19.8 MB)', 'edm-theme'); ?></span>
                </a>
            </div>
        </div>

    </div>
</div>

<?php
get_footer('edm');
