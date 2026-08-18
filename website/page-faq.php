<?php
/**
 * Template Name: FAQ Page
 * Description: Template for Frequently Asked Questions.
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
        <?php edm_render_breadcrumbs(esc_html__('FAQ', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Help & Answers', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Frequently Asked Questions', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('Answers to common inquiries regarding 32x acceleration, licensing, video ripping, and browser integration.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container container-narrow">
        <div class="faq-accordion-list">
            <details class="faq-item" open>
                <summary class="faq-question">
                    <span><?php esc_html_e('How does EDM achieve up to 32x faster downloads?', 'edm-theme'); ?></span>
                    <i data-lucide="chevron-down"></i>
                </summary>
                <div class="faq-answer">
                    <p><?php esc_html_e('Standard browsers use a single TCP connection that is easily throttled by web servers and packet drops. EDM negotiates byte-range chunks using up to 32 parallel TLS sockets simultaneously, saturating your internet bandwidth to its physical limit.', 'edm-theme'); ?></p>
                </div>
            </details>

            <details class="faq-item">
                <summary class="faq-question">
                    <span><?php esc_html_e('Does EDM work with YouTube, Vimeo, and video streaming sites?', 'edm-theme'); ?></span>
                    <i data-lucide="chevron-down"></i>
                </summary>
                <div class="faq-answer">
                    <p><?php esc_html_e('Yes. The EDM browser extension includes a dynamic video candidate detector that sniffs M3U8, DASH, and direct MP4 streams. It captures multi-bitrate streams and combines audio/video channels using embedded FFmpeg.', 'edm-theme'); ?></p>
                </div>
            </details>

            <details class="faq-item">
                <summary class="faq-question">
                    <span><?php esc_html_e('What happens if my PC suddenly reboots or loses power during a download?', 'edm-theme'); ?></span>
                    <i data-lucide="chevron-down"></i>
                </summary>
                <div class="faq-answer">
                    <p><?php esc_html_e('EDM uses durable atomic state flush via Win32 OS buffers. Unfinished byte segments are safely truncated on relaunch, allowing you to resume exactly where the transfer was interrupted without downloading duplicate data.', 'edm-theme'); ?></p>
                </div>
            </details>

            <details class="faq-item">
                <summary class="faq-question">
                    <span><?php esc_html_e('Can I transfer my Pro license to another computer?', 'edm-theme'); ?></span>
                    <i data-lucide="chevron-down"></i>
                </summary>
                <div class="faq-answer">
                    <p><?php esc_html_e('Yes. You can deactivate an old PC from the Settings menu in the EDM desktop app or manage active HWID hardware tokens directly via the SaaS Dashboard.', 'edm-theme'); ?></p>
                </div>
            </details>
        </div>
    </div>
</section>

<!-- Download CTA -->
<?php get_template_part('template-parts/landing/download-cta'); ?>

<?php
get_footer();
