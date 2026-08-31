<?php
/**
 * The main template file
 * 
 * Displays posts for blog, archives, search results, and other template views.
 * For the homepage, front-page.php is used instead.
 * 
 * Template hierarchy: front-page.php → home.php → index.php
 * 
 * @package Portfolio_Theme
 * @since 1.0
 */

get_header(); ?>

<main>

    <!-- Blog/Archive Section -->
    <section class="py-24 px-6 bg-navy-950 min-h-screen">
        <div class="max-w-4xl mx-auto">
                        
            <!-- Page Title & Description -->
            <div class="mb-16 reveal">
                <h1 class="text-4xl md:text-5xl font-extrabold text-white font-display mb-4">
                    <?php 
                        if ( is_search() ) {
                            printf( __( 'Search Results for: %s', 'portfolio' ), get_search_query() );
                        } elseif ( is_category() ) {
                            single_cat_title();
                        } elseif ( is_tag() ) {
                            single_tag_title();
                        } elseif ( is_author() ) {
                            the_post();
                            printf( __( 'Posts by: %s', 'portfolio' ), get_the_author() );
                            rewind_posts();
                        } else {
                            _e( 'Blog & Articles', 'portfolio' );
                        }
                    ?>
                </h1>
                <p class="text-slate-400 text-base md:text-lg">
                    <?php 
                        if ( is_search() ) {
                            printf( __( '%d search result(s) found', 'portfolio' ), $wp_query->found_posts );
                        } else {
                            _e( 'Latest insights, case studies, and industry updates', 'portfolio' );
                        }
                    ?>
                </p>
            </div>

            <!-- Posts Loop -->
            <?php
            if ( have_posts() ) :
                while ( have_posts() ) :
                    the_post();
                    ?>
                    <article id="post-<?php the_ID(); ?>" <?php post_class( 'mb-8 glass-panel p-6 md:p-8 rounded-2xl border border-white/10 reveal' ); ?>>
                        
                        <!-- Featured Image -->
                        <?php if ( has_post_thumbnail() ) : ?>
                            <div class="mb-6 rounded-xl overflow-hidden max-h-96">
                                <?php the_post_thumbnail( 'large', array( 'class' => 'w-full h-auto object-cover', 'loading' => 'lazy' ) ); ?>
                            </div>
                        <?php endif; ?>

                        <!-- Post Header -->
                        <div class="mb-4">
                            <div class="flex flex-wrap gap-2 mb-3">
                                <?php 
                                    $categories = get_the_category();
                                    if ( ! empty( $categories ) ) :
                                        foreach ( $categories as $category ) :
                                            ?>
                                            <a href="<?php echo esc_url( get_category_link( $category->term_id ) ); ?>" class="text-xs bg-blue-600/20 border border-blue-500/30 text-blue-400 tracking-wider uppercase font-bold px-3 py-1 rounded-full hover:bg-blue-600/40 transition-colors">
                                                <?php echo esc_html( $category->name ); ?>
                                            </a>
                                            <?php
                                        endforeach;
                                    endif;
                                    ?>
                            </div>

                            <h2 class="text-2xl md:text-3xl font-bold text-white font-display mb-2 hover:text-cyan transition-colors">
                                <a href="<?php the_permalink(); ?>">
                                    <?php the_title(); ?>
                                </a>
                            </h2>

                            <div class="flex flex-wrap items-center gap-4 text-slate-400 text-sm">
                                <span><i class="fa-regular fa-calendar mr-2"></i><?php echo get_the_date(); ?></span>
                                <span><i class="fa-solid fa-user-pen mr-2"></i><?php the_author(); ?></span>
                                <span><i class="fa-solid fa-clock mr-2"></i><?php echo max( 1, (int) ceil( str_word_count( wp_strip_all_tags( get_the_content() ) ) / 200 ) ); ?> <?php _e( 'min read', 'portfolio' ); ?></span>
                            </div>
                        </div>

                        <!-- Post Excerpt -->
                        <div class="text-slate-300 text-sm md:text-base leading-relaxed mb-6">
                            <?php the_excerpt(); ?>
                        </div>

                        <!-- Read More Link -->
                        <a href="<?php the_permalink(); ?>" class="btn-premium btn-premium-primary !px-6 !py-3 !text-[10px]">
                            <?php _e( 'Read Article', 'portfolio' ); ?> <i class="fa-solid fa-arrow-right ml-2"></i>
                        </a>
                    </article>
                    <?php
                endwhile;

                // Pagination
                ?>
                <div class="mt-16 flex items-center justify-center gap-2">
                    <?php
                    echo paginate_links( array(
                        'type' => 'list',
                        'prev_text' => '<i class="fa-solid fa-chevron-left"></i>',
                        'next_text' => '<i class="fa-solid fa-chevron-right"></i>',
                    ) );
                    ?>
                </div>
                <?php
            else :
                // No posts found
                ?>
                <div class="glass-panel p-8 rounded-2xl border border-white/10 text-center reveal">
                    <h2 class="text-2xl font-bold text-white font-display mb-4">
                        <?php _e( 'Nothing Found', 'portfolio' ); ?>
                    </h2>
                    <p class="text-slate-400 mb-6">
                        <?php _e( 'Sorry, no posts match your criteria. Try adjusting your search or browsing other posts.', 'portfolio' ); ?>
                    </p>
                    <a href="<?php echo esc_url( home_url( '/' ) ); ?>" class="btn-premium btn-premium-primary">
                        <?php _e( 'Return to Homepage', 'portfolio' ); ?>
                    </a>
                </div>
                <?php
            endif;
            ?>
        </div>
    </section>

<?php get_footer(); ?>