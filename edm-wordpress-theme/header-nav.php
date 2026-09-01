<!-- Ambient Background Blobs -->
<div class="absolute inset-0 overflow-hidden pointer-events-none -z-10 h-screen w-full">
    <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-blue-900/10 rounded-full blur-[120px]"></div>
    <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-cyan-900/10 rounded-full blur-[150px]"></div>
</div>

<!-- COMPACT PREMIUM GLASS HEADER (h-14 / 56px, blur-20px, responsive) -->
<header id="main-header" class="site-header sticky top-0 z-50 transition-all duration-300 backdrop-blur-xl bg-slate-950/70 dark:bg-[#030712]/75 border-b border-white/10 shadow-lg shadow-black/20">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between">
        
        <!-- Brand / Monogram Logo -->
        <a href="<?php echo esc_url(home_url('/')); ?>" class="flex items-center space-x-2.5 group">
            <span class="w-8 h-8 rounded-lg bg-gradient-to-tr from-blue-600 via-cyan-500 to-teal-400 flex items-center justify-center font-bold text-white text-sm shadow-md shadow-cyan-500/20 border border-white/20 font-display group-hover:scale-105 transition-transform duration-300">
                AH
            </span>
            <div class="flex flex-col">
                <span class="font-bold text-xs tracking-wider uppercase text-white leading-tight font-display group-hover:text-cyan transition-colors">
                    Alamin Hossain
                </span>
                <span class="text-[9px] tracking-widest text-gold font-medium uppercase leading-none">
                    Growth Expert
                </span>
            </div>
        </a>

        <!-- Desktop Navigation Links -->
        <nav class="hidden lg:flex items-center space-x-5 text-slate-300">
            <a href="<?php echo esc_url(home_url('/')); ?>" class="nav-link text-[11px] font-semibold tracking-wider uppercase hover:text-cyan transition-colors <?php echo is_front_page() ? 'text-cyan' : ''; ?>">Home</a>
            <a href="<?php echo esc_url(home_url('/about-me/')); ?>" class="nav-link text-[11px] font-semibold tracking-wider uppercase hover:text-cyan transition-colors">About</a>
            <a href="<?php echo esc_url(home_url('/services/')); ?>" class="nav-link text-[11px] font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Services</a>
            <a href="<?php echo esc_url(home_url('/portfolio/')); ?>" class="nav-link text-[11px] font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Portfolio</a>
            <a href="<?php echo esc_url(home_url('/results/')); ?>" class="nav-link text-[11px] font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Results</a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="nav-link nav-link-edm text-[11px] font-bold tracking-wider uppercase text-cyan hover:text-white transition-all duration-300 flex items-center gap-1.5 px-3 py-1 rounded-lg bg-cyan-500/10 border border-cyan-500/25 shadow-sm shadow-cyan-500/20 hover:bg-cyan-500/20 hover:border-cyan-400">
                <span class="w-2 h-2 rounded-full bg-cyan animate-pulse"></span>
                <span>EDM</span>
            </a>
        </nav>

        <!-- Theme Toggle & CTA Desktop -->
        <div class="hidden lg:flex items-center space-x-3">
            <button @click="darkMode = !darkMode" class="text-slate-400 hover:text-cyan transition-colors p-1.5 rounded-lg hover:bg-white/5 text-sm" aria-label="Toggle Theme">
                <i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i>
            </button>
            <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-primary !px-4 !py-1.5 !text-[10px] !tracking-wider">
                Hire Me
            </a>
        </div>

        <!-- Mobile Hamburger Controls -->
        <div class="lg:hidden flex items-center space-x-2">
            <button @click="darkMode = !darkMode" class="text-slate-400 hover:text-cyan p-1.5 text-sm" aria-label="Toggle Theme">
                <i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i>
            </button>
            <button @click="mobileMenuOpen = !mobileMenuOpen" class="text-slate-300 hover:text-cyan p-1.5 focus:outline-none" aria-label="Toggle Menu">
                <i class="fa-solid" :class="mobileMenuOpen ? 'fa-xmark text-lg' : 'fa-bars text-lg'"></i>
            </button>
        </div>
    </div>

    <!-- Mobile Slideout Navigation (matching glass theme) -->
    <div x-show="mobileMenuOpen" x-cloak class="lg:hidden backdrop-blur-2xl bg-slate-950/95 border-b border-white/10 py-4 px-5 z-40 transition-all duration-300">
        <nav class="flex flex-col space-y-2">
            <a href="<?php echo esc_url(home_url('/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5 hover:text-cyan <?php echo is_front_page() ? 'text-cyan font-bold' : ''; ?>">Home</a>
            <a href="<?php echo esc_url(home_url('/about-me/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5 hover:text-cyan">About Me</a>
            <a href="<?php echo esc_url(home_url('/services/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5 hover:text-cyan">Services</a>
            <a href="<?php echo esc_url(home_url('/portfolio/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5 hover:text-cyan">Portfolio</a>
            <a href="<?php echo esc_url(home_url('/results/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-slate-300 py-2 border-b border-white/5 hover:text-cyan">Results & Testimonials</a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" @click="mobileMenuOpen = false" class="text-xs tracking-wider uppercase text-cyan font-bold py-2.5 px-3 rounded-xl bg-cyan-500/10 border border-cyan-500/25 flex items-center justify-between">
                <span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full bg-cyan animate-pulse"></span> EDM Product Hub</span>
                <i class="fa-solid fa-arrow-up-right-from-square text-[10px]"></i>
            </a>
            <a href="<?php echo esc_url(home_url('/contact/')); ?>" @click="mobileMenuOpen = false" class="mt-3 w-full text-center btn-premium btn-premium-primary !py-2 !text-xs">
                Hire Me & Book Meeting
            </a>
        </nav>
    </div>
</header>