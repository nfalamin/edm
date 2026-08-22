<?php
/**
 * Template Name: Support Page
 * Description: Template for Help, Bug Reporting & Diagnostics.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<!-- Subpage Banner -->
<section class="page-banner">
    <div class="hero-glow-bg"></div>
    <div class="container">
        <?php edm_render_breadcrumbs(esc_html__('Support Center', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Customer Support', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Help & Technical Diagnostics', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Need assistance with installation, license activation, or browser extension attachment? Our engineering team is here to help.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container">
        <div class="features-grid-3">
            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #6366F1 0%, #4F46E5 100%);"><i data-lucide="mail"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Email VIP Support', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Direct ticket line for license recovery, billing queries, and priority troubleshooting.', 'edm-theme'); ?></p>
                </div>
                <a href="mailto:support@edm-download.org" class="btn btn-outline btn-sm" style="margin-top: 14px;"><?php esc_html_e('support@edm-download.org', 'edm-theme'); ?></a>
            </div>

            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #10B981 0%, #059669 100%);"><i data-lucide="terminal"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Diagnostic Logs', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Generate anonymized engine diagnostics from desktop menu: Help → Export Diagnostic Log.', 'edm-theme'); ?></p>
                </div>
                <span class="feature-tech-tag">DiagnosticsExporter.cs</span>
            </div>

            <div class="feature-card">
                <div>
                    <div class="feature-icon-box" style="background: linear-gradient(135deg, #38BDF8 0%, #0284C7 100%);"><i data-lucide="book-open"></i></div>
                    <h3 class="feature-card-title"><?php esc_html_e('Knowledge Base & FAQ', 'edm-theme'); ?></h3>
                    <p class="feature-card-desc"><?php esc_html_e('Explore comprehensive documentation regarding socket tuning and proxy setup.', 'edm-theme'); ?></p>
                </div>
                <a href="<?php echo esc_url(home_url('/faq/')); ?>" class="btn btn-outline btn-sm" style="margin-top: 14px;"><?php esc_html_e('Browse FAQ', 'edm-theme'); ?></a>
            </div>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
