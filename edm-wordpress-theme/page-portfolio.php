<?php
/**
 * Template Name: Portfolio Showcase Page
 * Description: Dynamic WordPress CPT portfolio showcase grid with category filter tabs and pagination.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('portfolio');
?>

<main x-data="{ activeTab: 'all' }">
    <!-- Page Header Banner -->
    <section class="pt-16 pb-12 px-4 sm:px-6 border-b border-white/5 relative overflow-hidden" style="background: linear-gradient(175deg, #020617 0%, #0a1628 60%, #050f1f 100%);">
        <div class="max-w-7xl mx-auto">
            <div class="flex items-center gap-2 text-xs text-slate-500 mb-4">
                <a href="<?php echo esc_url(home_url('/')); ?>" class="hover:text-cyan transition-colors">Home</a>
                <i class="fa-solid fa-chevron-right text-[8px]"></i>
                <span class="text-slate-400">Portfolio</span>
            </div>
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Work Highlights</span>
            <h1 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display mt-2 mb-3">
                Proven Campaigns Across Industries
            </h1>
            <p class="text-slate-300 max-w-2xl text-sm sm:text-base leading-relaxed">
                Real campaigns with verified analytics data from Google Search Console, GA4, and Meta Ads.
            </p>
        </div>
    </section>

    <!-- Portfolio Filter Grid -->
    <section class="py-16 md:py-24 px-4 sm:px-6">
        <div class="max-w-7xl mx-auto flex flex-col space-y-12">
            
            <!-- Category Filter Tabs -->
            <?php
            $portfolio_cats = get_terms(['taxonomy' => 'portfolio_category', 'hide_empty' => true]);
            if (!empty($portfolio_cats) && !is_wp_error($portfolio_cats)) : ?>
            <div class="flex flex-wrap gap-2 bg-slate-900/80 p-1.5 rounded-xl border border-white/10 w-fit">
                <button @click="activeTab = 'all'" :class="activeTab === 'all' ? 'active-tab' : 'inactive-tab'" class="portfolio-tab px-4 py-2 rounded-lg text-xs font-semibold uppercase tracking-wider transition-all duration-300">All Projects</button>
                <?php foreach ($portfolio_cats as $cat) : ?>
                <button @click="activeTab = '<?php echo esc_attr($cat->slug); ?>'" :class="activeTab === '<?php echo esc_attr($cat->slug); ?>' ? 'active-tab' : 'inactive-tab'" class="portfolio-tab px-4 py-2 rounded-lg text-xs font-semibold uppercase tracking-wider transition-all duration-300"><?php echo esc_html($cat->name); ?></button>
                <?php endforeach; ?>
            </div>
            <?php endif; ?>

            <!-- Projects Grid -->
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                <?php
                $paged = (get_query_var('page')) ? get_query_var('page') : (get_query_var('paged') ? get_query_var('paged') : 1);
                $portfolio_query = new WP_Query([
                    'post_type'      => 'portfolio',
                    'posts_per_page' => 9,
                    'paged'          => $paged,
                ]);

                if ($portfolio_query->have_posts()) :
                    while ($portfolio_query->have_posts()) : $portfolio_query->the_post();
                        $terms = get_the_terms(get_the_ID(), 'portfolio_category');
                        $tab_conds = [];
                        $primary_cat = 'Project';

                        if ($terms && !is_wp_error($terms)) {
                            foreach ($terms as $term) {
                                $tab_conds[] = "activeTab === '" . esc_js($term->slug) . "'";
                            }
                            $primary_cat = esc_html($terms[0]->name);
                        }
                        $alpine_cond = !empty($tab_conds) ? "activeTab === 'all' || " . implode(' || ', $tab_conds) : "activeTab === 'all'";
                        $bg_img = get_the_post_thumbnail_url(get_the_ID(), 'large');
                        $bg_style = $bg_img ? "background-image: url('" . esc_url($bg_img) . "');" : "";
                        ?>
                        <div x-show="<?php echo $alpine_cond; ?>" class="portfolio-card glass-panel rounded-2xl overflow-hidden border border-white/10 group reveal hover:border-cyan/30 transition-all duration-300">
                            <div class="relative overflow-hidden aspect-video bg-slate-900 flex items-center justify-center bg-cover bg-center transition-transform duration-500 group-hover:scale-[1.02]" style="<?php echo esc_attr($bg_style); ?>">
                                <div class="absolute inset-0 bg-gradient-to-t from-slate-950 via-slate-950/60 to-transparent z-10"></div>
                                <?php if (!$bg_img) : ?>
                                    <span class="text-5xl opacity-15 text-blue-400 z-10"><i class="fa-solid fa-image"></i></span>
                                <?php endif; ?>
                                <div class="absolute top-3 left-3 z-20 bg-blue-600 text-white text-[10px] tracking-wider uppercase font-bold px-3 py-1 rounded-full shadow">
                                    <?php echo $primary_cat; ?>
                                </div>
                            </div>
                            <div class="p-5 sm:p-6 flex flex-col space-y-3 relative z-20">
                                <h3 class="text-base font-bold text-white group-hover:text-gold transition-colors font-display leading-snug"><?php the_title(); ?></h3>
                                <p class="text-xs text-slate-400 leading-relaxed"><?php echo wp_trim_words(get_the_excerpt(), 14); ?></p>
                                <div class="flex items-center justify-between pt-3 border-t border-white/5">
                                    <span class="text-[10px] uppercase tracking-wider text-slate-500">Case Study</span>
                                    <a href="<?php the_permalink(); ?>" class="text-xs font-bold text-emerald-400 hover:text-emerald-300 transition-colors inline-flex items-center gap-1">
                                        Read Details &rarr;
                                    </a>
                                </div>
                            </div>
                        </div>
                    <?php endwhile;
                else : ?>
                    <div class="col-span-full py-12 text-center text-slate-400">
                        <i class="fa-solid fa-folder-open text-4xl mb-3 text-slate-600"></i>
                        <p class="text-sm">Portfolio items can be added directly via WordPress Dashboard &rarr; Portfolio.</p>
                    </div>
                <?php endif; ?>
            </div>

            <!-- Pagination -->
            <?php
            if ($portfolio_query->max_num_pages > 1) :
                echo '<div class="portfolio-pagination flex flex-wrap items-center justify-center gap-2 mt-8">';
                echo paginate_links([
                    'base'      => str_replace(999999999, '%#%', esc_url(get_pagenum_link(999999999))),
                    'format'    => '?paged=%#%',
                    'current'   => max(1, $paged),
                    'total'     => $portfolio_query->max_num_pages,
                    'prev_text' => '<i class="fa-solid fa-chevron-left text-[10px] mr-1"></i> Prev',
                    'next_text' => 'Next <i class="fa-solid fa-chevron-right text-[10px] ml-1"></i>',
                ]);
                echo '</div>';
            endif;
            wp_reset_postdata();
            ?>
        </div>
    </section>

    <!-- Page CTA -->
    <section class="py-20 px-4 sm:px-6 border-t border-white/5" style="background: radial-gradient(ellipse 60% 80% at 50% 50%, rgba(37,99,235,0.12) 0%, transparent 70%);">
        <div class="max-w-3xl mx-auto text-center flex flex-col items-center space-y-6">
            <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Need a Tailored Campaign Strategy?</h2>
            <p class="text-slate-300 text-xs sm:text-sm max-w-xl">Every business has unique search territory and audience funnels. Let's build yours.</p>
            <div class="flex flex-wrap gap-4 justify-center">
                <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary">
                    <i class="fa-solid fa-calendar-check text-xs"></i> Book Strategy Session
                </a>
            </div>
        </div>
    </section>
</main>

<?php get_footer('portfolio'); ?>