<?php
/**
 * Streamlined Master Portfolio Homepage (N F Alamin Hossain)
 *
 * Designed with a clean, focused hierarchy:
 * 1. Hero Section (Sitting Chair Portrait, Headline, Typing Effect, Counters)
 * 2. Trust & Rating Bar (5.0 Stars, Google/HubSpot/Meta/Upwork Verified)
 * 3. Services Preview (3 High-Impact Cards + Link to /services/)
 * 4. Testimonials Strip (3 Client Reviews + Link to /results/)
 * 5. Final CTA Strip (Direct Strategy Booking)
 *
 * All other deep-dive sections are organized in dedicated subpages:
 * - /about-me/
 * - /services/
 * - /portfolio/
 * - /results/
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('portfolio');

$show_hero = function_exists('edm_get_mod') ? edm_get_mod('edm_show_hero', true) : true;
$show_trust = function_exists('edm_get_mod') ? edm_get_mod('edm_show_trust', true) : true;
$show_services = function_exists('edm_get_mod') ? edm_get_mod('edm_show_services_preview', true) : true;
$show_testimonials = function_exists('edm_get_mod') ? edm_get_mod('edm_show_testimonials_preview', true) : true;
$show_cta = function_exists('edm_get_mod') ? edm_get_mod('edm_show_cta_strip', true) : true;

$badge_text = function_exists('edm_get_mod') ? edm_get_mod('edm_hero_badge_text', 'Available for Projects') : 'Available for Projects';
$hero_desc = function_exists('edm_get_mod') ? edm_get_mod('edm_hero_desc', 'I build transparent, highly-optimized campaigns designed to maximize traffic, generate qualified leads, and grow revenue as a certified SEO Specialist, Google Ads Expert, and Social Media Marketing Strategist.') : 'I build transparent, highly-optimized campaigns designed to maximize traffic, generate qualified leads, and grow revenue as a certified SEO Specialist, Google Ads Expert, and Social Media Marketing Strategist.';
?>

    <main>
        <?php if ($show_hero) : ?>
        <!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
             1. HERO SECTION (Chair Portrait, Headline, Typing & Counters)
             â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
        <section class="hero-section min-h-[calc(100vh-3.5rem)] flex items-center relative py-10 lg:py-16 px-4 sm:px-6 overflow-hidden z-10" style="background: radial-gradient(ellipse 80% 60% at 50% -10%, rgba(37,99,235,0.22) 0%, transparent 65%), radial-gradient(ellipse 60% 40% at 90% 30%, rgba(6,182,212,0.14) 0%, transparent 55%), linear-gradient(175deg, #020617 0%, #0a1628 40%, #050f1f 100%);">
            <div class="absolute inset-0 hero-grid-lines pointer-events-none -z-10"></div>
            
            <!-- Ambient Glow Orbs -->
            <div class="absolute top-0 left-1/4 w-96 h-96 rounded-full pointer-events-none -z-10" style="background: radial-gradient(circle, rgba(37,99,235,0.12) 0%, transparent 70%); filter: blur(40px);"></div>
            <div class="absolute bottom-0 right-1/4 w-80 h-80 rounded-full pointer-events-none -z-10" style="background: radial-gradient(circle, rgba(6,182,212,0.10) 0%, transparent 70%); filter: blur(50px);"></div>

            <div class="max-w-7xl mx-auto w-full grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
                
                <!-- Left Details & Headlines -->
                <div class="lg:col-span-7 flex flex-col space-y-5 items-center text-center lg:items-start lg:text-left hero-fade-in-up">
                    <div class="hero-badge inline-flex items-center space-x-2 px-3.5 py-1 rounded-full w-fit bg-blue-900/30 border border-blue-500/20">
                        <span class="hero-badge-dot w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                        <span class="hero-badge-text text-[11px] tracking-wider uppercase font-semibold text-emerald-300"><?php echo esc_html($badge_text); ?></span>
                    </div>

                    <h1 class="text-3xl sm:text-4xl md:text-5xl lg:text-6xl font-extrabold tracking-tight leading-tight text-white font-display hero-fade-in-up hero-delay-1">
                        <span class="text-gradient-cyan">Accelerating</span> 
                        <span class="relative inline-block">
                            <span class="text-gradient-cyan">Business</span>
                            <svg class="absolute left-0 w-full h-3 sm:h-4 -bottom-1 pointer-events-none text-gold" viewBox="0 0 100 20" preserveAspectRatio="none" fill="none">
                                <path d="M 1 14 C 20 2 60 5 99 16" stroke="currentColor" stroke-width="4" stroke-linecap="round" />
                            </svg>
                        </span> 
                        <span class="text-gradient-cyan">Growth</span> <br>
                        <span class="text-slate-400">Through Custom</span> <br>
                        <!-- Dynamic Typing Effect -->
                        <span id="typing-text" class="text-gradient-gold"></span><span class="text-gold animate-pulse">|</span>
                    </h1>

                    <p class="hero-description text-sm sm:text-base md:text-lg max-w-xl leading-relaxed text-slate-300 mx-auto lg:mx-0 hero-fade-in-up hero-delay-2">
                        <?php echo esc_html($hero_desc); ?>
                    </p>

                    <!-- CTAs -->
                    <div class="flex flex-wrap items-center justify-center lg:justify-start gap-4 pt-2 w-full hero-fade-in-up hero-delay-3">
                        <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary">
                            <i class="fa-solid fa-calendar-check text-xs"></i> Book Free Strategy Session
                        </a>
                        <a href="<?php echo esc_url(home_url('/about-me/')); ?>" class="btn-premium btn-premium-outline">
                            <i class="fa-solid fa-user text-xs"></i> About Me
                        </a>
                    </div>

                    <!-- Verified Performance Counters -->
                    <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 pt-8 border-t border-white/10 w-full">
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-2xl sm:text-3xl font-extrabold text-white font-display stat-num" data-target="100">0</span>
                            <span class="hero-stat-label text-[10px] tracking-wider uppercase text-slate-400 mt-0.5">Projects Delivered</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-2xl sm:text-3xl font-extrabold text-white font-display stat-num" data-target="95">0</span>
                            <span class="hero-stat-label text-[10px] tracking-wider uppercase text-slate-400 mt-0.5">Satisfaction Rate %</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-2xl sm:text-3xl font-extrabold text-white font-display stat-num" data-target="5">0</span>
                            <span class="hero-stat-label text-[10px] tracking-wider uppercase text-slate-400 mt-0.5">Years Experience</span>
                        </div>
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <span class="text-2xl sm:text-3xl font-extrabold text-white font-display stat-num" data-target="50">0</span>
                            <span class="hero-stat-label text-[10px] tracking-wider uppercase text-slate-400 mt-0.5">Businesses Scaled</span>
                        </div>
                    </div>
                </div>

                <!-- Right Visual (Portrait with Sitting Pose & Interactive Badges) -->
                <div class="lg:col-span-5 flex justify-center relative items-end w-full">
                    <div class="absolute inset-0 bg-gradient-to-tr from-blue-600/20 to-gold/20 rounded-2xl blur-3xl -z-10"></div>
                
                    <div class="relative w-[80vw] h-[80vw] max-w-[280px] max-h-[280px] sm:max-w-[340px] sm:max-h-[340px] md:max-w-[400px] md:max-h-[400px] mt-8 lg:mt-0 mx-auto aspect-square">
                
                        <!-- Background Cyan Ring -->
                        <div class="absolute bottom-0 w-full h-full rounded-full border-[4px] premium-circle z-10"></div>
                
                        <!-- Pop-out Image Wrapper with Portrait -->
                        <div class="absolute bottom-0 w-full h-full z-30 pointer-events-none" style="clip-path: inset(-100% 0 0 0 round 0 0 50% 50%);">
                            <img src="<?php echo get_template_directory_uri(); ?>/nf.png"
                                 class="hero-photo absolute bottom-0 left-1/2 -translate-x-[62%] w-[380px] sm:w-[460px] md:w-[560px] max-w-none drop-shadow-2xl hero-photo-premium"
                                 alt="Alamin Hossain - Growth Specialist">
                        </div>
                
                        <!-- Floating KPI Badges -->
                        <div class="absolute top-4 -left-2 sm:-left-6 glass-panel p-2.5 rounded-xl border border-white/10 flex items-center space-x-2.5 z-40 animate-bounce" style="animation-duration: 4s;">
                            <span class="text-xl">ðŸ“ˆ</span>
                            <div class="flex flex-col text-left">
                                <span class="text-xs font-bold text-white leading-none">+300%</span>
                                <span class="text-[9px] text-slate-400">Organic Growth</span>
                            </div>
                        </div>
                
                        <div class="absolute bottom-6 -right-2 sm:-right-6 glass-panel p-2.5 rounded-xl border border-white/10 flex items-center space-x-2.5 z-40 animate-bounce" style="animation-duration: 5s;">
                            <span class="text-xl">ðŸŽ¯</span>
                            <div class="flex flex-col text-left">
                                <span class="text-xs font-bold text-white leading-none">5X ROAS</span>
                                <span class="text-[9px] text-slate-400">PPC Performance</span>
                            </div>
                        </div>
                    </div>
                </div>

            </div>
        </section>
        <?php endif; ?>

        <?php if ($show_trust) : ?>
        <!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
             2. TRUST & CREDENTIALS BAR
             â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
        <section class="py-8 px-4 sm:px-6" style="background: linear-gradient(180deg, rgba(37,99,235,0.06) 0%, transparent 100%); border-top: 1px solid rgba(37,99,235,0.15); border-bottom: 1px solid rgba(255,255,255,0.05);">
            <div class="max-w-7xl mx-auto flex flex-col lg:flex-row items-center justify-between gap-6">
                
                <!-- Rating Info -->
                <div class="flex flex-col sm:flex-row items-center gap-3 text-center sm:text-left">
                    <div class="flex items-center space-x-1 text-amber-400 text-base">
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                        <i class="fa-solid fa-star"></i>
                    </div>
                    <div>
                        <p class="text-xs font-bold text-white uppercase tracking-wider">5.0 / 5.0 Rating</p>
                        <p class="text-[11px] text-slate-400">Across 50+ verified client engagements</p>
                    </div>
                </div>

                <!-- Verified Badges / Logos -->
                <div class="flex items-center gap-5 sm:gap-8 flex-wrap justify-center">
                    <span class="text-slate-500 text-[10px] tracking-widest uppercase font-semibold">Certified by</span>
                    <span class="text-slate-300 font-bold text-xs flex items-center gap-1.5"><i class="fa-brands fa-google text-blue-400"></i> Google</span>
                    <span class="text-slate-300 font-bold text-xs flex items-center gap-1.5"><i class="fa-brands fa-hubspot text-orange-400"></i> HubSpot</span>
                    <span class="text-slate-300 font-bold text-xs flex items-center gap-1.5"><i class="fa-brands fa-meta text-blue-400"></i> Meta</span>
                    <span class="text-slate-300 font-bold text-xs flex items-center gap-1.5"><i class="fa-brands fa-upwork text-emerald-400"></i> Upwork</span>
                </div>
            </div>
        </section>
        <?php endif; ?>

        <?php if ($show_services) : ?>
        <!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
             3. SERVICES PREVIEW (3 Key Pillars + Link to Full Page)
             â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
        <section id="services-preview" class="py-16 md:py-20 px-4 sm:px-6">
            <div class="max-w-7xl mx-auto flex flex-col space-y-12">
                <div class="flex flex-col md:flex-row items-start md:items-end justify-between gap-4">
                    <div class="flex flex-col space-y-2">
                        <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Core Capabilities</span>
                        <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">What I Do Best</h2>
                        <p class="text-slate-400 text-xs sm:text-sm max-w-xl">Focused on search mechanics, paid acquisition systems, and high-conversion funnels.</p>
                    </div>
                    <a href="<?php echo esc_url(home_url('/services/')); ?>" class="inline-flex items-center gap-1.5 text-xs font-bold text-cyan tracking-wider uppercase hover:text-cyan-light transition-colors whitespace-nowrap group">
                        <span>View All 8+ Services</span> 
                        <i class="fa-solid fa-arrow-right text-[10px] group-hover:translate-x-1 transition-transform"></i>
                    </a>
                </div>

                <!-- 3 Key Cards -->
                <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                    <!-- Service Card 1 -->
                    <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-white/10 flex flex-col justify-between gap-5 hover:border-cyan/30 hover:-translate-y-1 transition-all duration-300 group reveal">
                        <div class="flex flex-col space-y-3">
                            <span class="w-12 h-12 rounded-xl bg-cyan/10 border border-cyan/20 flex items-center justify-center text-cyan text-2xl group-hover:scale-110 transition-transform">
                                <i class="fa-solid fa-magnifying-glass-chart"></i>
                            </span>
                            <h3 class="text-base font-bold text-white font-display group-hover:text-gold transition-colors">Technical SEO & Audits</h3>
                            <p class="text-xs text-slate-400 leading-relaxed">
                                Complete crawler audit, site speed optimization, schema architecture, and canonical indexation maps that withstand search algorithm updates.
                            </p>
                        </div>
                        <a href="<?php echo esc_url(home_url('/services/')); ?>" class="text-[11px] uppercase tracking-wider font-bold text-cyan hover:text-white transition-colors inline-flex items-center gap-1">
                            Explore SEO Solutions &rarr;
                        </a>
                    </div>

                    <!-- Service Card 2 -->
                    <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-white/10 flex flex-col justify-between gap-5 hover:border-gold/30 hover:-translate-y-1 transition-all duration-300 group reveal">
                        <div class="flex flex-col space-y-3">
                            <span class="w-12 h-12 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-center text-gold text-2xl group-hover:scale-110 transition-transform">
                                <i class="fa-solid fa-bullhorn"></i>
                            </span>
                            <h3 class="text-base font-bold text-white font-display group-hover:text-gold transition-colors">Google & Meta Ads PPC</h3>
                            <p class="text-xs text-slate-400 leading-relaxed">
                                High-intent search campaigns, conversion tracking setup, strategic negative matching, and retargeting funnels that consistently average 5X ROAS.
                            </p>
                        </div>
                        <a href="<?php echo esc_url(home_url('/services/')); ?>" class="text-[11px] uppercase tracking-wider font-bold text-gold hover:text-white transition-colors inline-flex items-center gap-1">
                            Explore PPC Management &rarr;
                        </a>
                    </div>

                    <!-- Service Card 3 -->
                    <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-white/10 flex flex-col justify-between gap-5 hover:border-blue-500/30 hover:-translate-y-1 transition-all duration-300 group reveal">
                        <div class="flex flex-col space-y-3">
                            <span class="w-12 h-12 rounded-xl bg-blue-600/10 border border-blue-500/20 flex items-center justify-center text-blue-400 text-2xl group-hover:scale-110 transition-transform">
                                <i class="fa-solid fa-chart-pie"></i>
                            </span>
                            <h3 class="text-base font-bold text-white font-display group-hover:text-gold transition-colors">Lead Gen & Conversion Funnels</h3>
                            <p class="text-xs text-slate-400 leading-relaxed">
                                Multi-channel audience targeting, automated lead pipelines, custom landing page architectures, and analytics integration to convert browsers into clients.
                            </p>
                        </div>
                        <a href="<?php echo esc_url(home_url('/services/')); ?>" class="text-[11px] uppercase tracking-wider font-bold text-blue-400 hover:text-white transition-colors inline-flex items-center gap-1">
                            Explore Lead Generation &rarr;
                        </a>
                    </div>
                </div>
            </div>
        </section>
        <?php endif; ?>

        <?php if ($show_testimonials) : ?>
        <!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
             4. TESTIMONIALS STRIP (Verified Client Reviews)
             â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
        <section class="py-16 md:py-20 px-4 sm:px-6 border-t border-white/5" style="background: linear-gradient(135deg, rgba(37,99,235,0.06) 0%, rgba(6,182,212,0.03) 100%);">
            <div class="max-w-7xl mx-auto flex flex-col space-y-12">
                <div class="flex flex-col md:flex-row items-start md:items-end justify-between gap-4">
                    <div class="flex flex-col space-y-2">
                        <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Client Feedback</span>
                        <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">What Clients Say</h2>
                        <p class="text-slate-400 text-xs sm:text-sm max-w-xl">Direct quotes from business leaders and founders who scaled with my campaigns.</p>
                    </div>
                    <a href="<?php echo esc_url(home_url('/results/')); ?>" class="inline-flex items-center gap-1.5 text-xs font-bold text-cyan tracking-wider uppercase hover:text-cyan-light transition-colors whitespace-nowrap group">
                        <span>See All Results & FAQ</span> 
                        <i class="fa-solid fa-arrow-right text-[10px] group-hover:translate-x-1 transition-transform"></i>
                    </a>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                    <?php
                    $home_reviews = [
                        ['name' => 'Sarah Jenkins', 'role' => 'Marketing Director', 'review' => 'Al Amin completely transformed our organic growth. His technical SEO audit led to a 150% increase in traffic within 3 months.', 'rating' => 5],
                        ['name' => 'David Kovic', 'role' => 'Business Owner', 'review' => 'Highly recommended. He is very transparent with the Google Ads budget and scaling strategy. Immediate ROI improvement.', 'rating' => 5],
                        ['name' => 'Elena Rodriguez', 'role' => 'E-commerce Manager', 'review' => 'We were struggling with ROAS before working with Al Amin. Achieved a verified 5X return within 2 months. Exceptional!', 'rating' => 5],
                    ];
                    foreach ($home_reviews as $r) : ?>
                    <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-white/10 flex flex-col justify-between gap-5 reveal hover:border-white/20 transition-all duration-300">
                        <div>
                            <div class="flex items-center gap-1 text-amber-400 text-xs mb-3">
                                <?php for ($i = 0; $i < $r['rating']; $i++) : ?>
                                    <i class="fa-solid fa-star"></i>
                                <?php endfor; ?>
                            </div>
                            <p class="text-xs sm:text-sm text-slate-300 leading-relaxed italic">
                                &ldquo;<?php echo esc_html($r['review']); ?>&rdquo;
                            </p>
                        </div>
                        <div class="flex items-center gap-3 pt-4 border-t border-white/5">
                            <span class="w-9 h-9 rounded-full bg-gradient-to-br from-blue-600 to-cyan-500 flex items-center justify-center text-white font-bold text-xs shadow">
                                <?php echo esc_html(substr($r['name'], 0, 1)); ?>
                            </span>
                            <div>
                                <p class="text-xs font-bold text-white"><?php echo esc_html($r['name']); ?></p>
                                <p class="text-[10px] text-slate-400 uppercase tracking-wider"><?php echo esc_html($r['role']); ?></p>
                            </div>
                        </div>
                    </div>
                    <?php endforeach; ?>
                </div>
            </div>
        </section>
        <?php endif; ?>

        <?php if ($show_cta) : ?>
        <!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
             5. FINAL HIGH-CONVERSION CTA STRIP
             â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
        <section class="py-20 md:py-28 px-4 sm:px-6 border-t border-white/5 relative overflow-hidden">
            <div class="absolute inset-0 pointer-events-none" style="background: radial-gradient(ellipse 60% 80% at 50% 50%, rgba(37,99,235,0.14) 0%, transparent 70%);"></div>
            
            <div class="max-w-3xl mx-auto text-center flex flex-col items-center space-y-6 relative z-10">
                <div class="w-14 h-14 rounded-2xl bg-gradient-to-br from-blue-600 via-cyan-500 to-teal-400 flex items-center justify-center text-white text-2xl shadow-xl shadow-cyan-500/20 border border-white/20">
                    <i class="fa-solid fa-rocket"></i>
                </div>
                
                <div class="flex flex-col space-y-3">
                    <h2 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display leading-tight">
                        Ready to Scale Your <br>
                        <span class="text-gradient-cyan">Digital Presence?</span>
                    </h2>
                    <p class="text-slate-300 text-xs sm:text-sm md:text-base max-w-xl mx-auto leading-relaxed">
                        Schedule a free 30-minute strategy session. I'll audit your current search visibility & ads performance and provide an actionable growth blueprint â€” with zero obligation.
                    </p>
                </div>

                <div class="flex flex-wrap items-center justify-center gap-4 pt-2">
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary">
                        <i class="fa-solid fa-calendar-check text-xs"></i> Book Free Strategy Session
                    </a>
                    <a href="<?php echo esc_url(home_url('/portfolio/')); ?>" class="btn-premium btn-premium-outline">
                        <i class="fa-solid fa-briefcase text-xs"></i> View Portfolio Work
                    </a>
                </div>

                <p class="text-[11px] text-slate-500 pt-2">
                    âœ“ No credit card required &bull; âœ“ 30-minute audit call &bull; âœ“ Response within 24 hours
                </p>
            </div>
        </section>
        <?php endif; ?>

    </main>

<?php get_footer('portfolio'); ?>