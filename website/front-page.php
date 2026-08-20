<?php
/**
 * The template for the homepage (N F Alamin Hossain Master Portfolio)
 *
 * @package EDM_Theme
 */
get_header('portfolio'); ?>

    <main>
        <!-- 1. HERO SECTION -->
        <section class="hero-section relative pt-10 pb-20 lg:pt-20 lg:pb-32 px-6 overflow-hidden bg-gradient-to-b from-navy-950 to-navy-900 z-10">
            <div class="absolute inset-0 hero-grid-lines pointer-events-none -z-10"></div>
            <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
                
                <!-- Left Details -->
                <div class="lg:col-span-7 flex flex-col space-y-6 items-center text-center lg:items-start lg:text-left">
                    <div class="hero-badge inline-flex items-center space-x-2 px-4 py-1.5 rounded-full w-fit">
                        <span class="hero-badge-dot w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                        <span class="hero-badge-text text-xs tracking-wider uppercase font-semibold">Available for Projects</span>
                    </div>

                    <h1 class="text-3xl sm:text-4xl md:text-5xl lg:text-6xl font-extrabold tracking-tight leading-tight text-white font-display">
                        <span class="text-gradient-cyan">Accelerating</span> 
                        <span class="relative inline-block">
                            <span class="text-gradient-cyan">Business</span>
                            <svg class="absolute left-0 w-full h-3 sm:h-4 -bottom-1 pointer-events-none text-gold" viewBox="0 0 100 20" preserveAspectRatio="none" fill="none">
                                <path d="M 1 14 C 20 2 60 5 99 16" stroke="currentColor" stroke-width="4" stroke-linecap="round" />
                            </svg>
                        </span> 
                        <span class="text-gradient-cyan">Growth</span> <br>
                        <span class="text-slate-400">Through Custom</span> <br>
                        <!-- TYPING EFFECT PLACEMENT -->
                        <span id="typing-text" class="text-gradient-gold"></span><span class="text-gold animate-pulse">|</span>
                    </h1>

                    <p class="hero-description text-base md:text-lg max-w-xl leading-relaxed mx-auto lg:mx-0">I build transparent, highly-optimized campaigns designed to maximize traffic, generate qualified leads, and grow revenue as a <span class="hero-highlight-text font-bold">certified SEO Specialist, Google Ads Expert, and Social Media Marketing Strategist</span>.</p>

                    <!-- CTAs -->
                    <div class="flex flex-wrap items-center justify-center lg:justify-start gap-5 pt-4 w-full">
                        <a href="#contact" class="btn-premium btn-premium-primary">
                            Book Free Strategy Session
                        </a>
                        <a href="#portfolio" class="btn-premium btn-premium-outline">
                            Download CV
                        </a>
                    </div>

                    <!-- Stats (Animated Counters) -->
                    <div class="grid grid-cols-2 md:grid-cols-4 gap-6 md:gap-4 pt-10 border-t border-white/5 w-full">
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-3xl font-extrabold text-white font-display stat-num" data-target="100">0</span>
                            <span class="hero-stat-label text-xs tracking-wider uppercase">Projects Completed</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-3xl font-extrabold text-white font-display stat-num" data-target="95">0</span>
                            <span class="hero-stat-label text-xs tracking-wider uppercase">Client Satisfaction %</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-3xl font-extrabold text-white font-display stat-num" data-target="5">0</span>
                            <span class="hero-stat-label text-xs tracking-wider uppercase">Years Experience</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-3xl font-extrabold text-white font-display stat-num" data-target="50">0</span>
                            <span class="hero-stat-label text-xs tracking-wider uppercase">Businesses Helped</span>
                        </div>
                    </div>
                </div>

                <!-- Right Visual -->
                
                <div class="lg:col-span-5 flex justify-center relative items-end w-full">
                    <div class="absolute inset-0 bg-gradient-to-tr from-blue-600/20 to-gold/20 rounded-2xl blur-3xl -z-10"></div>
                
                    <div class="relative w-[85vw] h-[85vw] max-w-[260px] max-h-[260px] sm:max-w-[320px] sm:max-h-[320px] md:max-w-[380px] md:max-h-[380px] mt-16 md:mt-24 mx-auto aspect-square">
                
                        <!-- 1. Background Cyan Circle -->
                        <div class="absolute bottom-0 w-full h-full rounded-full border-[4px] premium-circle z-10"></div>
                
                        <!-- 2. Pop-out Image Wrapper with Custom Clip-Path -->
                        <div class="absolute bottom-0 w-full h-full z-30 pointer-events-none" style="clip-path: inset(-100% 0 0 0 round 0 0 50% 50%);">
                            <img src="<?php echo get_template_directory_uri(); ?>/nf.png"
                                 class="hero-photo absolute bottom-0 left-1/2 -translate-x-[62%] w-[360px] sm:w-[440px] md:w-[540px] max-w-none drop-shadow-2xl hero-photo-premium"
                                 alt="Alamin Hossain - Profile photo">
                        </div>
                
                        <div class="absolute top-4 -left-2 sm:-left-6 glass-panel p-2 md:p-3.5 rounded-xl border border-white/10 flex items-center space-x-2 md:space-x-3 z-40 animate-bounce" style="animation-duration: 4s;">
                            <span class="text-xl md:text-2xl">📈</span>
                            <div class="flex flex-col text-left">
                                <span class="text-[10px] md:text-xs font-bold text-white leading-none">+300%</span>
                                <span class="text-[8px] md:text-[9px] text-slate-400">Organic Growth</span>
                            </div>
                        </div>
                
                        <div class="absolute bottom-6 -right-2 sm:-right-6 glass-panel p-2 md:p-3.5 rounded-xl border border-white/10 flex items-center space-x-2 md:space-x-3 z-40 animate-bounce" style="animation-duration: 5s;">
                            <span class="text-xl md:text-2xl">🎯</span>
                            <div class="flex flex-col text-left">
                                <span class="text-[10px] md:text-xs font-bold text-white leading-none">5X ROAS</span>
                                <span class="text-[8px] md:text-[9px] text-slate-400">Google Ads Performance</span>
                            </div>
                        </div>
                
                    </div>
                </div>
            </div>
        </section>

        <!-- 2. TRUST SECTION -->
        <section class="py-12 trust-section bg-gradient-to-b from-navy-900 to-navy-900/30 px-6">
            <div class="max-w-7xl mx-auto flex flex-col lg:flex-row items-center justify-between gap-8">
                
                <!-- Rating Info -->
                <div class="flex flex-col md:flex-row items-center gap-4 text-center md:text-left">
                    <div class="flex items-center space-x-1 text-amber-400 text-lg">
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                    </div>
                    <div>
                        <p class="text-sm text-slate-300 font-semibold"><span class="text-white">★★★★★ 5.0 Rating</span> across digital marketplaces</p>
                        <p class="text-xs text-slate-500">Based on verified client feedback and metrics</p>
                    </div>
                </div>

                <!-- Trusted/Certified By Logos -->
                <div class="flex flex-wrap items-center justify-center gap-8 md:gap-12 opacity-70 grayscale hover:grayscale-0 transition-all duration-300">
                    <div class="flex items-center space-x-2">
                        <i class="fa-brands fa-google text-2xl text-slate-400"></i>
                        <span class="text-xs tracking-wider uppercase font-semibold text-slate-400">Google Certified</span>
                    </div>
                    <div class="flex items-center space-x-2">
                        <i class="fa-brands fa-hubspot text-2xl text-slate-400"></i>
                        <span class="text-xs tracking-wider uppercase font-semibold text-slate-400">HubSpot</span>
                    </div>
                    <div class="flex items-center space-x-2">
                        <i class="fa-brands fa-facebook text-2xl text-slate-400"></i>
                        <span class="text-xs tracking-wider uppercase font-semibold text-slate-400">Meta</span>
                    </div>
                    <div class="flex items-center space-x-2">
                        <span class="text-lg font-black tracking-tighter uppercase text-slate-400">UPWORK</span>
                    </div>
                </div>
            </div>
        </section>

        <!-- 3. ABOUT ME -->
        <section id="about" class="py-16 md:py-24 px-6 relative">
            <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-16 items-center">
                
                <!-- Left Visual (Image Integration) -->
                <div class="lg:col-span-5 relative reveal flex justify-center items-center">
                    <div class="relative w-full max-w-[480px] mx-auto">
                        <img src="<?php echo get_template_directory_uri(); ?>/Assets/images/nf011.png" alt="Alamin Hossain sitting" class="w-full h-auto object-contain chair-portrait">
                    </div>
                </div>

                <!-- Right Story Details -->
                <div class="lg:col-span-7 flex flex-col items-center text-center lg:items-start lg:text-left space-y-6 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">The Professional Journey</span>
                    <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">Bridging the Gap Between Technical Code & High-Performance Marketing</h2>
                    
                    <p class="text-slate-400 leading-relaxed text-sm">
                        With a solid academic foundation in Computer Science and over 5 years of industry experience, I focus on the elements of search engine mechanics and paid advertising systems. I avoid empty metrics, focusing instead on campaign performance that impacts your revenue.
                    </p>
                    <p class="text-slate-400 leading-relaxed text-sm">
                        As a certified marketer, my expertise includes technical SEO audits, ROI-driven PPC execution, and social audience targeting. I create search engine optimization strategies and user-experience frameworks that scale.
                    </p>

                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6 pt-4 w-full">
                        <div class="flex flex-col md:flex-row items-center md:items-start space-y-3 md:space-y-0 space-x-0 md:space-x-3 text-center md:text-left">
                            <span class="w-8 h-8 rounded bg-navy-900 flex items-center justify-center text-blue-400 border border-white/5 shrink-0"><i class="fa-solid fa-shield-halved text-xs"></i></span>
                            <div>
                                <h4 class="text-sm font-semibold text-white">White-Hat Strategy</h4>
                                <p class="text-xs text-slate-500">I adhere to search engine guidelines to help ensure your organic profile stands long-term algorithm shifts.</p>
                            </div>
                        </div>
                        <div class="flex flex-col md:flex-row items-center md:items-start space-y-3 md:space-y-0 space-x-0 md:space-x-3 text-center md:text-left">
                            <span class="w-8 h-8 rounded bg-navy-900 flex items-center justify-center text-gold border border-white/5 shrink-0"><i class="fa-solid fa-code text-xs"></i></span>
                            <div>
                                <h4 class="text-sm font-semibold text-white">Technical Background</h4>
                                <p class="text-xs text-slate-500">Computer Science training allows for deep core vitals audits and custom indexation maps.</p>
                            </div>
                        </div>
                    </div>

                    <!-- Relocated Core Competencies -->
                    <div class="flex flex-col space-y-4 pt-6 border-t border-white/5 w-full mt-4">
                        <div class="glass-panel p-4 rounded-xl flex flex-col md:flex-row items-center md:items-start text-center md:text-left space-y-3 md:space-y-0 space-x-0 md:space-x-4 transition-all duration-300">
                            <span class="w-10 h-10 rounded-lg bg-blue-600/10 flex items-center justify-center text-blue-400 border border-blue-500/20 shrink-0 text-xl">🎯</span>
                            <div>
                                <h4 class="text-sm font-bold text-white font-display">Results-First Approach</h4>
                                <p class="text-xs text-slate-400 mt-1">Every strategy is optimized for your business objectives, avoiding empty metrics.</p>
                            </div>
                        </div>

                        <div class="glass-panel p-4 rounded-xl flex flex-col md:flex-row items-center md:items-start text-center md:text-left space-y-3 md:space-y-0 space-x-0 md:space-x-4 transition-all duration-300">
                            <span class="w-10 h-10 rounded-lg bg-cyan-600/10 flex items-center justify-center text-cyan border border-cyan-500/20 shrink-0 text-xl">📊</span>
                            <div>
                                <h4 class="text-sm font-bold text-white font-display">Data-Driven Execution</h4>
                                <p class="text-xs text-slate-400 mt-1">Utilizing real analytics data to drive conversion optimization and build sustainable pipelines.</p>
                            </div>
                        </div>

                        <div class="glass-panel p-4 rounded-xl flex flex-col md:flex-row items-center md:items-start text-center md:text-left space-y-3 md:space-y-0 space-x-0 md:space-x-4 transition-all duration-300">
                            <span class="w-10 h-10 rounded-lg bg-amber-600/10 flex items-center justify-center text-amber-400 border border-amber-500/20 shrink-0 text-xl">🌍</span>
                            <div>
                                <h4 class="text-sm font-bold text-white font-display">Global Project Deployment</h4>
                                <p class="text-xs text-slate-400 mt-1">Proven experience running multi-channel campaigns for firms across USA, UK, UAE, and APAC regions.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>

        <!-- 4. SERVICES SECTION -->
        <?php get_template_part( 'section-services' ); ?>

        <!-- 5. FEATURED PORTFOLIO -->
        <section id="portfolio" class="py-16 md:py-24 px-6" x-data="{ activeTab: 'all' }">
            <div class="max-w-7xl mx-auto flex flex-col space-y-16">
                
                <div class="flex flex-col md:flex-row md:items-end justify-between gap-6 reveal">
                    <div class="flex flex-col space-y-4 max-w-xl">
                        <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Work Highlights</span>
                        <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">Proven Campaigns Across Industries</h2>
                    </div>
                    
                    <!-- Tabs Menu -->
                    <?php
                    $portfolio_categories = get_terms( ['taxonomy' => 'portfolio_category', 'hide_empty' => true] );
                    if ( ! empty( $portfolio_categories ) && ! is_wp_error( $portfolio_categories ) ) :
                    ?>
                    <div class="flex flex-wrap gap-2 bg-navy-900/85 p-1.5 rounded-xl border border-white/5 w-fit portfolio-tab-container">
                        <button @click="activeTab = 'all'" :class="activeTab === 'all' ? 'active-tab' : 'inactive-tab'" class="portfolio-tab px-4 py-1.5 rounded-lg text-xs font-semibold uppercase tracking-wider transition-all duration-300">All Project Files</button>
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

                <!-- Projects Showcase Grid -->
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-8">
                    
                    <?php
                    // Fetching Portfolio Projects dynamically from WordPress
                    $paged = ( get_query_var( 'page' ) ) ? get_query_var( 'page' ) : ( get_query_var( 'paged' ) ? get_query_var( 'paged' ) : 1 );
                    $portfolio_query = new WP_Query([
                        'post_type'      => 'portfolio',
                        'posts_per_page' => 6, // Show 6 items per page
                        'paged'          => $paged,
                    ]);

                    if ( $portfolio_query->have_posts() ) :
                        while ( $portfolio_query->have_posts() ) : $portfolio_query->the_post();
                            
                            // 1. Fetch Categories for Alpine.js Tab Logic (e.g. 'seo', 'ads')
                            $terms = get_the_terms( get_the_ID(), 'portfolio_category' );
                            $tab_conditions = [];
                            $primary_category_name = 'Project';

                            if ( $terms && ! is_wp_error( $terms ) ) {
                                foreach ( $terms as $term ) {
                                    $tab_conditions[] = "activeTab === '" . esc_js( $term->slug ) . "'";
                                }
                                $primary_category_name = esc_html( $terms[0]->name );
                            }
                            // Create Alpine string: activeTab === 'all' || activeTab === 'seo'
                            $alpine_condition = !empty($tab_conditions) ? "activeTab === 'all' || " . implode(' || ', $tab_conditions) : "activeTab === 'all'";

                            // 2. Fetch Featured Image
                            $bg_image = get_the_post_thumbnail_url( get_the_ID(), 'large' );
                            $bg_style = $bg_image ? "background-image: url('" . esc_url( $bg_image ) . "');" : "";
                            ?>
                            
                            <div x-show="<?php echo $alpine_condition; ?>" class="portfolio-card glass-panel rounded-lg md:rounded-2xl overflow-hidden border border-white/10 group reveal">
                                <!-- Dynamic Image Background -->
                                <div class="relative overflow-hidden aspect-video bg-navy-900 flex items-center justify-center p-2 md:p-6 bg-cover bg-center transition-transform duration-500 group-hover:scale-[1.02]" style="<?php echo esc_attr( $bg_style ); ?>">
                                    <div class="absolute inset-0 bg-gradient-to-t from-navy-950 via-navy-950/60 to-transparent group-hover:opacity-90 transition-opacity duration-300 z-10"></div>
                                    
                                    <?php if ( ! $bg_image ) : ?>
                                        <span class="text-3xl md:text-7xl opacity-20 text-blue-400 z-10"><i class="fa-solid fa-image"></i></span>
                                    <?php endif; ?>

                                    <div class="absolute top-2 left-2 md:top-4 md:left-4 z-20 bg-blue-600 text-white text-[10px] md:text-[10px] tracking-wider uppercase font-bold px-2 py-1 md:px-3 md:py-1 rounded-full shadow-lg">
                                        <?php echo $primary_category_name; ?>
                                    </div>
                                </div>
                                <div class="p-3 md:p-6 flex flex-col space-y-2 md:space-y-4 relative z-20">
                                    <h3 class="text-sm md:text-lg font-bold text-white group-hover:text-gold transition-colors font-display leading-snug md:leading-normal"><?php the_title(); ?></h3>
                                    <p class="text-xs md:text-xs text-slate-400 leading-snug md:leading-relaxed"><?php echo wp_trim_words( get_the_excerpt(), 12 ); ?></p>
                                    <div class="flex flex-row items-center justify-between pt-2 md:pt-4 border-t border-white/5 gap-2">
                                        <span class="text-[10px] md:text-[10px] uppercase tracking-wider text-slate-500">View Details</span>
                                        <a href="<?php the_permalink(); ?>" class="text-[10px] md:text-xs font-bold text-emerald-400 hover:text-emerald-300 transition-colors">Read Case Study &rarr;</a>
                                    </div>
                                </div>
                            </div>

                        <?php 
                        endwhile;
                    else :
                        echo '<p class="text-slate-400 col-span-3">No portfolio projects found. Add them in the WordPress Dashboard.</p>';
                    endif; 
                    ?>

                </div>

                <?php
                // Pagination for Portfolio Projects
                if ( $portfolio_query->max_num_pages > 1 ) :
                    echo '<div class="portfolio-pagination flex flex-wrap items-center justify-center gap-2 mt-8 md:mt-12 reveal">';
                    echo paginate_links([
                        'base'      => str_replace( 999999999, '%#%', esc_url( get_pagenum_link( 999999999 ) ) ),
                        'format'    => '?paged=%#%',
                        'current'   => max( 1, $paged ),
                        'total'     => $portfolio_query->max_num_pages,
                        'prev_text' => '<i class="fa-solid fa-chevron-left text-[10px] mr-1"></i> Prev',
                        'next_text' => 'Next <i class="fa-solid fa-chevron-right text-[10px] ml-1"></i>',
                    ]);
                    echo '</div>';
                endif;
                wp_reset_postdata(); // Important: Reset WP data
                ?>
            </div>
        </section>

        <!-- 6. CASE STUDIES -->
        <section id="case-studies" class="py-16 md:py-24 px-6 bg-navy-900/10 border-t border-white/5">
            <div class="max-w-7xl mx-auto flex flex-col space-y-16">
                
                <div class="text-center max-w-2xl mx-auto flex flex-col space-y-4 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Data & Outcomes</span>
                    <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">In-Depth Campaign Breakdown</h2>
                    <p class="text-slate-400 text-sm">Real execution paths showing challenge, structural action plan, and direct metrics verified by Google Search Console and Analytics.</p>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-8">
                    
                    <!-- Case 1 -->
                    <div class="glass-panel p-5 md:p-8 rounded-xl md:rounded-2xl border border-blue-500/10 relative flex flex-col justify-between reveal">
                        <div class="flex flex-col space-y-3 md:space-y-6">
                            <div class="flex items-center justify-between gap-2">
                                <span class="text-[10px] md:text-xs font-semibold tracking-wider uppercase text-cyan bg-cyan/10 px-2 py-1 md:px-3 md:py-1 rounded-full w-fit">SEO Growth</span>
                                <span class="text-[10px] md:text-xs text-slate-500 hidden md:inline">B2B SaaS</span>
                            </div>
                            <h3 class="text-sm md:text-xl font-bold text-white font-display leading-snug md:leading-normal">300% Direct Organic Traffic Growth Within 6 Months</h3>
                            <p class="text-xs md:text-xs text-slate-400 leading-relaxed">
                                <strong class="text-white">Challenge:</strong> Client page indices dropped due to duplicate taxonomies and thin-content structures after a platform migration. <br><br>
                                <strong class="text-white">Execution:</strong> Rebuilt the global redirection mapping plan, audited indexing signals, and targeted keywords matching commercial transactional intent.
                            </p>
                        </div>
                        <div class="pt-4 md:pt-6 mt-4 md:mt-8 border-t border-white/5 flex items-center justify-between gap-2">
                            <span class="text-[10px] tracking-wider uppercase text-slate-500">Direct Metric</span>
                            <span class="text-sm md:text-lg font-bold text-emerald-400">+300% Organic Sessions</span>
                        </div>
                    </div>

                    <!-- Case 2 -->
                    <div class="glass-panel p-5 md:p-8 rounded-xl md:rounded-2xl border border-blue-500/10 relative flex flex-col justify-between reveal">
                        <div class="flex flex-col space-y-3 md:space-y-6">
                            <div class="flex items-center justify-between gap-2">
                                <span class="text-[10px] md:text-xs font-semibold tracking-wider uppercase text-gold bg-gold/10 px-2 py-1 md:px-3 md:py-1 rounded-full w-fit">Google Ads</span>
                                <span class="text-[10px] md:text-xs text-slate-500 hidden md:inline">E-Commerce</span>
                            </div>
                            <h3 class="text-sm md:text-xl font-bold text-white font-display leading-snug md:leading-normal">5X ROAS Optimization and Scaled Sales Conversion</h3>
                            <p class="text-xs md:text-xs text-slate-400 leading-relaxed">
                                <strong class="text-white">Challenge:</strong> Low quality leads driving up average cost-per-acquisition metrics, with limited conversion activity recorded. <br><br>
                                <strong class="text-white">Execution:</strong> Restructured search parameters to target high-intent transactional key phrases while deploying strategic negative keyword match lists.
                            </p>
                        </div>
                        <div class="pt-4 md:pt-6 mt-4 md:mt-8 border-t border-white/5 flex items-center justify-between gap-2">
                            <span class="text-[10px] tracking-wider uppercase text-slate-500">Direct Metric</span>
                            <span class="text-sm md:text-lg font-bold text-emerald-400">5.0+ ROAS Verified</span>
                        </div>
                    </div>

                    <!-- Case 3 -->
                    <div class="glass-panel p-5 md:p-8 rounded-xl md:rounded-2xl border border-blue-500/10 relative flex flex-col justify-between reveal">
                        <div class="flex flex-col space-y-3 md:space-y-6">
                            <div class="flex items-center justify-between gap-2">
                                <span class="text-[10px] md:text-xs font-semibold tracking-wider uppercase text-cyan bg-cyan/10 px-2 py-1 md:px-3 py-1 rounded-full w-fit">Local SEO</span>
                                <span class="text-[10px] md:text-xs text-slate-500 hidden md:inline">Local Service Provider</span>
                            </div>
                            <h3 class="text-sm md:text-xl font-bold text-white font-display leading-snug md:leading-normal">Dominating Local Proximity Search & Leads Generation</h3>
                            <p class="text-xs md:text-xs text-slate-400 leading-relaxed">
                                <strong class="text-white">Challenge:</strong> Minimum local visibility for local service queries despite being based in a primary service territory. <br><br>
                                <strong class="text-white">Execution:</strong> Completely audited local schema structures, expanded citations, and updated geo-targeted content pages matching search patterns.
                            </p>
                        </div>
                        <div class="pt-4 md:pt-6 mt-4 md:mt-8 border-t border-white/5 flex items-center justify-between gap-2">
                            <span class="text-[10px] tracking-wider uppercase text-slate-500">Direct Metric</span>
                            <span class="text-sm md:text-lg font-bold text-emerald-400">Top 3 Map Pack Rank</span>
                        </div>
                    </div>

                </div>
            </div>
        </section>

        <!-- 7. SKILLS SECTION WITH PROGRESS BARS -->
        <section id="skills" class="py-16 md:py-24 px-6 border-t border-white/5">
            <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-16 items-center">
                
                <div class="lg:col-span-5 flex flex-col space-y-6 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Technical Toolset</span>
                    <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">Core Marketing Competency Matrix</h2>
                    <p class="text-slate-400 text-sm leading-relaxed">
                        My practice is rooted in testing and analysis. I keep track of search algorithm developments and conversion optimization structures so your campaigns remain competitive.
                    </p>
                    <div class="flex flex-wrap gap-2 pt-2">
                        <span class="px-3 py-1 bg-navy-900 border border-white/5 rounded text-xs text-slate-400">Ahrefs</span>
                        <span class="px-3 py-1 bg-navy-900 border border-white/5 rounded text-xs text-slate-400">Semrush</span>
                        <span class="px-3 py-1 bg-navy-900 border border-white/5 rounded text-xs text-slate-400">Screaming Frog</span>
                        <span class="px-3 py-1 bg-navy-900 border border-white/5 rounded text-xs text-slate-400">Google Search Console</span>
                        <span class="px-3 py-1 bg-navy-900 border border-white/5 rounded text-xs text-slate-400">Google Analytics (GA4)</span>
                    </div>
                </div>

                <!-- Skill Metrics -->
                <div class="lg:col-span-7 space-y-6 bg-navy-900/40 p-8 rounded-2xl border border-white/5 reveal">
                    
                    <!-- Skill 1 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Search Engine Optimization (On-Page, Off-Page, Tech)</span>
                            <span class="text-xs font-semibold text-cyan font-mono">95%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="95" style="width: 0%"></div>
                        </div>
                    </div>

                    <!-- Skill 2 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Google PPC Ads Campaign Management</span>
                            <span class="text-xs font-semibold text-cyan font-mono">90%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="90" style="width: 0%"></div>
                        </div>
                    </div>

                    <!-- Skill 3 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Facebook Paid Target Ads</span>
                            <span class="text-xs font-semibold text-cyan font-mono">88%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="88" style="width: 0%"></div>
                        </div>
                    </div>

                    <!-- Skill 4 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Audience Targeting & Keywords Intent Mapping</span>
                            <span class="text-xs font-semibold text-cyan font-mono">93%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="93" style="width: 0%"></div>
                        </div>
                    </div>

                    <!-- Skill 5 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Lead Generation & administrative pipelines</span>
                            <span class="text-xs font-semibold text-cyan font-mono">90%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="90" style="width: 0%"></div>
                        </div>
                    </div>

                    <!-- Skill 6 -->
                    <div class="flex flex-col space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-sm font-bold text-white font-display">Virtual Assistant Operations & Advanced Sheets</span>
                            <span class="text-xs font-semibold text-cyan font-mono">92%</span>
                        </div>
                        <div class="w-full h-1.5 bg-navy-950 rounded-full overflow-hidden">
                            <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-500 rounded-full skill-fill" data-pct="92" style="width: 0%"></div>
                        </div>
                    </div>

                </div>
            </div>
        </section>

        <!-- 8. CERTIFICATIONS -->
        <section class="py-16 md:py-24 px-6 bg-navy-900/30 border-t border-white/5">
            <div class="max-w-7xl mx-auto flex flex-col space-y-16">
                
                <div class="text-center max-w-xl mx-auto flex flex-col space-y-4 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Validated Knowledge</span>
                    <h2 class="text-3xl font-extrabold text-white font-display">Professional Certifications</h2>
                    <p class="text-slate-400 text-xs">Verified certifications from top digital marketing authorities.</p>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-8 max-w-4xl mx-auto w-full">
                    
                    <!-- Cert 1 -->
                    <div class="glass-panel p-8 rounded-2xl flex items-start space-x-6 border border-white/10 group hover:border-blue-500/20 transition-all duration-300 reveal">
                        <div class="w-16 h-16 rounded-xl bg-orange-600/15 flex items-center justify-center text-orange-400 text-3xl border border-orange-500/10 shrink-0">
                            <i class="fa-brands fa-hubspot"></i>
                        </div>
                        <div class="flex flex-col space-y-2">
                            <span class="text-[10px] text-orange-400 font-mono tracking-widest uppercase">HubSpot Academy</span>
                            <h3 class="text-lg font-bold text-white group-hover:text-gold transition-colors font-display">Content Marketing Certification</h3>
                            <p class="text-xs text-slate-400 leading-relaxed">Advanced organic growth tactics, customer journey creation, and funnel content development.</p>
                            <span class="text-[10px] text-slate-500 mt-2 font-mono">Issued May 2024 • Credentials Verified</span>
                        </div>
                    </div>

                    <!-- Cert 2 -->
                    <div class="glass-panel p-8 rounded-2xl flex items-start space-x-6 border border-white/10 group hover:border-blue-500/20 transition-all duration-300 reveal">
                        <div class="w-16 h-16 rounded-xl bg-blue-600/15 flex items-center justify-center text-blue-400 text-3xl border border-blue-500/10 shrink-0">
                            <i class="fa-brands fa-google"></i>
                        </div>
                        <div class="flex flex-col space-y-2">
                            <span class="text-[10px] text-blue-400 font-mono tracking-widest uppercase font-display">Google</span>
                            <h3 class="text-lg font-bold text-white group-hover:text-gold transition-colors font-display">Digital Marketing & E-commerce Certificate</h3>
                            <p class="text-xs text-slate-400 leading-relaxed">Comprehensive management covering display, SEM, performance max systems, and UX frameworks.</p>
                            <span class="text-[10px] text-slate-500 mt-2 font-mono">Issued August 2023 • Credentials Verified</span>
                        </div>
                    </div>

                </div>
            </div>
        </section>

        <?php
        $testimonials_data = [];
        $testimonials_query = new WP_Query([
            'post_type'      => 'testimonial',
            'posts_per_page' => 10, // Get up to 10 testimonials
            'orderby'        => 'date',
            'order'          => 'DESC'
        ]);

        if ($testimonials_query->have_posts()) {
            while ($testimonials_query->have_posts()) {
                $testimonials_query->the_post();
                
                $image_url = has_post_thumbnail() 
                    ? get_the_post_thumbnail_url(get_the_ID(), 'thumbnail') 
                    : 'https://ui-avatars.com/api/?name=' . urlencode(get_the_title()) . '&background=0D8ABC&color=fff';

                $testimonials_data[] = [
                    'name'   => get_the_title(),
                    'role'   => get_post_meta(get_the_ID(), 'client_role', true), // From ACF/Custom Field
                    'review' => wp_strip_all_tags(strip_shortcodes(get_the_content())),
                    'rating' => (float) get_post_meta(get_the_ID(), 'rating', true), // From ACF/Custom Field
                    'img'    => $image_url,
                ];
            }
            wp_reset_postdata();
        } else {
            // Fallback to 10 premium reviews if none exist in WP
            $testimonials_data = [
                ['name' => 'Sarah Jenkins', 'role' => 'Marketing Director', 'review' => 'Al Amin completely transformed our organic growth. His technical SEO audit led to a 150% increase in traffic.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Sarah+Jenkins&background=0D8ABC&color=fff'],
                ['name' => 'David Kovic', 'role' => 'Business Owner', 'review' => 'Highly recommended. He is very transparent with the Google Ads budget and scaling strategy.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=David+Kovic&background=0D8ABC&color=fff'],
                ['name' => 'Elena Rodriguez', 'role' => 'E-commerce Manager', 'review' => 'We were struggling with ROAS before working with Al Amin. Achieved a 5X return. Exceptional!', 'rating' => 4.9, 'img' => 'https://ui-avatars.com/api/?name=Elena+Rodriguez&background=0D8ABC&color=fff'],
                ['name' => 'Mark Stevenson', 'role' => 'CEO', 'review' => 'A true professional. He understands the balance between code and marketing. Lead generation has never been better.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Mark+Stevenson&background=0D8ABC&color=fff'],
                ['name' => 'Jessica Chen', 'role' => 'Startup Founder', 'review' => 'His Facebook Ads strategy was spot on. We reached our target audience much faster than anticipated.', 'rating' => 4.8, 'img' => 'https://ui-avatars.com/api/?name=Jessica+Chen&background=0D8ABC&color=fff'],
                ['name' => 'Tom Harrison', 'role' => 'Local Service Provider', 'review' => 'Our local map pack ranking skyrocketed. We get so many more calls now just from Google Maps.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Tom+Harrison&background=0D8ABC&color=fff'],
                ['name' => 'Amanda Phillips', 'role' => 'VP of Growth', 'review' => 'Al Amin is a gem. Detail-oriented, analytical, and constantly optimizing for better results.', 'rating' => 4.9, 'img' => 'https://ui-avatars.com/api/?name=Amanda+Phillips&background=0D8ABC&color=fff'],
                ['name' => 'Chris Norton', 'role' => 'SaaS Founder', 'review' => 'Immediate improvements in our site speed and core web vitals after his technical interventions.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Chris+Norton&background=0D8ABC&color=fff'],
                ['name' => 'Olivia Martinez', 'role' => 'Content Manager', 'review' => 'His keyword intent mapping aligns perfectly with our content strategy. Very easy to collaborate with.', 'rating' => 4.8, 'img' => 'https://ui-avatars.com/api/?name=Olivia+Martinez&background=0D8ABC&color=fff'],
                ['name' => 'James Wilson', 'role' => 'Agency Partner', 'review' => 'We white-label Al Amin\'s services for our own clients. He always over-delivers. Top tier expertise.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=James+Wilson&background=0D8ABC&color=fff'],
            ];
        }
        ?>

        <!-- 9. TESTIMONIALS SLIDER -->
        <section class="py-24 px-6 border-t border-slate-200 dark:border-white/5 relative bg-slate-50 dark:bg-navy-900/10" x-data="testimonialSlider(<?php echo esc_attr(wp_json_encode($testimonials_data)); ?>)" x-init="init()">
            <div class="max-w-7xl mx-auto flex flex-col space-y-12 overflow-hidden">
                
                <div class="text-center flex flex-col space-y-4 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Client Feedback</span>
                    <h2 class="text-3xl md:text-4xl font-extrabold text-slate-900 dark:text-white font-display">Client Success Stories</h2>
                </div>

                <!-- Slider Area -->
                <div class="relative w-full reveal" @mouseenter="pause()" @mouseleave="resume()" @touchstart.passive="touchStart($event)" @touchmove.passive="touchMove($event)" @touchend.passive="touchEnd($event)" @mousedown="touchStart($event)" @mousemove="touchMove($event)" @mouseup="touchEnd($event)" @mouseleave="touchEnd($event)">
                    
                    <!-- Auto-swiping Container -->
                    <div class="flex transition-transform duration-500 ease-out" :style="'transform: translateX(calc(-' + (currentIndex * (100 / visibleCards)) + '% + ' + dragOffset + 'px))'">
                        <template x-for="(testimonial, index) in testimonials" :key="index">
                            
                            <div class="shrink-0 px-3 transition-all duration-500" :style="`width: ${100 / visibleCards}%`">
                                <div class="h-full bg-white dark:bg-navy-800 p-5 sm:p-6 md:p-8 rounded-3xl border border-slate-200 dark:border-white/10 shadow-lg shadow-slate-200/75 dark:shadow-navy-950/50 flex flex-col justify-between space-y-6 hover:-translate-y-1 transition-transform duration-300 cursor-grab active:cursor-grabbing">
                                    <div class="flex items-start justify-between">
                                        <div class="flex flex-col">
                                            <span class="text-3xl sm:text-4xl md:text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-gold to-yellow-400 font-display" x-text="testimonial.rating ? testimonial.rating.toFixed(1) : '10.0'"></span>
                                            <div class="flex items-center space-x-1 text-gold text-xs mt-2">
                                                <template x-for="i in 10"><i class="fa-solid" :class="i <= Math.floor(testimonial.rating) ? 'fa-star text-gold' : (i - testimonial.rating === 0.5 ? 'fa-star-half-stroke text-gold' : 'fa-star text-slate-300 dark:text-slate-600')"></i></template>
                                            </div>
                                        </div>
                                        <span class="text-4xl md:text-5xl text-slate-200 dark:text-white/5"><i class="fa-solid fa-quote-right"></i></span>
                                    </div>
                                    <p class="text-sm md:text-base text-slate-600 dark:text-slate-300 leading-relaxed font-medium" x-html="'&quot;' + testimonial.review + '&quot;'"></p>
                                    <div class="flex items-center space-x-4 pt-4 border-t border-slate-100 dark:border-white/5">
                                        <img :src="testimonial.img" alt="User" class="w-10 h-10 sm:w-12 sm:h-12 rounded-full shadow-md object-cover">
                                        <div class="flex flex-col">
                                            <span class="text-sm font-bold text-slate-900 dark:text-white font-display" x-text="testimonial.name"></span>
                                            <span class="text-[10px] uppercase tracking-wider text-slate-500 dark:text-slate-400" x-text="testimonial.role"></span>
                                        </div>
                                    </div>
                                </div>
                            </div>

                        </template>
                    </div>
                </div>

                <!-- Slider Nav Controls -->
                <div class="flex flex-col md:flex-row items-center justify-between mt-8 gap-6 md:gap-0">
                    <div class="flex space-x-2"><template x-for="(_, index) in Math.max(1, testimonials.length - visibleCards + 1)" :key="index"><button @click="goTo(index)" :class="currentIndex === index ? 'bg-blue-600 w-8' : 'bg-slate-300 dark:bg-slate-700 w-2 hover:bg-blue-400 dark:hover:bg-slate-500'" class="h-2 rounded-full transition-all duration-300"></button></template></div>
                    <div class="flex space-x-3">
                        <button @click="prev()" class="w-10 h-10 rounded-full border border-slate-200 dark:border-white/10 flex items-center justify-center text-slate-600 dark:text-white hover:bg-slate-100 dark:hover:bg-white/5 transition-colors shadow-sm"><i class="fa-solid fa-chevron-left text-xs"></i></button>
                        <button @click="next()" class="w-10 h-10 rounded-full border border-slate-200 dark:border-white/10 flex items-center justify-center text-slate-600 dark:text-white hover:bg-slate-100 dark:hover:bg-white/5 transition-colors shadow-sm"><i class="fa-solid fa-chevron-right text-xs"></i></button>
                    </div>
                </div>
            </div>
        </section>

        <!-- 10. PRICING PACKAGES -->
        <section id="pricing" class="py-24 px-6 bg-navy-900/10 border-t border-white/5">
            <div class="max-w-7xl mx-auto flex flex-col space-y-16">
                
                <div class="text-center max-w-2xl mx-auto flex flex-col space-y-4 reveal">
                    <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Work Agreements</span>
                    <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">Flexible Service Engagements</h2>
                    <p class="text-slate-400 text-sm">Select an agreement structure tailored to your current scale.</p>
                </div>

                <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
                    
                    <!-- Tier 1 -->
                    <div class="glass-panel p-8 rounded-3xl border border-white/5 flex flex-col justify-between h-auto lg:h-[600px] hover:border-white/10 transition-colors reveal">
                        <div class="flex flex-col space-y-6">
                            <div class="flex flex-col space-y-1">
                                <span class="text-xs text-blue-400 font-bold uppercase tracking-wider font-display">Growth Foundation</span>
                                <h3 class="text-2xl font-bold text-white font-display">Starter Plan</h3>
                            </div>
                            <p class="text-sm text-slate-400 leading-relaxed">Suitable for local operations and businesses seeking to establish organic search foundations.</p>
                            <div class="flex items-baseline space-x-1.5">
                                <span class="text-4xl font-extrabold text-white font-display">$149</span>
                                <span class="text-xs text-slate-500">/ project</span>
                            </div>
                            <div class="h-px bg-white/5 my-2"></div>
                            <ul class="flex flex-col space-y-3.5 text-sm text-slate-300">
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Comprehensive On-Page Keyword Targeting (Up to 10 pages)</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Core Site Speed Audit & Correction Roadmap</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Google Business Map Optimization</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Monthly Search Rankings Reporting</span></li>
                            </ul>
                        </div>
                        <a href="#contact" class="w-full mt-8 text-center btn-premium btn-premium-outline">
                            Select Starter Plan
                        </a>
                    </div>

                    <!-- Tier 2 (Highly Recommended) -->
                    <div class="glass-panel p-8 rounded-3xl border-2 border-blue-500/30 flex flex-col justify-between h-auto lg:h-[600px] relative shadow-xl shadow-blue-950/40 reveal mt-6 lg:mt-0">
                        <div class="absolute -top-3.5 left-1/2 -translate-x-1/2 bg-gradient-to-r from-blue-600 to-cyan-600 text-white text-[10px] tracking-widest font-black uppercase px-4 py-1.5 rounded-full shadow border border-blue-400/20 font-display whitespace-nowrap">
                            Recommended Strategy
                        </div>
                        <div class="flex flex-col space-y-6 mt-2">
                            <div class="flex flex-col space-y-1">
                                <span class="text-xs text-cyan font-bold uppercase tracking-wider font-display">Scaling Search & Paid</span>
                                <h3 class="text-2xl font-bold text-white font-display">Professional Growth</h3>
                            </div>
                            <p class="text-sm text-slate-400 leading-relaxed">Designed for established businesses seeking to integrate high-intent search ads with technical SEO.</p>
                            <div class="flex items-baseline space-x-1.5">
                                <span class="text-4xl font-extrabold text-white font-display">$399</span>
                                <span class="text-xs text-slate-500">/ month</span>
                            </div>
                            <div class="h-px bg-white/5 my-2"></div>
                            <ul class="flex flex-col space-y-3.5 text-sm text-slate-300">
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Complete Technical & On-Page SEO (Up to 50 pages)</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Strategic Google PPC & Search Campaign Management</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Ahrefs Competitor Backlink Acquisition Targeting</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Direct Bi-weekly Performance Strategy Calls</span></li>
                            </ul>
                        </div>
                        <a href="#contact" class="w-full mt-8 text-center btn-premium btn-premium-primary">
                            Select Professional Plan
                        </a>
                    </div>

                    <!-- Tier 3 -->
                    <div class="glass-panel p-8 rounded-3xl border border-white/5 flex flex-col justify-between h-auto lg:h-[600px] hover:border-white/10 transition-colors reveal">
                        <div class="flex flex-col space-y-6">
                            <div class="flex flex-col space-y-1">
                                <span class="text-xs text-gold font-bold uppercase tracking-wider font-display font-medium">Full Omnichannel Dominance</span>
                                <h3 class="text-2xl font-bold text-white font-display">Enterprise Scaling</h3>
                            </div>
                            <p class="text-sm text-slate-400 leading-relaxed">Suitable for multi-channel scaling, international campaigns, and high-budget e-commerce platforms.</p>
                            <div class="flex items-baseline space-x-1.5">
                                <span class="text-4xl font-extrabold text-white font-display">Custom</span>
                            </div>
                            <div class="h-px bg-white/5 my-2"></div>
                            <ul class="flex flex-col space-y-3.5 text-sm text-slate-300">
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Omnichannel Strategy (SEO, Search Ads, Meta Paid Ads)</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Custom Landing Page Architecture & Conversion Audits</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Dedicated Growth Reporting Dashboards (GA4 Integration)</span></li>
                                <li class="flex items-center space-x-3"><i class="fa-solid fa-circle-check text-blue-500 text-sm"></i> <span>Priority Daily Direct Communication Channels</span></li>
                            </ul>
                        </div>
                        <a href="#contact" class="w-full mt-8 text-center btn-premium btn-premium-outline">
                            Contact For Enterprise Details
                        </a>
                    </div>

                </div>
            </div>
        </section>

        <!-- 11. CONTACT SECTION -->
        <section id="contact" class="py-24 px-6 border-t border-white/5 relative">
            <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-16">
                
                <!-- Left contact details -->
                <div class="lg:col-span-5 flex flex-col space-y-8 reveal">
                    <div class="flex flex-col space-y-4">
                        <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Ready to Grow?</span>
                        <h2 class="text-3xl md:text-4xl font-extrabold text-white font-display">Let’s Discuss Your Business Metrics</h2>
                        <p class="text-slate-400 text-sm">Schedule a direct strategy audit to identify potential optimization improvements on your channels.</p>
                    </div>

                    <div class="flex flex-col space-y-4">
                        
                        <!-- Contact Detail Card -->
                        <div class="glass-panel p-4 rounded-xl flex items-center space-x-4">
                            <span class="w-10 h-10 rounded-lg bg-blue-600/10 border border-blue-500/20 flex items-center justify-center text-blue-400 text-lg shrink-0">
                                <i class="fa-solid fa-envelope"></i>
                            </span>
                            <div class="flex flex-col">
                                <span class="text-[10px] uppercase tracking-wider text-slate-500 font-display">Direct Email Inquiry</span>
                                <a href="mailto:alamin@example.com" class="text-sm font-bold text-white hover:text-gold transition-colors">alamin@example.com</a>
                            </div>
                        </div>

                        <!-- Contact Detail Card -->
                        <div class="glass-panel p-4 rounded-xl flex items-center space-x-4">
                            <span class="w-10 h-10 rounded-lg bg-emerald-600/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 text-lg shrink-0">
                                <i class="fa-brands fa-whatsapp"></i>
                            </span>
                            <div class="flex flex-col">
                                <span class="text-[10px] uppercase tracking-wider text-slate-500 font-display font-medium">WhatsApp Hotlines</span>
                                <a href="https://wa.me/8801XXXXXXXXX" target="_blank" rel="noopener noreferrer" class="text-sm font-bold text-white hover:text-gold transition-colors">+880 1XXX-XXXXXX</a>
                            </div>
                        </div>

                        <!-- Contact Detail Card -->
                        <div class="glass-panel p-4 rounded-xl flex items-center space-x-4">
                            <span class="w-10 h-10 rounded-lg bg-cyan-600/10 border border-cyan-500/20 flex items-center justify-center text-cyan text-lg shrink-0">
                                <i class="fa-brands fa-linkedin-in"></i>
                            </span>
                            <div class="flex flex-col">
                                <span class="text-[10px] uppercase tracking-wider text-slate-500 font-display">Professional Networking</span>
                                <a href="https://linkedin.com" target="_blank" rel="noopener noreferrer" class="text-sm font-bold text-white hover:text-gold transition-colors font-display">linkedin.com/in/alamin</a>
                            </div>
                        </div>

                    </div>
                </div>

                <!-- Right contact form -->
                <div class="lg:col-span-7 reveal">
                    <form id="contactForm" class="glass-panel p-8 md:p-10 rounded-3xl border border-white/10 flex flex-col space-y-6">
                        <?php wp_nonce_field( 'contact_form_action', 'contact_nonce' ); ?>
                        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <div class="flex flex-col space-y-2">
                                <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold font-display">Your Name</label>
                                <input type="text" name="full_name" placeholder="John Doe" required class="w-full bg-navy-950 border border-white/10 rounded-lg px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-blue-500 transition-colors">
                            </div>
                            <div class="flex flex-col space-y-2">
                                <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold font-display">Business Email</label>
                                <input type="email" name="email" placeholder="john@example.com" required class="w-full bg-navy-950 border border-white/10 rounded-lg px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-blue-500 transition-colors">
                            </div>
                        </div>

                        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <div class="flex flex-col space-y-2">
                                <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold font-display">Your Website URL</label>
                                <input type="text" name="website" placeholder="www.example.com" class="w-full bg-navy-950 border border-white/10 rounded-lg px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-blue-500 transition-colors">
                            </div>
                            <div class="flex flex-col space-y-2">
                                <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold font-display">Target Marketing Goal</label>
                                <select name="service" class="w-full bg-navy-950 border border-white/10 rounded-lg px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-blue-500 transition-colors">
                                    <option>SEO Growth Strategy</option>
                                    <option>Paid Target PPC Google Ads</option>
                                    <option>Facebook Target Social Ads</option>
                                    <option>Administrative Virtual Assistant</option>
                                </select>
                            </div>
                        </div>

                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold font-display">Project Details</label>
                            <textarea name="details" rows="4" placeholder="Briefly describe your objectives and current challenges..." required class="w-full bg-navy-950 border border-white/10 rounded-lg px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-blue-500 transition-colors"></textarea>
                        </div>

                        <button type="submit" class="w-full mt-4 btn-premium btn-premium-primary">
                            Submit Form & Book Meeting
                        </button>
                    </form>
                </div>

            </div>
        </section>
    </main>

    <!-- Contact Form AJAX Script -->
    <script>
        document.getElementById('contactForm')?.addEventListener('submit', async function(e) {
            e.preventDefault();
            const btn = this.querySelector('button[type="submit"]');
            const originalText = btn.innerHTML;
            btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Sending...';
            btn.disabled = true;

            const formData = new FormData(this);
            formData.append('action', 'send_contact_form');
            
            const ajaxurl = '<?php echo esc_url(admin_url("admin-ajax.php")); ?>';

            try {
                const response = await fetch(ajaxurl, { method: 'POST', body: formData });
                const result = await response.json();
                
                if (result.success) {
                    alert('Thank you! Message sent successfully.');
                    this.reset();
                } else {
                    alert('Error: ' + result.data);
                }
            } catch (err) {
                alert('Something went wrong. Please try again.');
            } finally {
                btn.innerHTML = originalText;
                btn.disabled = false;
            }
        });
    </script>

<?php get_footer('portfolio'); ?>
