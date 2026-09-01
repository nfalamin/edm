<?php
/**
 * The main template file (Fallback Blog / Archive)
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<section class="section">
    <div class="container">
        <div class="section-header">
            <h1 class="section-title"><?php esc_html_e('Latest Updates & Articles', 'edm-theme'); ?></h1>
            <p class="section-subtitle"><?php esc_html_e('Stay up to date with the latest developments, turbo socket improvements, and releases for EDM.', 'edm-theme'); ?></p>
        </div>

        <div class="blog-grid">
            <?php
            if (have_posts()) :
                while (have_posts()) :
                    the_post();
                    ?>
                    <article id="post-<?php the_ID(); ?>" <?php post_class('blog-card'); ?>>
                        <?php if (has_post_thumbnail()) : ?>
                            <div class="blog-card-thumb">
                                <a href="<?php the_permalink(); ?>">
                                    <?php the_post_thumbnail('medium_large'); ?>
                                </a>
                            </div>
                        <?php endif; ?>
                        <div class="blog-card-body">
                            <div class="blog-meta">
                                <time datetime="<?php echo esc_attr(get_the_date('c')); ?>"><?php echo esc_html(get_the_date()); ?></time>
                                <span>&middot;</span>
                                <span><?php the_author(); ?></span>
                            </div>
                            <h2 class="blog-title">
                                <a href="<?php the_permalink(); ?>"><?php the_title(); ?></a>
                            </h2>
                            <div class="blog-excerpt">
                                <?php the_excerpt(); ?>
                            </div>
                            <a href="<?php the_permalink(); ?>" class="blog-readmore">
                                <span><?php esc_html_e('Read Full Post', 'edm-theme'); ?></span>
                                <i data-lucide="arrow-right" style="width: 14px; height: 14px;"></i>
                            </a>
                        </div>
                    </article>
                    <?php
                endwhile;

                the_posts_pagination(array(
                    'prev_text' => '<i data-lucide="chevron-left"></i>',
                    'next_text' => '<i data-lucide="chevron-right"></i>',
                ));
            else :
                ?>
                <div class="no-posts-card">
                    <p><?php esc_html_e('No posts found. Check back soon for exciting announcements!', 'edm-theme'); ?></p>
                </div>
                <?php
            endif;
            ?>
        </div>
    </div>
</section>

<?php
get_footer();
