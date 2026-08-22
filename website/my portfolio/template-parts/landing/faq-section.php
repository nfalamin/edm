<?php
/**
 * Landing Page: FAQ Accordion Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<section class="section section-darker" id="faq">
    <div class="container">
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Frequently Asked Questions', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('Everything You Need to Know About EDM', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Clear, transparent answers about 32-socket acceleration, browser extensions, licensing, and security.', 'edm-theme'); ?>
            </p>
        </div>

        <div class="faq-accordion-wrap">
            
            <div class="faq-item active" onclick="if(window.edmSite) window.edmSite.toggleFaq(this);">
                <div class="faq-question">
                    <h3><?php esc_html_e('How does EDM achieve up to 32x faster download speeds?', 'edm-theme'); ?></h3>
                    <div class="faq-chevron"><i data-lucide="chevron-down"></i></div>
                </div>
                <div class="faq-answer">
                    <p><?php esc_html_e('Standard browsers download files through a single TCP connection, often restricted by server-side per-connection QoS limits and latency spikes. EDM dynamically slices files into 32 simultaneous HTTP range segments across independent TLS sockets. Each socket receives its chunk concurrently and writes directly into sparse disk storage, saturating your total bandwidth throughput.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="faq-item" onclick="if(window.edmSite) window.edmSite.toggleFaq(this);">
                <div class="faq-question">
                    <h3><?php esc_html_e('Can EDM resume broken or interrupted downloads after a crash?', 'edm-theme'); ?></h3>
                    <div class="faq-chevron"><i data-lucide="chevron-down"></i></div>
                </div>
                <div class="faq-answer">
                    <p><?php esc_html_e('Yes. EDM utilizes a crash-proof Durable Metadata Manager with Win32 FlushFileBuffers synchronization. If your internet disconnects, power fails, or Windows restarts, EDM preserves byte-exact offsets in its atomic state journal. When reopened, it seamlessly resumes without re-downloading existing chunks.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="faq-item" onclick="if(window.edmSite) window.edmSite.toggleFaq(this);">
                <div class="faq-question">
                    <h3><?php esc_html_e('How do the browser extensions work with Chrome, Edge, and Firefox?', 'edm-theme'); ?></h3>
                    <div class="faq-chevron"><i data-lucide="chevron-down"></i></div>
                </div>
                <div class="faq-answer">
                    <p><?php esc_html_e('EDM integrates via official Chromium Manifest V3 and Mozilla WebExtensions protocols communicating over native Windows Standard I/O (Native Messaging). When you click a download link or stream a video, the extension captures the request along with your active session cookies, user-agent, and referrers, handing it off to the desktop engine with zero delay.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="faq-item" onclick="if(window.edmSite) window.edmSite.toggleFaq(this);">
                <div class="faq-question">
                    <h3><?php esc_html_e('Is EDM clean from malware, telemetry trackers, and bundleware?', 'edm-theme'); ?></h3>
                    <div class="faq-chevron"><i data-lucide="chevron-down"></i></div>
                </div>
                <div class="faq-answer">
                    <p><?php esc_html_e('100% clean and privacy-focused. EDM is digitally signed with an Authenticode certificate and automatically scanned against Windows Defender. We do not bundle third-party search bars, adware, or invasive tracking scripts. Your downloads remain private on your computer.', 'edm-theme'); ?></p>
                </div>
            </div>

            <div class="faq-item" onclick="if(window.edmSite) window.edmSite.toggleFaq(this);">
                <div class="faq-question">
                    <h3><?php esc_html_e('What happens after the 30-day free trial expires?', 'edm-theme'); ?></h3>
                    <div class="faq-chevron"><i data-lucide="chevron-down"></i></div>
                </div>
                <div class="faq-answer">
                    <p><?php esc_html_e('During the 30-day trial, you have unrestricted access to all 32-socket acceleration features and 4K stream rippers. When the trial completes, you can upgrade to Pro Lifetime with a single-payment license key or continue using basic single-stream download features.', 'edm-theme'); ?></p>
                </div>
            </div>

        </div>
    </div>
</section>
