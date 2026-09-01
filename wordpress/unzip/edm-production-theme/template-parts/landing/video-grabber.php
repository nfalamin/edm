<?php
/**
 * Landing Page: Video Grabber Showcase Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section" id="video-grabber">
    <div class="container">
        <div class="split-feature-layout">
            <!-- Left Text -->
            <div class="split-feature-text">
                <span class="section-badge"><?php esc_html_e('Next-Gen Media Sniffer', 'edm-theme'); ?></span>
                <h2 class="section-title" style="text-align: left;"><?php esc_html_e('Grab 4K & 8K Video Streams with Zero Quality Loss', 'edm-theme'); ?></h2>
                <p class="section-subtitle" style="text-align: left; margin: 0 0 24px 0;">
                    <?php esc_html_e('EDM automatically detects streaming video manifests (M3U8, DASH, MP4, WebM) on web pages. It presents a zero-lag overlay button, allowing you to select bitrate, resolution, or extract pure MP3 audio.', 'edm-theme'); ?>
                </p>

                <div class="feature-checklist">
                    <div class="check-item">
                        <div class="check-icon"><i data-lucide="check"></i></div>
                        <div>
                            <strong><?php esc_html_e('Multi-Stream FFmpeg Audio/Video Merger', 'edm-theme'); ?></strong>
                            <p><?php esc_html_e('Downloads separated 4K video and lossless audio tracks simultaneously, combining them into clean MP4 or MKV without re-encoding lag.', 'edm-theme'); ?></p>
                        </div>
                    </div>
                    <div class="check-item">
                        <div class="check-icon"><i data-lucide="check"></i></div>
                        <div>
                            <strong><?php esc_html_e('Support for 1,000+ Video Portals & Streams', 'edm-theme'); ?></strong>
                            <p><?php esc_html_e('Full support for YouTube, Vimeo, Facebook, TikTok, Twitch VODs, and generic HLS/DASH streaming servers.', 'edm-theme'); ?></p>
                        </div>
                    </div>
                </div>

                <div style="margin-top: 32px;">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmSite) window.edmSite.openVideoModal();">
                        <i data-lucide="play" style="width: 16px; height: 16px;"></i>
                        <span><?php esc_html_e('See Video Grabber in Action', 'edm-theme'); ?></span>
                    </button>
                </div>
            </div>

            <!-- Right Showcase Graphic -->
            <div class="split-feature-graphic">
                <div class="video-grabber-mockup">
                    <div class="mockup-top-bar">
                        <span class="mockup-dot red"></span>
                        <span class="mockup-dot yellow"></span>
                        <span class="mockup-dot green"></span>
                        <span class="mockup-url-bar">https://stream-portal.com/watch?v=4k_demo</span>
                    </div>
                    <div class="mockup-video-canvas">
                        <div class="mockup-overlay-badge">
                            <i data-lucide="download-cloud" style="width: 14px; height: 14px;"></i>
                            <span><?php esc_html_e('EDM: 7 Streams Found', 'edm-theme'); ?></span>
                        </div>
                        <div class="mockup-dropdown-preview">
                            <div class="stream-opt active">
                                <span class="opt-res">4K (2160p60)</span>
                                <span class="opt-size">3.8 GB · MP4</span>
                                <span class="opt-tag">HDR Lossless</span>
                            </div>
                            <div class="stream-opt">
                                <span class="opt-res">1440p (2K)</span>
                                <span class="opt-size">1.9 GB · MP4</span>
                                <span class="opt-tag">60fps</span>
                            </div>
                            <div class="stream-opt">
                                <span class="opt-res">1080p (FHD)</span>
                                <span class="opt-size">850 MB · MP4</span>
                                <span class="opt-tag">Standard</span>
                            </div>
                            <div class="stream-opt">
                                <span class="opt-res">Audio Only</span>
                                <span class="opt-size">45 MB · MP3</span>
                                <span class="opt-tag">320kbps</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</section>
