<?php
/**
 * Standalone Dashboard Footer Template (Isolated from Portfolio)
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>

<!-- DASHBOARD ACTION MODALS -->
<?php get_template_part('template-parts/dashboard/modals'); ?>

<!-- Fail-safe Direct Scripts -->
<script src="<?php echo esc_url(get_template_directory_uri() . '/assets/js/mock-data.js'); ?>"></script>
<script src="<?php echo esc_url(get_template_directory_uri() . '/assets/js/dashboard-app.js'); ?>"></script>

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
