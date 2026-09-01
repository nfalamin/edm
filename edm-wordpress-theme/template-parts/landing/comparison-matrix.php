<?php
/**
 * Landing Page: Comprehensive Comparison Matrix (EDM vs IDM vs Native Browsers)
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section" id="comparison">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Industry Benchmark', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Why EDM Outperforms Traditional Download Tools', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Compare architectural specifications, multi-threading limits, browser integration, and pricing models.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="comparison-table-wrap glass-panel-table">
            <table class="comparison-table">
                <thead>
                    <tr>
                        <th class="feature-col"><?php esc_html_e('Core Capabilities', 'edm-theme'); ?></th>
                        <th class="highlight-col">
                            <div class="table-brand-pill">
                                <span>⚡ EDM Turbo v2.1</span>
                                <span class="table-tag-recommended"><?php esc_html_e('RECOMMENDED', 'edm-theme'); ?></span>
                            </div>
                        </th>
                        <th><?php esc_html_e('Legacy Download Tools', 'edm-theme'); ?></th>
                        <th><?php esc_html_e('Standard Browser', 'edm-theme'); ?></th>
                        <th><?php esc_html_e('Generic FDM', 'edm-theme'); ?></th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Concurrent Sockets', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('Parallel HTTP range chunks per download stream', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><span class="badge-success">32 Sockets (Dynamic)</span></td>
                        <td>32 Sockets (Fixed)</td>
                        <td><span class="badge-fail">1 Socket (Single-thread)</span></td>
                        <td>10 Sockets</td>
                    </tr>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Video Sniffer & 4K/8K Ripper', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('Automatic multi-segment audio/video stream stitcher', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><i data-lucide="check-circle" class="icon-check"></i> <?php esc_html_e('Native 4K/8K + Audio Mux', 'edm-theme'); ?></td>
                        <td><i data-lucide="check-circle" class="icon-check"></i> <?php esc_html_e('Supported (Older UI)', 'edm-theme'); ?></td>
                        <td><i data-lucide="x-circle" class="icon-cross"></i> <?php esc_html_e('No Ripper', 'edm-theme'); ?></td>
                        <td><i data-lucide="minus-circle" class="icon-partial"></i> <?php esc_html_e('Basic MP4 Only', 'edm-theme'); ?></td>
                    </tr>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Browser Integration', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('Chrome, Edge Chromium & Firefox Native Messaging', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><span class="badge-success">Manifest V3 Certified</span></td>
                        <td>Manifest V2 / Legacy</td>
                        <td>Built-in</td>
                        <td>Manifest V2</td>
                    </tr>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Architecture & Performance', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('Memory footprint and native CPU usage during 1 Gbps bursts', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><span class="badge-success">&lt; 1.2% CPU (x64 / ARM64)</span></td>
                        <td>3.5% CPU (x86 Emulation)</td>
                        <td>High RAM Overhead</td>
                        <td>4.8% CPU Overhead</td>
                    </tr>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Crash-Proof Resume', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('SQLite persistent transaction journal for interrupted streams', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><i data-lucide="check-circle" class="icon-check"></i> <?php esc_html_e('Zero Corrupt Resume', 'edm-theme'); ?></td>
                        <td><i data-lucide="check-circle" class="icon-check"></i> <?php esc_html_e('Standard Resume', 'edm-theme'); ?></td>
                        <td><i data-lucide="x-circle" class="icon-cross"></i> <?php esc_html_e('Restarts on Crash', 'edm-theme'); ?></td>
                        <td><i data-lucide="minus-circle" class="icon-partial"></i> <?php esc_html_e('Partial Resume', 'edm-theme'); ?></td>
                    </tr>
                    <tr>
                        <td class="feature-title">
                            <strong><?php esc_html_e('Licensing & Pricing', 'edm-theme'); ?></strong>
                            <span><?php esc_html_e('Fair, transparent ownership with zero yearly subscription traps', 'edm-theme'); ?></span>
                        </td>
                        <td class="highlight-cell"><span class="badge-highlight"><?php esc_html_e('Lifetime (৳1,499) / 30-Day Free', 'edm-theme'); ?></span></td>
                        <td>$24.95 / Year (Rental)</td>
                        <td>Free (Slow)</td>
                        <td>Free (Ad-Supported)</td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>
</section>
