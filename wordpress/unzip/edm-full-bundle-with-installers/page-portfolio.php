<?php
/**
 * Template Name: Projects & Portfolio - Alamin Hossain
 * Description: Dedicated Portfolio page template showcasing client campaigns and projects.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-24 pb-20 px-6">
    <div class="w-full max-w-[96%] 2xl:max-w-[1820px] mx-auto flex flex-col space-y-16">
        
        <!-- Header -->
        <div class="flex flex-col space-y-3">
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Proven Case Studies</span>
            <h1 class="text-4xl md:text-5xl lg:text-6xl font-extrabold text-white font-display tracking-tight leading-tight">
                Featured Projects & <br>
                <span class="bg-gradient-to-r from-cyan-400 via-blue-400 to-indigo-400 bg-clip-text text-transparent">Multi-Channel Campaigns</span>
            </h1>
        </div>

        <!-- EDM Showcase Card -->
        <?php get_template_part('template-parts/portfolio/section-edm-showcase'); ?>

        <!-- Portfolio Projects Grid with Filter Tabs -->
        <section id="portfolio" class="py-8" x-data="{ activeTab: 'all' }">
            <div class="flex flex-col space-y-12">
                
                <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
                    <div class="flex flex-col space-y-2">
                        <span class="text-xs tracking-widest text-cyan font-bold uppercase font-display">Client Portfolio</span>
                        <h2 class="text-2xl md:text-3xl font-extrabold text-white font-display">Search, Ads & Full-Stack Deployments</h2>
                    </div>
                    
                    <?php
                    $portfolio_categories = get_terms( ['taxonomy' => 'portfolio_category', 'hide_empty' => true] );
                    if ( ! empty( $portfolio_categories ) && ! is_wp_error( $portfolio_categories ) ) :
                    ?>
                    <div class="flex flex-wrap gap-2 bg-navy-900/85 p-1.5 rounded-xl border border-white/5 w-fit">
                        <button @click="activeTab = 'all'" :class="activeTab === 'all' ? 'active-tab' : 'inactive-tab'" class="portfolio-tab px-4 py-1.5 rounded-lg text-xs font-semibold uppercase tracking-wider transition-all duration-300">All Projects</button>
                        <?php foreach ( $portfolio_categories as $category ) : ?>
                            <button @click="activeTab = '<?php echo esc_attr( $category->slug ); ?>'" 
                                    :class="activeTab === '<?php echo esc_attr( $category->slug ); ?>' ? 'active-tab' : 'inactive-tab'" 
                                    class="portfolio-tab px-4 py-1.5 rounded-lg text-xs font-semibold uppercase tracking-wider transition-all duration-300">
                                <?php echo esc_html( $category->name ); ?>
                            </button>
                        <?php endforeach; ?>
                    </div>
                    <?php endif; ?>
                </div>

                <!-- Query Portfolio Projects -->
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
                    <?php
                    $portfolio_query = new WP_Query([
                        'post_type'      => 'portfolio',
                        'posts_per_page' => 12,
                    ]);

                    if ( $portfolio_query->have_posts() ) :
                        while ( $portfolio_query->have_posts() ) : $portfolio_query->the_post();
                            $terms = get_the_terms( get_the_ID(), 'portfolio_category' );
                            $slugs = [];
                            if ( $terms && ! is_wp_error( $terms ) ) {
                                foreach ( $terms as $term ) {
                                    $slugs[] = $term->slug;
                                }
                            }
                            $slug_string = implode( ' ', $slugs );
                            ?>
                            <div x-show="activeTab === 'all' || '<?php echo esc_attr( $slug_string ); ?>'.includes(activeTab)"
                                 x-transition:enter="transition ease-out duration-300"
                                 x-transition:enter-start="opacity-0 transform scale-95"
                                 x-transition:enter-end="opacity-100 transform scale-100"
                                 class="glass-panel rounded-2xl overflow-hidden border border-white/10 flex flex-col group hover:border-cyan/40 transition-all duration-300">
                                <?php if ( has_post_thumbnail() ) : ?>
                                    <div class="relative overflow-hidden aspect-video">
                                        <?php the_post_thumbnail( 'large', ['class' => 'w-full h-full object-cover group-hover:scale-105 transition-transform duration-500'] ); ?>
                                    </div>
                                <?php endif; ?>
                                <div class="p-6 flex flex-col flex-1 justify-between space-y-4">
                                    <div>
                                        <span class="text-[10px] uppercase font-bold text-cyan tracking-wider"><?php echo esc_html( $terms[0]->name ?? 'Case Study' ); ?></span>
                                        <h3 class="text-lg font-bold text-white mt-1 group-hover:text-cyan transition-colors"><?php the_title(); ?></h3>
                                        <div class="text-slate-400 text-xs mt-2 line-clamp-3"><?php the_excerpt(); ?></div>
                                    </div>
                                    <a href="<?php the_permalink(); ?>" class="text-xs font-bold text-cyan hover:text-white inline-flex items-center gap-1">
                                        <span>View Case Study</span> &rarr;
                                    </a>
                                </div>
                            </div>
                            <?php
                        endwhile;
                        wp_reset_postdata();
                    else :
                    ?>
                        <!-- Fallback Case Studies -->
                        <div class="glass-panel rounded-2xl p-6 border border-white/10">
                            <span class="text-xs font-bold text-gold uppercase">SEO Scaling</span>
                            <h3 class="text-lg font-bold text-white mt-2">+300% Organic Traffic Growth</h3>
                            <p class="text-xs text-slate-400 mt-2">Technical search restructuring, schema deployment, and keyword velocity scaling.</p>
                        </div>
                        <div class="glass-panel rounded-2xl p-6 border border-white/10">
                            <span class="text-xs font-bold text-cyan uppercase">Google Ads PPC</span>
                            <h3 class="text-lg font-bold text-white mt-2">5.2X ROAS E-Commerce Strategy</h3>
                            <p class="text-xs text-slate-400 mt-2">High-intent search keyword bidding and dynamic retargeting funnels.</p>
                        </div>
                        <div class="glass-panel rounded-2xl p-6 border border-white/10">
                            <span class="text-xs font-bold text-blue-400 uppercase">SaaS Engineering</span>
                            <h3 class="text-lg font-bold text-white mt-2">EDM 32-Socket Accelerator</h3>
                            <p class="text-xs text-slate-400 mt-2">Multi-threaded Windows desktop software with browser stream sniffing.</p>
                        </div>
                    <?php endif; ?>
                </div>

            </div>
        </section>

    </div>
</main>

<?php
get_footer();
