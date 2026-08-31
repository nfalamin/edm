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
<script src="<?php echo esc_url(get_template_directory_uri() . '/assets/js/landing-app.js'); ?>"></script>
<script src="<?php echo esc_url(get_template_directory_uri() . '/assets/js/obhijog.js'); ?>"></script>
<script src="<?php echo esc_url(get_template_directory_uri() . '/assets/js/edm-support-bot.js'); ?>"></script>

<script>
    document.addEventListener('DOMContentLoaded', function() {
        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
        }
    });
</script>

<?php wp_footer(); ?>
</body>
</html>
