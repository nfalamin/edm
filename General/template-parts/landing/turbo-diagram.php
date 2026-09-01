<?php
/**
 * Landing Page: 32x Turbo Technology Visualizer Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section section-darker" id="technology">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Proprietary Socket Engine', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('How 32-Socket Dynamic Range Multi-Threading Works', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Unlike standard single-stream browser downloads that suffer from TCP window bottlenecks, EDM divides files into 32 byte-exact ranges over concurrent TLS socket connections.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="turbo-comparison-grid">
            <!-- Legacy Single Stream Card -->
            <div class="comparison-card legacy-card">
                <div class="comp-header">
                    <span class="comp-badge comp-badge-legacy"><?php esc_html_e('STANDARD BROWSER / LEGACY', 'edm-theme'); ?></span>
                    <h3><?php esc_html_e('Single TCP Stream (1x)', 'edm-theme'); ?></h3>
                </div>
                <div class="comp-visual">
                    <div class="stream-line-single">
                        <div class="stream-pulse"></div>
                    </div>
                </div>
                <ul class="comp-list">
                    <li><i data-lucide="x" style="color: var(--edm-danger);"></i> <?php esc_html_e('Restricted by server-side per-connection QoS bandwidth limits', 'edm-theme'); ?></li>
                    <li><i data-lucide="x" style="color: var(--edm-danger);"></i> <?php esc_html_e('Single packet drop causes TCP Congestion Window collapse', 'edm-theme'); ?></li>
                    <li><i data-lucide="x" style="color: var(--edm-danger);"></i> <?php esc_html_e('Total download fails if connection disconnects at 99%', 'edm-theme'); ?></li>
                </ul>
                <div class="comp-speed-result legacy-speed">
                    <span><?php esc_html_e('Average Speed: 4.2 MB/s', 'edm-theme'); ?></span>
                </div>
            </div>

            <!-- EDM 32-Socket Turbo Card -->
            <div class="comparison-card edm-turbo-card">
                <div class="comp-header">
                    <span class="comp-badge comp-badge-turbo"><?php esc_html_e('EDM TURBO ENGINE', 'edm-theme'); ?></span>
                    <h3><?php esc_html_e('32 Concurrent Range Slices (32x)', 'edm-theme'); ?></h3>
                </div>
                <div class="comp-visual">
                    <div class="stream-lines-multi">
                        <?php for ($i = 0; $i < 8; $i++): ?>
                            <div class="stream-slice-line"><div class="slice-pulse" style="animation-delay: <?php echo esc_attr($i * 0.15); ?>s;"></div></div>
                        <?php endfor; ?>
                    </div>
                </div>
                <ul class="comp-list">
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('32 independent socket connections bypass per-IP throttling', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Dynamic buffer stitching into pre-allocated sparse disk storage', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Atomic byte offsets saved continuously for seamless instant resume', 'edm-theme'); ?></li>
                </ul>
                <div class="comp-speed-result turbo-speed">
                    <span><?php esc_html_e('Average Speed: 134.5 MB/s (Line Saturation)', 'edm-theme'); ?></span>
                </div>
            </div>
        </div>

        <div class="section-footer-cta">
            <a href="<?php echo esc_url(home_url('/technology/')); ?>" class="btn btn-primary">
                <span><?php esc_html_e('Read Technical Architecture Whitepaper', 'edm-theme'); ?></span>
                <i data-lucide="cpu" style="width: 16px; height: 16px;"></i>
            </a>
        </div>
    </div>
</section>
