<?php
/**
 * The template for displaying single blog posts
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<section class="section">
    <div class="container container-narrow">
        <?php
        while (have_posts()) :
            the_post();
            ?>
            <article id="post-<?php the_ID(); ?>" <?php post_class('single-post-card'); ?>>
                <header class="single-header">
                    <div class="single-meta">
                        <time datetime="<?php echo esc_attr(get_the_date('c')); ?>"><?php echo esc_html(get_the_date()); ?></time>
                        <span>&middot;</span>
                        <span><?php the_author(); ?></span>
                    </div>
                    <h1 class="single-title"><?php the_title(); ?></h1>
                </header>

                <?php if (has_post_thumbnail()) : ?>
                    <div class="single-featured-image">
                        <?php the_post_thumbnail('large'); ?>
                    </div>
                <?php endif; ?>

                <div class="single-content">
                    <?php the_content(); ?>
                </div>

                <footer class="single-footer">
                    <?php the_tags('<div class="post-tags"><span class="tags-label">' . esc_html__('Tags:', 'edm-theme') . '</span> ', ', ', '</div>'); ?>
                </footer>
            </article>

            <?php
            // If comments are open or we have at least one comment, load up the comment template.
            if (comments_open() || get_comments_number()) :
                comments_template();
            endif;

        endwhile;
        ?>
    </div>
</section>

<?php
get_footer();
