<?php
/**
 * Footer Bottom Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<div class="footer-bottom">
    <div class="container footer-bottom-flex">
        <div class="footer-copy">
            &copy; <?php echo esc_html(gmdate('Y')); ?> <?php esc_html_e('Exclusive Download Manager (EDM). All Rights Reserved.', 'edm-theme'); ?>
        </div>
        <div class="footer-badges">
            <span class="badge-tech"><i data-lucide="code" style="width: 12px; height: 12px;"></i> .NET 10 WPF</span>
            <span class="badge-tech"><i data-lucide="zap" style="width: 12px; height: 12px;"></i> 32x Turbo Socket</span>
            <span class="badge-tech"><i data-lucide="shield" style="width: 12px; height: 12px;"></i> Win32 Durable Engine</span>
        </div>
    </div>
</div>
