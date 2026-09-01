<?php
/**
 * Footer Main Columns Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<div class="footer-top">
    <div class="container">
        <div class="footer-grid">
            <!-- Col 1: Brand & Bio -->
            <div class="footer-col footer-col-brand">
                <a href="<?php echo esc_url(home_url('/')); ?>" class="footer-brand">
                    <div class="brand-logo-box"><i data-lucide="zap" style="width: 18px; height: 18px;"></i></div>
                    <span class="brand-title">EDM</span>
                </a>
                <p class="footer-bio">
                    <?php esc_html_e('Exclusive Download Manager (EDM) is a next-generation high-speed download manager engineered in C# and WPF on .NET 10. Built with 32x turbo socket acceleration, dynamic 4K/8K stream ripping, and resilient crash-proof resume.', 'edm-theme'); ?>
                </p>
                <div class="footer-status-pill">
                    <span class="status-dot"></span>
                    <span><?php esc_html_e('All Systems Operational — v2.1.0 Live', 'edm-theme'); ?></span>
                </div>
            </div>

            <!-- Col 2: Core Product -->
            <div class="footer-col">
                <h4 class="footer-heading"><?php esc_html_e('Product', 'edm-theme'); ?></h4>
                <ul class="footer-links">
                    <li><a href="<?php echo esc_url(home_url('/features/')); ?>"><?php esc_html_e('Engine Features', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/technology/')); ?>"><?php esc_html_e('32x Socket Turbo', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/browser-extension/')); ?>"><?php esc_html_e('Browser Extension', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/download/')); ?>"><?php esc_html_e('Windows Setup (x64/ARM)', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/pricing/')); ?>"><?php esc_html_e('Pricing & Licenses', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/changelog/')); ?>"><?php esc_html_e('Release Notes', 'edm-theme'); ?></a></li>
                </ul>
            </div>

            <!-- Col 3: Technical & Docs -->
            <div class="footer-col">
                <h4 class="footer-heading"><?php esc_html_e('Technology & Specs', 'edm-theme'); ?></h4>
                <ul class="footer-links">
                    <li><a href="<?php echo esc_url(home_url('/system-requirements/')); ?>"><?php esc_html_e('System Requirements', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/screenshots/')); ?>"><?php esc_html_e('UI Screenshots', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/faq/')); ?>"><?php esc_html_e('Technical FAQ', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/support/')); ?>"><?php esc_html_e('Help & Diagnostics', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/about/')); ?>"><?php esc_html_e('About EDM Engineering', 'edm-theme'); ?></a></li>
                </ul>
            </div>

            <!-- Col 4: Trust & Verification -->
            <div class="footer-col">
                <h4 class="footer-heading"><?php esc_html_e('Security & Privacy', 'edm-theme'); ?></h4>
                <ul class="footer-links">
                    <li><a href="<?php echo esc_url(home_url('/privacy/')); ?>"><?php esc_html_e('Privacy Policy', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/terms/')); ?>"><?php esc_html_e('Terms of Service', 'edm-theme'); ?></a></li>
                    <li><a href="<?php echo esc_url(home_url('/download/')); ?>#checksums"><?php esc_html_e('SHA-256 Checksums', 'edm-theme'); ?></a></li>
                </ul>
                <div class="footer-security-badge">
                    <i data-lucide="shield-check" style="width: 18px; height: 18px; color: var(--edm-green);"></i>
                    <span><?php esc_html_e('Zero Telemetry Leakage · Authenticode Signed', 'edm-theme'); ?></span>
                </div>
            </div>
        </div>
    </div>
</div>
