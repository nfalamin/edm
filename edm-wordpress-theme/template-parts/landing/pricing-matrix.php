<?php
/**
 * Landing Page: Pricing Matrix Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section" id="pricing">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Transparent Licensing', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Simple, Honest Pricing. Zero Hidden Fees.', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Start free with our fully featured 14-day trial or unlock Lifetime Turbo with priority updates.', 'edm-theme'); ?>
            </p>

            <!-- Pricing Period Toggle -->
            <div class="pricing-toggle-wrap">
                <span class="toggle-label"><?php esc_html_e('Monthly', 'edm-theme'); ?></span>
                <button class="toggle-switch" id="pricing-period-toggle" onclick="if(window.edmSite) window.edmSite.togglePricingPeriod();" aria-label="<?php esc_attr_e('Toggle Yearly/Monthly Billing', 'edm-theme'); ?>">
                    <span class="toggle-switch-slider active"></span>
                </button>
                <span class="toggle-label active">
                    <?php esc_html_e('Yearly / Lifetime', 'edm-theme'); ?> 
                    <span class="toggle-discount-badge"><?php esc_html_e('Save 40%', 'edm-theme'); ?></span>
                </span>
            </div>
        </div>

        <div class="pricing-cards-grid">
            <!-- 1. Free Trial Plan -->
            <div class="pricing-card">
                <div class="pricing-card-header">
                    <span class="plan-type"><?php esc_html_e('COMMUNITY', 'edm-theme'); ?></span>
                    <h3 class="plan-name"><?php esc_html_e('Free Trial', 'edm-theme'); ?></h3>
                    <div class="plan-price-wrap">
                        <span class="plan-currency" id="price-cur-free">$</span>
                        <span class="plan-price-num" id="price-val-free">0</span>
                        <span class="plan-duration">/ 30 <?php esc_html_e('Days', 'edm-theme'); ?></span>
                    </div>
                    <p class="plan-desc"><?php esc_html_e('Complete access to all 32-socket turbo features for 30 days. No credit card required.', 'edm-theme'); ?></p>
                </div>

                <ul class="plan-features">
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Up to 32 Socket Multi-Threading', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('4K Video Stream Ripper', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Browser Extensions (Chrome, Edge)', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Single PC Activation', 'edm-theme'); ?></li>
                </ul>

                <div class="plan-cta">
                    <a href="<?php echo esc_url(edm_get_download_url()); ?>" class="btn btn-outline btn-full" download>
                        <?php esc_html_e('Download Free Trial', 'edm-theme'); ?>
                    </a>
                </div>
            </div>

            <!-- 2. Pro Turbo Plan (Featured) -->
            <div class="pricing-card pricing-card-featured">
                <div class="featured-ribbon"><?php esc_html_e('MOST POPULAR', 'edm-theme'); ?></div>
                <div class="pricing-card-header">
                    <span class="plan-type"><?php esc_html_e('PRO TURBO', 'edm-theme'); ?></span>
                    <h3 class="plan-name"><?php esc_html_e('Annual License', 'edm-theme'); ?></h3>
                    <div class="plan-price-wrap">
                        <span class="plan-currency" id="price-cur-pro">৳</span>
                        <span class="plan-price-num" id="price-val-pro">1,200</span>
                        <span class="plan-duration">/ <?php esc_html_e('Year', 'edm-theme'); ?></span>
                    </div>
                    <p class="plan-desc"><?php esc_html_e('Unlimited 32x acceleration, all future v2.x updates, and dedicated VIP support.', 'edm-theme'); ?></p>
                </div>

                <ul class="plan-features">
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Full 32 Socket Turbo Acceleration', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('4K & 8K Video Ripper + MP3 Extraction', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Multi-PC License (3 Windows Devices)', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Cloud Backup of Download Queues', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Priority 24/7 VIP Engineering Support', 'edm-theme'); ?></li>
                </ul>

                <div class="plan-cta">
                    <a href="<?php echo esc_url(home_url('/pricing/')); ?>" class="btn btn-primary btn-full">
                        <?php esc_html_e('Upgrade to Pro Turbo', 'edm-theme'); ?>
                    </a>
                </div>
            </div>

            <!-- 3. Lifetime VIP Plan -->
            <div class="pricing-card">
                <div class="pricing-card-header">
                    <span class="plan-type"><?php esc_html_e('ENTERPRISE / LIFETIME', 'edm-theme'); ?></span>
                    <h3 class="plan-name"><?php esc_html_e('Lifetime Pass', 'edm-theme'); ?></h3>
                    <div class="plan-price-wrap">
                        <span class="plan-currency" id="price-cur-life">৳</span>
                        <span class="plan-price-num" id="price-val-life">2,800</span>
                        <span class="plan-duration">/ <?php esc_html_e('One-Time', 'edm-theme'); ?></span>
                    </div>
                    <p class="plan-desc"><?php esc_html_e('Pay once, own forever. Includes all future major version upgrades (v3, v4).', 'edm-theme'); ?></p>
                </div>

                <ul class="plan-features">
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Lifetime Major Version Upgrades', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('5 PC Activations + Hardware Transfer', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Beta Access to Turbo Kernel Innovations', 'edm-theme'); ?></li>
                    <li><i data-lucide="check" style="color: var(--edm-green);"></i> <?php esc_html_e('Custom Proxy & SOCKS5 Routing Suite', 'edm-theme'); ?></li>
                </ul>

                <div class="plan-cta">
                    <a href="<?php echo esc_url(home_url('/pricing/')); ?>" class="btn btn-outline btn-full">
                        <?php esc_html_e('Get Lifetime Pass', 'edm-theme'); ?>
                    </a>
                </div>
            </div>
        </div>
    </div>
</section>
