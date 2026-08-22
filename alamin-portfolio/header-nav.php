<!-- Glowing Background blobs -->
<div class="absolute inset-0 overflow-hidden pointer-events-none -z-10 h-screen w-full">
    <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-blue-900/10 rounded-full blur-[120px]"></div>
    <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-cyan-900/10 rounded-full blur-[150px]"></div>
    <div class="absolute bottom-1/4 left-10 w-[450px] h-[450px] bg-blue-950/20 rounded-full blur-[100px]"></div>
</div>

<!-- HEADER / NAVIGATION -->
<header id="main-header" class="site-header sticky top-0 z-50 glass-panel border-b border-white/5 transition-all duration-300">
    <div class="w-full max-w-[96%] 2xl:max-w-[1820px] mx-auto px-4 sm:px-6 h-14 sm:h-16 lg:h-20 flex items-center justify-between">
        <a href="<?php echo esc_url(home_url('/')); ?>" class="flex items-center space-x-2 sm:space-x-2.5">
            <span class="w-8 h-8 sm:w-10 sm:h-10 rounded-lg bg-gradient-to-tr from-blue-600 to-cyan-600 flex items-center justify-center font-bold text-white text-sm sm:text-lg shadow-lg border border-white/10 font-display">AH</span>
            <div class="flex flex-col">
                <span class="font-bold text-xs sm:text-sm tracking-wider uppercase text-white leading-tight font-display"><?php echo esc_html(get_theme_mod('hero_name', 'Alamin Hossain')); ?></span>
                <span class="text-[9px] sm:text-[10px] tracking-widest text-gold font-medium uppercase"><?php echo esc_html(get_theme_mod('hero_tagline', 'Growth Expert')); ?></span>
            </div>
        </a>

        <!-- Desktop Nav -->
        <nav class="hidden lg:flex items-center space-x-7 text-slate-300">
            <a href="<?php echo is_front_page() ? '#about' : esc_url(home_url('/about/')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">About</a>
            <a href="<?php echo is_front_page() ? '#services' : esc_url(home_url('/services/')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Services</a>
            <a href="<?php echo is_front_page() ? '#portfolio' : esc_url(home_url('/portfolio/')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Portfolio</a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase text-cyan hover:text-white transition-colors flex items-center gap-1.5"><span class="w-1.5 h-1.5 rounded-full bg-cyan animate-pulse"></span>EDM Software</a>
            <a href="<?php echo is_front_page() ? '#skills' : esc_url(home_url('/#skills')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Skills</a>
            <a href="<?php echo is_front_page() ? '#pricing' : esc_url(home_url('/edm/#pricing')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Pricing</a>
            <a href="<?php echo is_front_page() ? '#contact' : esc_url(home_url('/contact/')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Contact</a>
        </nav>

        <!-- Theme Toggle & CTA Desktop -->
        <div class="hidden lg:flex items-center space-x-4">
            <button @click="darkMode = !darkMode" class="text-slate-400 hover:text-cyan transition-colors focus:outline-none text-base" title="Toggle Theme">
                <i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i>
            </button>
            <a href="<?php echo is_front_page() ? '#contact' : esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary !px-5 !py-2.5 !text-[11px]">
                <i class="fa-solid fa-paper-plane mr-1.5"></i>
                Hire Me
            </a>
        </div>

        <!-- Mobile Controls -->
        <div class="lg:hidden flex items-center space-x-3">
            <button @click="darkMode = !darkMode" class="text-slate-400 hover:text-cyan transition-colors focus:outline-none text-base" aria-label="Toggle Theme"><i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i></button>
            <button @click="mobileMenuOpen = !mobileMenuOpen" class="text-slate-300 hover:text-cyan focus:outline-none p-1" aria-label="Open Mobile Menu"><i class="fa-solid" :class="mobileMenuOpen ? 'fa-xmark text-xl' : 'fa-bars text-xl'"></i></button>
        </div>
    </div>

    <!-- Sub/Mobile Nav Menu -->
    <div x-show="mobileMenuOpen" x-cloak class="lg:hidden absolute top-14 sm:top-16 left-0 w-full glass-panel border-b border-white/10 py-5 px-5 z-40 max-h-[85vh] overflow-y-auto">
        <nav class="flex flex-col space-y-3">
            <a href="<?php echo is_front_page() ? '#about' : esc_url(home_url('/about/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">About Me</a>
            <a href="<?php echo is_front_page() ? '#services' : esc_url(home_url('/services/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Services &amp; Pricing</a>
            <a href="<?php echo is_front_page() ? '#portfolio' : esc_url(home_url('/portfolio/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Case Studies &amp; Portfolio</a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-cyan py-2 border-b border-white/5 font-bold">⚡ EDM Software Hub</a>
            <a href="<?php echo esc_url(home_url('/edm-extensions/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">🧩 Browser Extensions</a>
            <a href="<?php echo esc_url(home_url('/edm-download/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">📥 Official Downloads (19.8 MB)</a>
            <a href="<?php echo is_front_page() ? '#contact' : esc_url(home_url('/contact/')); ?>" @click="mobileMenuOpen = false" class="nav-link text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Contact / Consultation</a>
            <a href="<?php echo is_front_page() ? '#contact' : esc_url(home_url('/contact/')); ?>" @click="mobileMenuOpen = false" class="mt-3 w-full text-center btn-premium btn-premium-primary !py-2.5 !text-xs">Hire Me (01888567189)</a>
        </nav>
    </div>
</header>