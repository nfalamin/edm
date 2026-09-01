<?php
/**
 * Template Name: Privacy Policy Page
 * Description: Template for Privacy Policy & Zero-Telemetry statement.
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
        <?php edm_render_breadcrumbs(esc_html__('Privacy Policy', 'edm-theme')); ?>
        <span class="section-badge"><?php esc_html_e('Privacy & Telemetry', 'edm-theme'); ?></span>
        <h1 class="page-banner-title"><?php esc_html_e('Privacy Policy & Telemetry Notice', 'edm-theme'); ?></h1>
        <p class="page-banner-desc">
            <?php esc_html_e('We strictly protect your privacy. Zero browser history or downloaded payload contents are ever collected.', 'edm-theme'); ?>
        </p>
    </div>
</section>

<section class="section">
    <div class="container container-narrow">
        <div class="legal-doc-card">
            <h2>1. Zero Payload Inspection Policy</h2>
            <p>Exclusive Download Manager operates as a client-side utility on your local Windows system. EDM does not inspect, log, proxy, or relay your downloaded files or URLs to external servers.</p>

            <h2>2. Anonymous Aggregated Telemetry</h2>
            <p>When legal and privacy-conscious analytics are enabled, client IP addresses are immediately masked to /24 subnets (IPv4) or /48 prefixes (IPv6) before any processing. No identifiable personal information is ever logged.</p>

            <h2>3. License & Hardware Verification</h2>
            <p>During Pro license validation, an irreversible cryptographic hash of your system hardware ID (HWID) is checked against our Control Plane to verify activation slot limits without revealing your identity.</p>

            <h2>4. Security & Digital Signatures</h2>
            <p>All installer executables distributed via EDM are digitally signed with Microsoft Authenticode to guarantee file integrity.</p>
        </div>
    </div>
</section>

<?php
get_footer();
