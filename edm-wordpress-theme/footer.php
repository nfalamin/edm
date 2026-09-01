<?php
/**
 * Global Footer Template
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
    </main><!-- #primary -->

    <!-- Global Footer -->
    <footer class="site-footer" id="colophon">
        <?php 
        // 4-Column Footer Links & Bio
        get_template_part('template-parts/footer/footer-main'); 

        // Bottom Copyright & Technical Badges
        get_template_part('template-parts/footer/footer-bottom'); 
        ?>
    </footer>

    <?php 
    // Global Components: Action Modals, Video Modal & Toast Container
    get_template_part('template-parts/components/action-modals');
    get_template_part('template-parts/components/video-modal');
    get_template_part('template-parts/components/toast');
    ?>

</div><!-- #page -->

<?php wp_footer(); ?>
</body>
</html>
