<?php
/**
 * Single Template for Portfolio Custom Post Type
 */
get_header('portfolio'); ?>

    <main class="bg-navy-950 min-h-screen pt-16">
        <?php while ( have_posts() ) : the_post(); ?>
            <article id="post-<?php the_ID(); ?>" <?php post_class(); ?>>
                
                <!-- Hero Section (Dynamic Background & Title) -->
                <header class="relative w-full h-[40vh] md:h-[55vh] min-h-[400px] flex items-end pb-12 px-6 overflow-hidden border-b border-white/5">
                    <div class="absolute inset-0 bg-navy-900 -z-20"></div>
                    
                    <?php if ( has_post_thumbnail() ) : ?>
                        <div class="absolute inset-0 bg-cover bg-center -z-10 transform scale-105" style="background-image: url('<?php echo esc_url( get_the_post_thumbnail_url( null, 'full' ) ); ?>');"></div>
                    <?php endif; ?>
                    
                    <!-- Beautiful fade-to-dark gradient so text is always readable -->
                    <div class="absolute inset-0 bg-gradient-to-t from-navy-950 via-navy-950/80 to-navy-950/20 -z-10"></div>
                    
                    <div class="max-w-4xl mx-auto w-full relative z-10 reveal">
                        <div class="flex flex-wrap gap-2 mb-4">
                            <?php 
                            // Dynamically load the project's assigned categories
                            $terms = get_the_terms( get_the_ID(), 'portfolio_category' );
                            if ( $terms && ! is_wp_error( $terms ) ) :
                                foreach ( $terms as $term ) : ?>
                                    <span class="bg-blue-600/20 border border-blue-500/30 text-blue-400 text-[10px] tracking-wider uppercase font-bold px-3 py-1 rounded-full backdrop-blur-sm">
                                        <?php echo esc_html( $term->name ); ?>
                                    </span>
                                <?php endforeach;
                            endif;
                            ?>
                        </div>
                        
                        <h1 class="text-3xl md:text-5xl lg:text-6xl font-extrabold text-white font-display leading-tight mb-4 drop-shadow-lg">
                            <?php the_title(); ?>
                        </h1>
                        
                        <div class="text-slate-400 text-sm md:text-base font-medium flex items-center space-x-4">
                            <span><i class="fa-regular fa-calendar mr-2"></i><?php echo get_the_date(); ?></span>
                            <span><i class="fa-solid fa-user-pen mr-2"></i><?php the_author(); ?></span>
                        </div>
                    </div>
                </header>

                <!-- Main Case Study Content Area -->
                <section class="max-w-4xl mx-auto px-6 py-16 reveal relative z-10">
                    <div class="glass-panel p-6 md:p-12 rounded-3xl border border-white/10 shadow-xl shadow-navy-950/50">
                        <!-- The Tailwind arbitrary selectors below automatically style standard WordPress HTML output (H2s, lists, paragraphs, images) -->
                        <div class="case-study-content text-slate-300 text-sm md:text-base leading-relaxed 
                                    [&>p]:mb-6 [&>p:last-child]:mb-0
                                    [&>h2]:text-2xl [&>h2]:font-bold [&>h2]:text-white [&>h2]:mt-10 [&>h2]:mb-4 [&>h2]:font-display
                                    [&>h3]:text-xl [&>h3]:font-bold [&>h3]:text-white [&>h3]:mt-8 [&>h3]:mb-4 [&>h3]:font-display
                                    [&>ul]:list-disc [&>ul]:pl-6 [&>ul]:mb-6 [&>ul>li]:mb-2 [&>ul>li::marker]:text-cyan
                                    [&>ol]:list-decimal [&>ol]:pl-6 [&>ol]:mb-6 [&>ol>li]:mb-2
                                    [&>blockquote]:border-l-4 [&>blockquote]:border-cyan-500 [&>blockquote]:pl-4 [&>blockquote]:italic [&>blockquote]:my-6 [&>blockquote]:text-slate-400 [&>blockquote]:bg-white/5 [&>blockquote]:py-2 [&>blockquote]:rounded-r-lg
                                    [&>figure>img]:rounded-xl [&>img]:rounded-xl [&>img]:my-8 [&>img]:shadow-lg [&>img]:border [&>img]:border-white/10
                                    [&>a]:text-cyan [&>a]:underline hover:[&>a]:text-blue-400 transition-colors">
                            <?php the_content(); ?>
                        </div>
                    </div>
                </section>

                <!-- Footer / Navigation inside post -->
                <section class="max-w-4xl mx-auto px-6 pb-16 reveal">
                    <div class="flex flex-col md:flex-row justify-center items-center pt-8 border-t border-white/10 gap-6">
                        <a href="<?php echo esc_url( home_url( '/#portfolio' ) ); ?>" class="btn-premium btn-premium-primary">
                            &larr; Back to Portfolio
                        </a>
                    </div>
                </section>

            </article>
        <?php endwhile; ?>
    </main>

<?php get_footer('portfolio'); ?>