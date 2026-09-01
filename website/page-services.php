<?php
/**
 * Template Name: Services & Capabilities Page
 * Description: Complete 8+ services grid, in-depth case studies, pricing tiers, and tools.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('portfolio');
?>

<main>
    <!-- Page Header Banner -->
    <section class="pt-16 pb-12 px-4 sm:px-6 border-b border-white/5 relative overflow-hidden" style="background: linear-gradient(175deg, #020617 0%, #0a1628 60%, #050f1f 100%);">
        <div class="max-w-7xl mx-auto">
            <div class="flex items-center gap-2 text-xs text-slate-500 mb-4">
                <a href="<?php echo esc_url(home_url('/')); ?>" class="hover:text-cyan transition-colors">Home</a>
                <i class="fa-solid fa-chevron-right text-[8px]"></i>
                <span class="text-slate-400">Services</span>
            </div>
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Engineered Growth</span>
            <h1 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display mt-2 mb-3">
                Services & Capabilities
            </h1>
            <p class="text-slate-300 max-w-2xl text-sm sm:text-base leading-relaxed">
                Combining deep search engine optimization mechanics with high-performance paid ads management and automated funnel architecture.
            </p>
        </div>
    </section>

    <!-- 1. Full Services Grid (Template Part Include) -->
    <?php get_template_part('section-services'); ?>

    <!-- 2. In-Depth Case Studies -->
    <section id="case-studies" class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5" style="background: rgba(255,255,255,0.01);">
        <div class="max-w-7xl mx-auto flex flex-col space-y-12">
            <div class="text-center max-w-2xl mx-auto flex flex-col space-y-3 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Data & Outcomes</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">In-Depth Campaign Breakdowns</h2>
                <p class="text-slate-400 text-xs sm:text-sm">Real execution paths showing challenge, structural action plan, and direct metrics verified by Google Search Console and Analytics.</p>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                <!-- Case 1 -->
                <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-blue-500/20 flex flex-col justify-between reveal hover:border-cyan/40 transition-all duration-300">
                    <div class="flex flex-col space-y-4">
                        <div class="flex items-center justify-between">
                            <span class="text-xs font-semibold tracking-wider uppercase text-cyan bg-cyan/10 px-3 py-1 rounded-full">SEO Growth</span>
                            <span class="text-xs text-slate-500">B2B SaaS</span>
                        </div>
                        <h3 class="text-base sm:text-lg font-bold text-white font-display">300% Direct Organic Traffic Growth Within 6 Months</h3>
                        <p class="text-xs text-slate-300 leading-relaxed">
                            <strong class="text-white">Challenge:</strong> Client page indices dropped due to duplicate taxonomies and thin-content structures after a platform migration. <br><br>
                            <strong class="text-white">Execution:</strong> Rebuilt global redirection mapping plan, audited indexing signals, and targeted commercial-intent keyword clusters.
                        </p>
                    </div>
                    <div class="pt-5 mt-6 border-t border-white/10 flex items-center justify-between">
                        <span class="text-[10px] tracking-wider uppercase text-slate-400">Direct Metric</span>
                        <span class="text-base font-bold text-emerald-400">+300% Organic Sessions</span>
                    </div>
                </div>

                <!-- Case 2 -->
                <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-blue-500/20 flex flex-col justify-between reveal hover:border-gold/40 transition-all duration-300">
                    <div class="flex flex-col space-y-4">
                        <div class="flex items-center justify-between">
                            <span class="text-xs font-semibold tracking-wider uppercase text-gold bg-amber-500/10 px-3 py-1 rounded-full">Google Ads</span>
                            <span class="text-xs text-slate-500">E-Commerce</span>
                        </div>
                        <h3 class="text-base sm:text-lg font-bold text-white font-display">5X ROAS Optimization & Scaled Sales Conversion</h3>
                        <p class="text-xs text-slate-300 leading-relaxed">
                            <strong class="text-white">Challenge:</strong> Low quality leads driving up average cost-per-acquisition metrics, with limited conversion activity recorded. <br><br>
                            <strong class="text-white">Execution:</strong> Restructured search parameters to target high-intent transactional key phrases while deploying strategic negative keyword match lists.
                        </p>
                    </div>
                    <div class="pt-5 mt-6 border-t border-white/10 flex items-center justify-between">
                        <span class="text-[10px] tracking-wider uppercase text-slate-400">Direct Metric</span>
                        <span class="text-base font-bold text-emerald-400">5.0+ ROAS Verified</span>
                    </div>
                </div>

                <!-- Case 3 -->
                <div class="glass-panel p-6 sm:p-7 rounded-2xl border border-blue-500/20 flex flex-col justify-between reveal hover:border-blue-400/40 transition-all duration-300">
                    <div class="flex flex-col space-y-4">
                        <div class="flex items-center justify-between">
                            <span class="text-xs font-semibold tracking-wider uppercase text-blue-400 bg-blue-600/10 px-3 py-1 rounded-full">Local SEO</span>
                            <span class="text-xs text-slate-500">Service Provider</span>
                        </div>
                        <h3 class="text-base sm:text-lg font-bold text-white font-display">Dominating Local Proximity Search & Lead Generation</h3>
                        <p class="text-xs text-slate-300 leading-relaxed">
                            <strong class="text-white">Challenge:</strong> Minimum local visibility for service queries despite operating in a high-density primary territory. <br><br>
                            <strong class="text-white">Execution:</strong> Completely audited local schema structures, expanded citations, and updated geo-targeted content matching real search patterns.
                        </p>
                    </div>
                    <div class="pt-5 mt-6 border-t border-white/10 flex items-center justify-between">
                        <span class="text-[10px] tracking-wider uppercase text-slate-400">Direct Metric</span>
                        <span class="text-base font-bold text-emerald-400">Top 3 Map Pack Rank</span>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <!-- 3. Flexible Pricing Packages -->
    <section id="pricing" class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5">
        <div class="max-w-7xl mx-auto flex flex-col space-y-12">
            <div class="text-center max-w-2xl mx-auto flex flex-col space-y-3 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Work Agreements</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Flexible Service Engagements</h2>
                <p class="text-slate-400 text-xs sm:text-sm">Select an agreement structure tailored to your current scale and goals.</p>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8 items-stretch">
                <!-- Tier 1 -->
                <div class="glass-panel p-7 sm:p-8 rounded-3xl border border-white/10 flex flex-col justify-between hover:border-white/20 transition-all duration-300 reveal">
                    <div class="flex flex-col space-y-5">
                        <div>
                            <span class="text-xs text-blue-400 font-bold uppercase tracking-wider font-display">Growth Foundation</span>
                            <h3 class="text-2xl font-bold text-white font-display mt-1">Starter Plan</h3>
                        </div>
                        <p class="text-xs sm:text-sm text-slate-300 leading-relaxed">Suitable for local operations and businesses seeking to establish strong search foundations.</p>
                        <div class="flex items-baseline space-x-1.5">
                            <span class="text-4xl font-extrabold text-white font-display">$149</span>
                            <span class="text-xs text-slate-400">/ project</span>
                        </div>
                        <div class="h-px bg-white/10 my-2"></div>
                        <ul class="flex flex-col space-y-3 text-xs sm:text-sm text-slate-300">
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-blue-400 text-xs"></i> <span>On-Page Keyword Targeting (Up to 10 pages)</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-blue-400 text-xs"></i> <span>Core Web Vitals Speed Audit & Fix Roadmap</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-blue-400 text-xs"></i> <span>Google Business Map Pack Optimization</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-blue-400 text-xs"></i> <span>Monthly Search Rankings Reporting</span></li>
                        </ul>
                    </div>
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="w-full mt-8 text-center btn-premium btn-premium-outline">
                        Select Starter Plan
                    </a>
                </div>

                <!-- Tier 2 (Recommended) -->
                <div class="glass-panel p-7 sm:p-8 rounded-3xl border-2 border-cyan/40 flex flex-col justify-between relative shadow-xl shadow-cyan-950/40 reveal">
                    <div class="absolute -top-3.5 left-1/2 -translate-x-1/2 bg-gradient-to-r from-blue-600 via-cyan-500 to-teal-400 text-white text-[10px] tracking-widest font-black uppercase px-4 py-1.5 rounded-full shadow border border-cyan-300/30 font-display whitespace-nowrap">
                        Recommended Strategy
                    </div>
                    <div class="flex flex-col space-y-5 mt-2">
                        <div>
                            <span class="text-xs text-cyan font-bold uppercase tracking-wider font-display">Scaling Search & Paid</span>
                            <h3 class="text-2xl font-bold text-white font-display mt-1">Professional Growth</h3>
                        </div>
                        <p class="text-xs sm:text-sm text-slate-300 leading-relaxed">Designed for established businesses seeking to combine high-intent PPC ads with technical SEO.</p>
                        <div class="flex items-baseline space-x-1.5">
                            <span class="text-4xl font-extrabold text-white font-display">$399</span>
                            <span class="text-xs text-slate-400">/ month</span>
                        </div>
                        <div class="h-px bg-white/10 my-2"></div>
                        <ul class="flex flex-col space-y-3 text-xs sm:text-sm text-slate-300">
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-cyan text-xs"></i> <span>Complete Technical & On-Page SEO (Up to 50 pages)</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-cyan text-xs"></i> <span>Strategic Google PPC & Search Ads Management</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-cyan text-xs"></i> <span>Ahrefs Competitor Backlink Acquisition Targeting</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-cyan text-xs"></i> <span>Bi-weekly Direct Performance Strategy Calls</span></li>
                        </ul>
                    </div>
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="w-full mt-8 text-center btn-premium btn-premium-primary">
                        Select Professional Plan
                    </a>
                </div>

                <!-- Tier 3 -->
                <div class="glass-panel p-7 sm:p-8 rounded-3xl border border-white/10 flex flex-col justify-between hover:border-white/20 transition-all duration-300 reveal">
                    <div class="flex flex-col space-y-5">
                        <div>
                            <span class="text-xs text-gold font-bold uppercase tracking-wider font-display">Full Omnichannel</span>
                            <h3 class="text-2xl font-bold text-white font-display mt-1">Enterprise Scaling</h3>
                        </div>
                        <p class="text-xs sm:text-sm text-slate-300 leading-relaxed">Suitable for multi-channel scaling, international campaigns, and high-budget e-commerce platforms.</p>
                        <div class="flex items-baseline space-x-1.5">
                            <span class="text-4xl font-extrabold text-white font-display">Custom</span>
                        </div>
                        <div class="h-px bg-white/10 my-2"></div>
                        <ul class="flex flex-col space-y-3 text-xs sm:text-sm text-slate-300">
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-gold text-xs"></i> <span>Omnichannel Strategy (SEO, Search Ads, Meta Paid Ads)</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-gold text-xs"></i> <span>Custom Landing Page Architecture & CRO Audits</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-gold text-xs"></i> <span>Dedicated Growth Reporting Dashboards (GA4 Integration)</span></li>
                            <li class="flex items-center space-x-2.5"><i class="fa-solid fa-circle-check text-gold text-xs"></i> <span>Priority Daily Direct Communication Channels</span></li>
                        </ul>
                    </div>
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="w-full mt-8 text-center btn-premium btn-premium-outline">
                        Contact For Enterprise
                    </a>
                </div>
            </div>
        </div>
    </section>

    <!-- 4. Tools & Tech Stack Grid -->
    <section id="tools" class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5" style="background: rgba(255,255,255,0.01);">
        <div class="max-w-7xl mx-auto flex flex-col space-y-12">
            <div class="text-center max-w-2xl mx-auto flex flex-col space-y-3 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">My Toolkit</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Tools & Platforms I Work With</h2>
                <p class="text-slate-400 text-xs sm:text-sm">Precision tools for every phase â€” research, execution, tracking, and reporting.</p>
            </div>

            <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 reveal">
                <?php
                $tools_list = [
                    ['icon' => 'fa-brands fa-google', 'name' => 'Google Search Console', 'color' => 'text-blue-400', 'bg' => 'bg-blue-600/10 border-blue-500/20'],
                    ['icon' => 'fa-brands fa-google', 'name' => 'Google Analytics (GA4)', 'color' => 'text-orange-400', 'bg' => 'bg-orange-600/10 border-orange-500/20'],
                    ['icon' => 'fa-solid fa-chart-line', 'name' => 'Ahrefs', 'color' => 'text-orange-400', 'bg' => 'bg-orange-600/10 border-orange-500/20'],
                    ['icon' => 'fa-solid fa-magnifying-glass-chart', 'name' => 'SEMrush', 'color' => 'text-green-400', 'bg' => 'bg-green-600/10 border-green-500/20'],
                    ['icon' => 'fa-solid fa-frog', 'name' => 'Screaming Frog', 'color' => 'text-green-400', 'bg' => 'bg-green-600/10 border-green-500/20'],
                    ['icon' => 'fa-brands fa-meta', 'name' => 'Meta Ads Manager', 'color' => 'text-blue-400', 'bg' => 'bg-blue-600/10 border-blue-500/20'],
                    ['icon' => 'fa-solid fa-bullhorn', 'name' => 'Google Ads', 'color' => 'text-cyan', 'bg' => 'bg-cyan-600/10 border-cyan-500/20'],
                    ['icon' => 'fa-brands fa-hubspot', 'name' => 'HubSpot CRM', 'color' => 'text-orange-400', 'bg' => 'bg-orange-600/10 border-orange-500/20'],
                    ['icon' => 'fa-solid fa-file-excel', 'name' => 'Google Sheets', 'color' => 'text-green-400', 'bg' => 'bg-green-600/10 border-green-500/20'],
                    ['icon' => 'fa-brands fa-wordpress', 'name' => 'WordPress', 'color' => 'text-blue-400', 'bg' => 'bg-blue-600/10 border-blue-500/20'],
                    ['icon' => 'fa-solid fa-wand-magic-sparkles', 'name' => 'Canva / Figma', 'color' => 'text-pink-400', 'bg' => 'bg-pink-600/10 border-pink-500/20'],
                    ['icon' => 'fa-brands fa-mailchimp', 'name' => 'Mailchimp', 'color' => 'text-yellow-400', 'bg' => 'bg-yellow-600/10 border-yellow-500/20'],
                ];
                foreach ($tools_list as $t) : ?>
                <div class="glass-panel p-4 rounded-xl border <?php echo $t['bg']; ?> flex flex-col items-center gap-2.5 text-center hover:scale-105 transition-all duration-300">
                    <span class="w-10 h-10 rounded-lg flex items-center justify-center <?php echo $t['color']; ?> text-xl border <?php echo $t['bg']; ?>">
                        <i class="<?php echo $t['icon']; ?>"></i>
                    </span>
                    <span class="text-[10px] font-semibold text-slate-300 leading-tight"><?php echo $t['name']; ?></span>
                </div>
                <?php endforeach; ?>
            </div>
        </div>
    </section>

    <!-- Page CTA -->
    <section class="py-20 px-4 sm:px-6 border-t border-white/5" style="background: radial-gradient(ellipse 60% 80% at 50% 50%, rgba(37,99,235,0.12) 0%, transparent 70%);">
        <div class="max-w-3xl mx-auto text-center flex flex-col items-center space-y-6">
            <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Ready for Sustainable Growth?</h2>
            <p class="text-slate-300 text-xs sm:text-sm max-w-xl">Let's build a customized roadmap for your business scale and budget.</p>
            <div class="flex flex-wrap gap-4 justify-center">
                <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary">
                    <i class="fa-solid fa-calendar-check text-xs"></i> Book Strategy Session
                </a>
                <a href="<?php echo esc_url(home_url('/portfolio/')); ?>" class="btn-premium btn-premium-outline">
                    <i class="fa-solid fa-briefcase text-xs"></i> See Past Campaigns
                </a>
            </div>
        </div>
    </section>
</main>

<?php get_footer('portfolio'); ?>