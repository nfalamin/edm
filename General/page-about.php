<?php
/**
 * Template Name: About Me & Engineering Masterplan - Alamin Hossain
 * Description: In-depth technical biography, computer science background, engineering philosophy, and 5-year strategic masterplan.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-24 pb-24 px-4 sm:px-6 relative overflow-hidden">
    <!-- Ambient Background Blobs -->
    <div class="absolute top-10 left-1/4 w-[600px] h-[600px] bg-blue-600/10 rounded-full blur-[140px] pointer-events-none -z-10"></div>
    <div class="absolute top-1/2 right-10 w-[500px] h-[500px] bg-cyan-600/10 rounded-full blur-[150px] pointer-events-none -z-10"></div>

    <div class="w-full max-w-[96%] 2xl:max-w-[1820px] mx-auto flex flex-col space-y-20">
        
        <!-- ══════════════════════════════════════════════════════════
             1. HERO & EXECUTIVE INTRODUCTION
             ══════════════════════════════════════════════════════════ -->
        <section class="flex flex-col space-y-6">
            <div class="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-blue-600/10 border border-blue-500/20 w-fit">
                <span class="w-2 h-2 rounded-full bg-cyan animate-pulse"></span>
                <span class="text-xs font-mono uppercase tracking-widest text-cyan font-bold">Systems Architect · Software Engineer · Growth Specialist</span>
            </div>

            <h1 class="text-3xl sm:text-5xl lg:text-6xl font-extrabold text-white font-display tracking-tight leading-tight">
                Architecting High-Performance Software <br>
                <span class="bg-gradient-to-r from-blue-400 via-cyan-300 to-indigo-400 bg-clip-text text-transparent">
                    & Engineering Data-Driven Business Growth
                </span>
            </h1>

            <p class="text-slate-300 text-base sm:text-lg max-w-4xl leading-relaxed">
                I am <strong>Alamin Hossain</strong>, a software developer, technical SEO specialist, and digital growth strategist with an academic foundation in Computer Science and over 5 years of full-lifecycle engineering experience. My work unites low-latency native systems programming with sophisticated search algorithms and high-conversion marketing funnels.
            </p>
        </section>

        <!-- ══════════════════════════════════════════════════════════
             2. PORTRAIT & CORE EXECUTIVE OVERVIEW
             ══════════════════════════════════════════════════════════ -->
        <section class="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-16 items-center">
            
            <!-- Left Portrait & Verified Credentials -->
            <div class="lg:col-span-5 flex flex-col items-center">
                <div class="relative w-full max-w-[420px] mx-auto">
                    <div class="absolute inset-0 bg-gradient-to-tr from-blue-600/20 to-cyan-500/20 rounded-3xl blur-2xl -z-10"></div>
                    <img src="<?php echo function_exists('portfolio_get_profile_image') ? portfolio_get_profile_image() : get_template_directory_uri() . '/Assets/images/nf011.png'; ?>" alt="Alamin Hossain - Software Architect" class="w-full h-auto object-contain drop-shadow-2xl rounded-2xl border border-white/10">
                </div>

                <div class="grid grid-cols-2 gap-4 w-full max-w-[420px] mt-6">
                    <div class="glass-panel p-4 rounded-xl border border-white/10 text-center">
                        <span class="text-2xl font-extrabold text-white font-display">5+ Years</span>
                        <span class="block text-[11px] text-slate-400 uppercase tracking-wider mt-0.5">Industry Experience</span>
                    </div>
                    <div class="glass-panel p-4 rounded-xl border border-white/10 text-center">
                        <span class="text-2xl font-extrabold text-cyan font-display">100+</span>
                        <span class="block text-[11px] text-slate-400 uppercase tracking-wider mt-0.5">Projects Delivered</span>
                    </div>
                    <div class="glass-panel p-4 rounded-xl border border-white/10 text-center">
                        <span class="text-2xl font-extrabold text-emerald-400 font-display">10,000+</span>
                        <span class="block text-[11px] text-slate-400 uppercase tracking-wider mt-0.5">Active Software Users</span>
                    </div>
                    <div class="glass-panel p-4 rounded-xl border border-white/10 text-center">
                        <span class="text-2xl font-extrabold text-gold font-display">95%</span>
                        <span class="block text-[11px] text-slate-400 uppercase tracking-wider mt-0.5">Client Satisfaction</span>
                    </div>
                </div>
            </div>

            <!-- Right Executive Bio & Philosophy -->
            <div class="lg:col-span-7 flex flex-col space-y-6">
                <div class="flex flex-col space-y-2">
                    <span class="text-xs font-mono uppercase tracking-widest text-gold font-bold">The Core Mission</span>
                    <h2 class="text-2xl sm:text-3xl font-extrabold text-white font-display">Engineering Speed, Precision & Predictable ROI</h2>
                </div>

                <p class="text-slate-300 leading-relaxed text-sm sm:text-base">
                    Most digital initiatives fail because of a fundamental disconnect between technical execution and business revenue. Marketing teams rarely understand socket-level concurrency, Core Web Vitals, or database index latency; while pure developers often overlook search intent, customer journey psychology, and conversion cost efficiency.
                </p>

                <p class="text-slate-400 leading-relaxed text-sm sm:text-base">
                    My practice bridges this gap entirely. Whether building <strong>Exclusive Download Manager (EDM)</strong> — a high-concurrency 32-socket Windows accelerator serving over 10,000 global users — or orchestrating high-ROAS Google Ads campaigns that generate measurable revenue, every project is engineered with mathematical precision, clean architecture, and strict adherence to industry best practices.
                </p>

                <!-- 4 Strategic Pillars -->
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                    <div class="glass-panel p-5 rounded-2xl border border-white/10 flex flex-col space-y-2">
                        <div class="flex items-center space-x-2 text-cyan">
                            <i class="fa-solid fa-code text-lg"></i>
                            <h4 class="text-sm font-bold text-white">Systems Engineering</h4>
                        </div>
                        <p class="text-xs text-slate-400 leading-relaxed">Multithreaded network engines, native Win32/C# architectures, and zero-bloat modular design.</p>
                    </div>

                    <div class="glass-panel p-5 rounded-2xl border border-white/10 flex flex-col space-y-2">
                        <div class="flex items-center space-x-2 text-emerald-400">
                            <i class="fa-solid fa-magnifying-glass-chart text-lg"></i>
                            <h4 class="text-sm font-bold text-white">Algorithmic SEO</h4>
                        </div>
                        <p class="text-xs text-slate-400 leading-relaxed">Mathematical keyword intent modeling, deep crawl budget optimization, and resilient white-hat search rankings.</p>
                    </div>

                    <div class="glass-panel p-5 rounded-2xl border border-white/10 flex flex-col space-y-2">
                        <div class="flex items-center space-x-2 text-gold">
                            <i class="fa-solid fa-bullseye text-lg"></i>
                            <h4 class="text-sm font-bold text-white">PPC & Growth Funnels</h4>
                        </div>
                        <p class="text-xs text-slate-400 leading-relaxed">Targeted Google Search, Display, and Meta performance marketing delivering predictable 5X+ ROAS.</p>
                    </div>

                    <div class="glass-panel p-5 rounded-2xl border border-white/10 flex flex-col space-y-2">
                        <div class="flex items-center space-x-2 text-indigo-400">
                            <i class="fa-solid fa-shield-halved text-lg"></i>
                            <h4 class="text-sm font-bold text-white">Zero-Bloat Ethics</h4>
                        </div>
                        <p class="text-xs text-slate-400 leading-relaxed">100% clean Authenticode signed software, zero spyware, transparent data governance, and privacy-first design.</p>
                    </div>
                </div>

                <!-- Action CTAs -->
                <div class="flex flex-wrap items-center gap-4 pt-4">
                    <a href="<?php echo function_exists('edm_get_cv_url') ? edm_get_cv_url() : get_template_directory_uri() . '/downloads/Alamin-Hossain-CV.pdf'; ?>" class="btn-premium btn-premium-primary" download="Alamin-Hossain-CV.pdf">
                        <i class="fa-solid fa-file-arrow-down mr-2"></i> Download Full CV (1.2 MB)
                    </a>
                    <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-outline">
                        <i class="fa-solid fa-calendar-check mr-2 text-cyan"></i> Book Strategy Session
                    </a>
                </div>
            </div>
        </section>

        <!-- ══════════════════════════════════════════════════════════
             3. TECHNICAL STACK & ARCHITECTURAL TOOLSET
             ══════════════════════════════════════════════════════════ -->
        <section class="flex flex-col space-y-10 border-t border-white/5 pt-16">
            <div class="flex flex-col space-y-3 text-center max-w-3xl mx-auto">
                <span class="text-xs font-mono uppercase tracking-widest text-cyan font-bold">Engineered Competency Matrix</span>
                <h2 class="text-3xl sm:text-4xl font-extrabold text-white font-display">Deep Technical Toolset & Technologies</h2>
                <p class="text-sm text-slate-400 leading-relaxed">
                    A multi-disciplinary stack engineered for enterprise stability, lightning-fast execution, and seamless cross-platform synchronization.
                </p>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                <!-- Group 1: Desktop & Systems -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <div class="flex items-center space-x-3 text-blue-400">
                        <span class="w-10 h-10 rounded-lg bg-blue-600/10 flex items-center justify-center text-xl font-bold font-mono">.NET</span>
                        <h3 class="text-base font-bold text-white">Desktop & Systems</h3>
                    </div>
                    <ul class="space-y-2 text-xs text-slate-300">
                        <li class="flex items-center justify-between"><span>C# & .NET 10 / 9</span> <span class="text-cyan font-mono font-bold">Expert</span></li>
                        <li class="flex items-center justify-between"><span>Windows Win32 / WPF</span> <span class="text-cyan font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Socket Multithreading (32x)</span> <span class="text-cyan font-mono font-bold">Specialist</span></li>
                        <li class="flex items-center justify-between"><span>SQLite Embedded Engine</span> <span class="text-cyan font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Authenticode Code Signing</span> <span class="text-cyan font-mono font-bold">Certified</span></li>
                    </ul>
                </div>

                <!-- Group 2: Full-Stack Web & APIs -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <div class="flex items-center space-x-3 text-cyan">
                        <span class="w-10 h-10 rounded-lg bg-cyan-600/10 flex items-center justify-center text-xl font-bold font-mono">JS</span>
                        <h3 class="text-base font-bold text-white">Web Architecture</h3>
                    </div>
                    <ul class="space-y-2 text-xs text-slate-300">
                        <li class="flex items-center justify-between"><span>JavaScript ESNext & TypeScript</span> <span class="text-cyan font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>PHP 8.3 & Modern WordPress</span> <span class="text-cyan font-mono font-bold">Master</span></li>
                        <li class="flex items-center justify-between"><span>REST API & WebSockets</span> <span class="text-cyan font-mono font-bold">Expert</span></li>
                        <li class="flex items-center justify-between"><span>Tailwind CSS & Glassmorphism</span> <span class="text-cyan font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Browser Extension MV3 (IPC)</span> <span class="text-cyan font-mono font-bold">Specialist</span></li>
                    </ul>
                </div>

                <!-- Group 3: Technical Search Engine Optimization -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <div class="flex items-center space-x-3 text-emerald-400">
                        <span class="w-10 h-10 rounded-lg bg-emerald-600/10 flex items-center justify-center text-xl font-bold font-mono">SEO</span>
                        <h3 class="text-base font-bold text-white">Algorithmic SEO</h3>
                    </div>
                    <ul class="space-y-2 text-xs text-slate-300">
                        <li class="flex items-center justify-between"><span>Technical SEO & Core Web Vitals</span> <span class="text-emerald-400 font-mono font-bold">Expert</span></li>
                        <li class="flex items-center justify-between"><span>Schema & JSON-LD Structured Data</span> <span class="text-emerald-400 font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Programmatic Content Silos</span> <span class="text-emerald-400 font-mono font-bold">Specialist</span></li>
                        <li class="flex items-center justify-between"><span>Search Console & Screaming Frog</span> <span class="text-emerald-400 font-mono font-bold">Master</span></li>
                        <li class="flex items-center justify-between"><span>White-Hat Link Authority</span> <span class="text-emerald-400 font-mono font-bold">Proven</span></li>
                    </ul>
                </div>

                <!-- Group 4: Growth, PPC & Analytics -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <div class="flex items-center space-x-3 text-gold">
                        <span class="w-10 h-10 rounded-lg bg-gold/10 flex items-center justify-center text-xl font-bold font-mono">ROI</span>
                        <h3 class="text-base font-bold text-white">PPC & Analytics</h3>
                    </div>
                    <ul class="space-y-2 text-xs text-slate-300">
                        <li class="flex items-center justify-between"><span>Google Ads Search & Shopping</span> <span class="text-gold font-mono font-bold">Certified</span></li>
                        <li class="flex items-center justify-between"><span>Meta Pixel & Conversion API (CAPI)</span> <span class="text-gold font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Google Analytics 4 (GA4) & GTM</span> <span class="text-gold font-mono font-bold">Master</span></li>
                        <li class="flex items-center justify-between"><span>A/B Split Testing & Heatmaps</span> <span class="text-gold font-mono font-bold">Advanced</span></li>
                        <li class="flex items-center justify-between"><span>Multi-Touch Attribution</span> <span class="text-gold font-mono font-bold">Specialist</span></li>
                    </ul>
                </div>
            </div>
        </section>

        <!-- ══════════════════════════════════════════════════════════
             4. THE 5-YEAR MASTERPLAN & STRATEGIC VISION (2026 - 2030)
             ══════════════════════════════════════════════════════════ -->
        <section class="flex flex-col space-y-10 border-t border-white/5 pt-16">
            <div class="flex flex-col space-y-3 max-w-3xl">
                <span class="text-xs font-mono uppercase tracking-widest text-gold font-bold">Strategic Roadmap</span>
                <h2 class="text-3xl sm:text-4xl font-extrabold text-white font-display">The 2026–2030 Engineering & Business Masterplan</h2>
                <p class="text-sm text-slate-400 leading-relaxed">
                    A transparent blueprint outlining our technology roadmap, software evolution, and enterprise scalability milestones over the next five years.
                </p>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
                <!-- Phase 1 -->
                <div class="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between space-y-6 relative overflow-hidden bg-gradient-to-b from-navy-950 to-navy-900">
                    <div class="flex items-center justify-between">
                        <span class="px-3 py-1 rounded-full bg-blue-600/20 border border-blue-500/30 text-blue-400 text-xs font-mono font-bold">PHASE 1 (2026)</span>
                        <span class="text-xs text-emerald-400 font-bold">CURRENT FOCUS</span>
                    </div>
                    <div>
                        <h3 class="text-xl font-extrabold text-white font-display mb-3">EDM v2.1.0 Turbo Consolidation</h3>
                        <p class="text-xs text-slate-400 leading-relaxed">
                            Full rollout of the 32-socket multithreaded engine, Manifest V3 browser integration for Chrome, Edge, and Firefox, and deployment of the WordPress-integrated Control Plane telemetry engine. Reaching 25,000+ active installations with 99.9% crash-free stability.
                        </p>
                    </div>
                    <ul class="text-xs text-slate-300 space-y-1.5 font-mono">
                        <li>✓ Native Win32 .NET 10 core engine</li>
                        <li>✓ 4K/8K media candidate sniffer</li>
                        <li>✓ 3-Way file sync & manifest authority</li>
                    </ul>
                </div>

                <!-- Phase 2 -->
                <div class="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between space-y-6 relative overflow-hidden bg-gradient-to-b from-navy-950 to-navy-900">
                    <div class="flex items-center justify-between">
                        <span class="px-3 py-1 rounded-full bg-cyan/20 border border-cyan/30 text-cyan text-xs font-mono font-bold">PHASE 2 (2027–2028)</span>
                        <span class="text-xs text-slate-400 font-bold">UPCOMING</span>
                    </div>
                    <div>
                        <h3 class="text-xl font-extrabold text-white font-display mb-3">Cross-Platform macOS & Linux Engines</h3>
                        <p class="text-xs text-slate-400 leading-relaxed">
                            Porting the core socket acceleration pipeline to native macOS (Apple Silicon M-series optimized via Metal & POSIX sockets) and Linux (AppImage & Flatpak). Introducing decentralized P2P assisted chunk distribution for ultra-large multi-gigabyte archives.
                        </p>
                    </div>
                    <ul class="text-xs text-slate-300 space-y-1.5 font-mono">
                        <li>• macOS Universal Binary (ARM64/x64)</li>
                        <li>• Linux daemon with CLI & WebUI</li>
                        <li>• Encrypted cloud queue synchronization</li>
                    </ul>
                </div>

                <!-- Phase 3 -->
                <div class="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between space-y-6 relative overflow-hidden bg-gradient-to-b from-navy-950 to-navy-900">
                    <div class="flex items-center justify-between">
                        <span class="px-3 py-1 rounded-full bg-gold/20 border border-gold/30 text-gold text-xs font-mono font-bold">PHASE 3 (2029–2030)</span>
                        <span class="text-xs text-slate-400 font-bold">ENTERPRISE SCALE</span>
                    </div>
                    <div>
                        <h3 class="text-xl font-extrabold text-white font-display mb-3">AI Network Optimization & Global Cloud Edge</h3>
                        <p class="text-xs text-slate-400 leading-relaxed">
                            Integrating edge machine-learning models directly into the socket distributor to predict network congestion, dynamically adjust TCP window buffers, and automatically select the fastest mirror worldwide with zero manual user configuration.
                        </p>
                    </div>
                    <ul class="text-xs text-slate-300 space-y-1.5 font-mono">
                        <li>• AI-predictive TCP packet routing</li>
                        <li>• Global edge cache relay network</li>
                        <li>• Enterprise SDK & white-label licensing</li>
                    </ul>
                </div>
            </div>
        </section>

        <!-- ══════════════════════════════════════════════════════════
             5. CODE PHILOSOPHY & ETHICAL STANDARDS
             ══════════════════════════════════════════════════════════ -->
        <section class="flex flex-col space-y-8 border-t border-white/5 pt-16">
            <div class="flex flex-col space-y-3">
                <span class="text-xs font-mono uppercase tracking-widest text-emerald-400 font-bold">Engineering Discipline</span>
                <h2 class="text-3xl sm:text-4xl font-extrabold text-white font-display">Code Philosophy & Development Principles</h2>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-3">
                    <div class="text-cyan text-2xl font-bold font-mono">01.</div>
                    <h3 class="text-lg font-bold text-white">Zero Bloat & Native Performance</h3>
                    <p class="text-xs text-slate-400 leading-relaxed">
                        We reject bloated webview-wrapped desktop apps when native Win32 and C# can achieve 100x lower memory footprints, instantaneous startup, and direct kernel socket control.
                    </p>
                </div>

                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-3">
                    <div class="text-emerald-400 text-2xl font-bold font-mono">02.</div>
                    <h3 class="text-lg font-bold text-white">Crash-Proof Durability (ACID)</h3>
                    <p class="text-xs text-slate-400 leading-relaxed">
                        All state changes, download chunk byte-ranges, and user settings use transactional SQLite write-ahead logging (WAL), guaranteeing zero data corruption even during unexpected power loss.
                    </p>
                </div>

                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-3">
                    <div class="text-gold text-2xl font-bold font-mono">03.</div>
                    <h3 class="text-lg font-bold text-white">Absolute Privacy & Zero Spyware</h3>
                    <p class="text-xs text-slate-400 leading-relaxed">
                        No adware, no bundleware, no hidden telemetry trackers, and no selling of user browsing data. Software is signed with certified Authenticode cryptographic keys.
                    </p>
                </div>
            </div>
        </section>

        <!-- ══════════════════════════════════════════════════════════
             6. DIRECT STRATEGIC CONSULTATION & CONTACT BANNER
             ══════════════════════════════════════════════════════════ -->
        <section class="glass-panel p-8 sm:p-12 rounded-3xl border border-white/10 flex flex-col lg:flex-row items-center justify-between gap-8 bg-gradient-to-r from-navy-950 via-navy-900 to-navy-950">
            <div class="flex flex-col space-y-3 max-w-2xl">
                <span class="text-xs font-mono uppercase tracking-widest text-cyan font-bold">Direct Communication</span>
                <h3 class="text-2xl sm:text-3xl font-extrabold text-white font-display">Ready to Scale Your Software or Digital Revenue?</h3>
                <p class="text-sm text-slate-400 leading-relaxed">
                    Whether you require a custom software architecture audit, technical SEO restructuring, or a data-backed Google Ads growth campaign, let's connect directly.
                </p>
                <div class="flex flex-wrap gap-4 pt-2 text-xs font-mono text-slate-300">
                    <span>📞 <strong>01888567189</strong></span>
                    <span>✉️ <strong>nfxalamin@gmail.com</strong></span>
                    <span>📍 <strong>Dhaka, Bangladesh (Global Remote)</strong></span>
                </div>
            </div>

            <div class="flex flex-col sm:flex-row gap-4 shrink-0 w-full lg:w-auto">
                <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary !px-8 !py-4 text-center">
                    <span>Consult with Alamin</span>
                    <i class="fa-solid fa-arrow-right ml-2 text-xs"></i>
                </a>
            </div>
        </section>

    </div>
</main>

<?php
get_footer();
