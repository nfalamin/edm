<?php
/**
 * EDM Dedicated Footer Template
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
    </main><!-- #primary -->

    <!-- Global EDM Footer -->
    <footer class="site-footer" id="colophon">
        <?php 
        // 4-Column Footer Links & Bio
        get_template_part('template-parts/footer/footer-main'); 

        // Bottom Copyright & Technical Badges
        get_template_part('template-parts/footer/footer-bottom'); 
        ?>
    </footer>

    <?php 
    // Global EDM Components: Action Modals, Video Modal, Toast Container, Obhijog Modal & AI Bot
    get_template_part('template-parts/components/action-modals');
    get_template_part('template-parts/components/video-modal');
    get_template_part('template-parts/components/toast');
    get_template_part('template-parts/components/obhijog-modal');
    get_template_part('template-parts/components/edm-support-bot');
    ?>

</div><!-- #page -->

<!-- Fail-safe Direct Scripts -->
<script src="<?php echo esc_url(get_template_directory_uri() . '/Assets/js/landing-app.js'); ?>?ver=2.1.0"></script>
<script src="<?php echo esc_url(get_template_directory_uri() . '/Assets/js/obhijog.js'); ?>?ver=2.1.0"></script>
<script src="<?php echo esc_url(get_template_directory_uri() . '/Assets/js/edm-support-bot.js'); ?>?ver=2.1.0"></script>

<!-- Embedded Fail-Safe Core Handlers (Mobile Menu, Buttons, Simulator) -->
<script>
    // 1. Mobile Menu Drawer Toggle
    window.toggleMobileMenu = function() {
        var drawer = document.getElementById('mobile-drawer');
        if (drawer) {
            drawer.classList.toggle('active');
            if (drawer.classList.contains('active')) {
                drawer.style.display = 'block';
                drawer.style.transform = 'translateY(0)';
            } else {
                drawer.style.display = 'none';
            }
        }
    };

    // 2. Feedback / Obhijog Modal Handlers
    window.openObhijogModal = function() {
        var m = document.getElementById('modal-obhijog-center') || document.getElementById('modal-obhijog');
        if (m) {
            m.style.display = 'flex';
            m.classList.add('active');
            if (window.lucide) window.lucide.createIcons();
        }
    };

    window.closeObhijogModal = function() {
        var m = document.getElementById('modal-obhijog-center') || document.getElementById('modal-obhijog');
        if (m) {
            m.classList.remove('active');
            setTimeout(function() { m.style.display = 'none'; }, 200);
        }
    };

    // 3. Fallback edmSite Object for Interactive Buttons (Bulletproof Button Protection)
    if (!window.edmSite) {
        window.edmSite = {
            toggleMobileMenu: window.toggleMobileMenu,
            toggleTheme: function() {
                document.body.classList.toggle('light-theme');
                localStorage.setItem('edm_theme', document.body.classList.contains('light-theme') ? 'light' : 'dark');
            },
            handleSniffUrl: function() {
                var input = document.getElementById('url-sniffer-input');
                var val = input ? input.value.trim() : '';
                if (!val) {
                    alert('Please paste a download URL or video link to sniff.');
                    return;
                }
                var speedEl = document.getElementById('sim-download-speed');
                if (speedEl) speedEl.textContent = '48.6 MB/s (32 Sockets)';
                alert('⚡ Sniffed URL successfully! 32 Sockets allocated. Starting accelerated stream.');
            },
            toggleSimPause: function() {
                var btn = document.getElementById('btn-sim-pause');
                if (btn) btn.textContent = btn.textContent.includes('Pause') ? '▶ Resume' : '⏸ Pause';
            },
            boostTurbo: function() {
                var speedEl = document.getElementById('sim-download-speed');
                if (speedEl) speedEl.textContent = '96.4 MB/s (32 Sockets - TURBO BOOST)';
            },
            togglePricingPeriod: function() {
                var btn = document.getElementById('pricing-period-toggle');
                if (btn) btn.classList.toggle('monthly');
            },
            toggleFaq: function(el) {
                if (el) el.classList.toggle('active');
            },
            openVideoModal: function() {
                var m = document.getElementById('modal-video-demo');
                if (m) { m.style.display = 'flex'; m.classList.add('active'); }
            },
            closeVideoModal: function() {
                var m = document.getElementById('modal-video-demo');
                if (m) { m.classList.remove('active'); m.style.display = 'none'; }
            },
            closeModal: function(id) {
                var m = document.getElementById(id);
                if (m) { m.classList.remove('active'); m.style.display = 'none'; }
            },
            showToast: function(msg, type) {
                alert(msg);
            }
        };
    }

    document.addEventListener('DOMContentLoaded', function() {
        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
        }
    });
</script>

<?php wp_footer(); ?>
</body>
</html>
