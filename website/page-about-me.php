<?php
/**
 * Template Name: About Me Page
 * Description: Dedicated in-depth biography, technical CS background, skill metrics & certifications.
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
                <span class="text-slate-400">About Me</span>
            </div>
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">The Professional Journey</span>
            <h1 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display mt-2 mb-3">
                About N F Alamin Hossain
            </h1>
            <p class="text-slate-300 max-w-2xl text-sm sm:text-base leading-relaxed">
                Certified SEO Specialist, Google Ads Expert & Growth Strategist with an academic background in Computer Science and 5+ years delivering measurable ROI.
            </p>
        </div>
    </section>

    <!-- 1. Detailed Biography & Portrait -->
    <section class="py-16 md:py-24 px-4 sm:px-6">
        <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-16 items-center">
            
            <div class="lg:col-span-5 relative reveal flex justify-center items-center">
                <div class="relative w-full max-w-[440px] mx-auto">
                    <div class="absolute inset-0 bg-gradient-to-tr from-blue-600/20 to-cyan-500/20 rounded-3xl blur-2xl -z-10"></div>
                    <img src="<?php echo get_template_directory_uri(); ?>/Assets/images/nf011.png" alt="Alamin Hossain" class="w-full h-auto object-contain chair-portrait drop-shadow-2xl">
                </div>
            </div>

            <div class="lg:col-span-7 flex flex-col items-center text-center lg:items-start lg:text-left space-y-6 reveal">
                <h2 class="text-2xl sm:text-3xl font-extrabold text-white font-display leading-snug">
                    Bridging the Gap Between Technical Code & High-Performance Marketing
                </h2>
                
                <p class="text-slate-300 leading-relaxed text-sm">
                    With a solid academic foundation in Computer Science and over 5 years of industry experience, I focus on the structural elements of search engine mechanics and paid advertising systems. I avoid empty vanity metrics, focusing instead on campaign performance that directly impacts your bottom line.
                </p>
                <p class="text-slate-300 leading-relaxed text-sm">
                    As a certified marketer, my expertise includes technical SEO audits, ROI-driven PPC execution, and social audience targeting. I create search engine optimization strategies and user-experience frameworks that scale sustainably.
                </p>

                <!-- Core Pillars -->
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2 w-full">
                    <div class="glass-panel p-4 rounded-xl flex items-start gap-3 border border-white/10 text-left">
                        <span class="w-9 h-9 rounded-lg bg-blue-600/10 flex items-center justify-center text-blue-400 border border-blue-500/20 shrink-0">
                            <i class="fa-solid fa-shield-halved text-sm"></i>
                        </span>
                        <div>
                            <h4 class="text-sm font-bold text-white">White-Hat Strategy</h4>
                            <p class="text-xs text-slate-400 mt-1">Strict adherence to search engine quality guidelines for long-term algorithmic resilience.</p>
                        </div>
                    </div>

                    <div class="glass-panel p-4 rounded-xl flex items-start gap-3 border border-white/10 text-left">
                        <span class="w-9 h-9 rounded-lg bg-amber-500/10 flex items-center justify-center text-gold border border-amber-500/20 shrink-0">
                            <i class="fa-solid fa-code text-sm"></i>
                        </span>
                        <div>
                            <h4 class="text-sm font-bold text-white">Technical Background</h4>
                            <p class="text-xs text-slate-400 mt-1">Computer Science training enables deep Core Web Vitals audits and custom indexation architectures.</p>
                        </div>
                    </div>

                    <div class="glass-panel p-4 rounded-xl flex items-start gap-3 border border-white/10 text-left">
                        <span class="w-9 h-9 rounded-lg bg-cyan-600/10 flex items-center justify-center text-cyan border border-cyan-500/20 shrink-0">
                            <i class="fa-solid fa-chart-line text-sm"></i>
                        </span>
                        <div>
                            <h4 class="text-sm font-bold text-white">Data-Driven Execution</h4>
                            <p class="text-xs text-slate-400 mt-1">Real analytics data drives conversion rate optimization and builds predictable revenue pipelines.</p>
                        </div>
                    </div>

                    <div class="glass-panel p-4 rounded-xl flex items-start gap-3 border border-white/10 text-left">
                        <span class="w-9 h-9 rounded-lg bg-emerald-600/10 flex items-center justify-center text-emerald-400 border border-emerald-500/20 shrink-0">
                            <i class="fa-solid fa-globe text-sm"></i>
                        </span>
                        <div>
                            <h4 class="text-sm font-bold text-white">Global Deployment</h4>
                            <p class="text-xs text-slate-400 mt-1">Proven experience running multi-channel campaigns for firms across USA, UK, UAE, and APAC regions.</p>
                        </div>
                    </div>
                </div>

                <div class="flex flex-wrap gap-4 pt-2">
                    <a href="<?php echo function_exists('edm_get_cv_url') ? edm_get_cv_url() : get_template_directory_uri() . '/downloads/Alamin-Hossain-CV.pdf'; ?>" class="btn-premium btn-premium-primary" download="Alamin-Hossain-CV.pdf">
                        <i class="fa-solid fa-download text-xs"></i> Download Verified CV
                    </a>
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-outline">
                        <i class="fa-solid fa-envelope text-xs"></i> Contact & Consult
                    </a>
                </div>
            </div>
        </div>
    </section>

    <!-- 2. Technical Competency Matrix & Progress Bars -->
    <section id="skills" class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5" style="background: rgba(255,255,255,0.01);">
        <div class="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-16 items-center">
            <div class="lg:col-span-5 flex flex-col space-y-5 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Technical Toolset</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">
                    Core Marketing Competency Matrix
                </h2>
                <p class="text-slate-300 text-sm leading-relaxed">
                    My methodology is rooted in rigorous testing and continuous performance tracking. I keep track of search algorithm developments and conversion optimization structures so your campaigns remain competitive.
                </p>
                <div class="flex flex-wrap gap-2 pt-2">
                    <?php 
                    $tech_tags = ['Ahrefs', 'SEMrush', 'Screaming Frog', 'Google Search Console', 'Google Analytics (GA4)', 'Google Ads', 'Meta Ads Manager', 'HubSpot CRM', 'WordPress', 'Google Sheets'];
                    foreach ($tech_tags as $tag) : ?>
                    <span class="px-3 py-1 bg-white/5 border border-white/10 rounded-lg text-xs text-slate-300 font-medium"><?php echo esc_html($tag); ?></span>
                    <?php endforeach; ?>
                </div>
            </div>

            <div class="lg:col-span-7 space-y-5 glass-panel p-6 sm:p-8 rounded-2xl border border-white/10 reveal">
                <?php
                $skills_list = [
                    ['name' => 'Search Engine Optimization (On-Page, Off-Page, Tech)', 'pct' => 95],
                    ['name' => 'Google PPC Ads Campaign Management', 'pct' => 90],
                    ['name' => 'Facebook & Meta Paid Target Ads', 'pct' => 88],
                    ['name' => 'Audience Targeting & Keywords Intent Mapping', 'pct' => 93],
                    ['name' => 'Lead Generation & Sales Pipelines', 'pct' => 90],
                    ['name' => 'Virtual Assistant Operations & Advanced Workflows', 'pct' => 92],
                ];
                foreach ($skills_list as $skill) : ?>
                <div class="flex flex-col space-y-2">
                    <div class="flex justify-between items-center">
                        <span class="text-xs sm:text-sm font-bold text-white font-display"><?php echo esc_html($skill['name']); ?></span>
                        <span class="text-xs font-semibold text-cyan font-mono"><?php echo $skill['pct']; ?>%</span>
                    </div>
                    <div class="w-full h-2 bg-white/5 rounded-full overflow-hidden">
                        <div class="h-full bg-gradient-to-r from-blue-500 to-cyan-400 rounded-full skill-fill" data-pct="<?php echo $skill['pct']; ?>" style="width: <?php echo $skill['pct']; ?>%;"></div>
                    </div>
                </div>
                <?php endforeach; ?>
            </div>
        </div>
    </section>

    <!-- 3. Professional Certifications -->
    <section class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5">
        <div class="max-w-7xl mx-auto flex flex-col space-y-12">
            <div class="text-center max-w-xl mx-auto flex flex-col space-y-3 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Validated Knowledge</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Professional Certifications</h2>
                <p class="text-slate-400 text-xs sm:text-sm">Verified credentials from leading digital authorities.</p>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-6 max-w-4xl mx-auto w-full">
                <!-- Cert 1 -->
                <div class="glass-panel p-6 sm:p-8 rounded-2xl flex items-start gap-5 border border-white/10 group hover:border-blue-500/30 transition-all duration-300 reveal">
                    <div class="w-14 h-14 rounded-xl bg-orange-600/15 flex items-center justify-center text-orange-400 text-2xl border border-orange-500/20 shrink-0">
                        <i class="fa-brands fa-hubspot"></i>
                    </div>
                    <div class="flex flex-col gap-2">
                        <span class="text-[10px] text-orange-400 font-mono tracking-widest uppercase">HubSpot Academy</span>
                        <h3 class="text-base sm:text-lg font-bold text-white group-hover:text-gold transition-colors font-display">Content Marketing Certification</h3>
                        <p class="text-xs text-slate-400 leading-relaxed">Advanced organic growth tactics, customer journey creation, and funnel content development.</p>
                        <span class="text-[10px] text-slate-500 font-mono">Issued May 2024 &bull; Credentials Verified</span>
                    </div>
                </div>

                <!-- Cert 2 -->
                <div class="glass-panel p-6 sm:p-8 rounded-2xl flex items-start gap-5 border border-white/10 group hover:border-blue-500/30 transition-all duration-300 reveal">
                    <div class="w-14 h-14 rounded-xl bg-blue-600/15 flex items-center justify-center text-blue-400 text-2xl border border-blue-500/20 shrink-0">
                        <i class="fa-brands fa-google"></i>
                    </div>
                    <div class="flex flex-col gap-2">
                        <span class="text-[10px] text-blue-400 font-mono tracking-widest uppercase">Google Digital</span>
                        <h3 class="text-base sm:text-lg font-bold text-white group-hover:text-gold transition-colors font-display">Digital Marketing & E-Commerce Certificate</h3>
                        <p class="text-xs text-slate-400 leading-relaxed">Comprehensive management covering SEM, Performance Max systems, display networks, and UX frameworks.</p>
                        <span class="text-[10px] text-slate-500 font-mono">Issued August 2023 &bull; Credentials Verified</span>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <!-- Page CTA -->
    <section class="py-20 px-4 sm:px-6 border-t border-white/5" style="background: radial-gradient(ellipse 60% 80% at 50% 50%, rgba(37,99,235,0.12) 0%, transparent 70%);">
        <div class="max-w-3xl mx-auto text-center flex flex-col items-center space-y-6">
            <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display">Ready to Work Together?</h2>
            <p class="text-slate-300 text-xs sm:text-sm max-w-xl">Let's discuss how my technical expertise can accelerate your business objectives.</p>
            <div class="flex flex-wrap gap-4 justify-center">
                <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary">
                    <i class="fa-solid fa-calendar-check text-xs"></i> Book Strategy Call
                </a>
                <a href="<?php echo esc_url(home_url('/services/')); ?>" class="btn-premium btn-premium-outline">
                    <i class="fa-solid fa-layer-group text-xs"></i> View Full Services
                </a>
            </div>
        </div>
    </section>
</main>

<?php get_footer('portfolio'); ?>