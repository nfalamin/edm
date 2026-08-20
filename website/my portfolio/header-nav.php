<!-- Glowing Background blobs -->
<div class="absolute inset-0 overflow-hidden pointer-events-none -z-10 h-screen w-full">
    <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-blue-900/10 rounded-full blur-[120px]"></div>
    <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-cyan-900/10 rounded-full blur-[150px]"></div>
    <div class="absolute bottom-1/4 left-10 w-[450px] h-[450px] bg-blue-950/20 rounded-full blur-[100px]"></div>
</div>

<!-- HEADER / NAVIGATION -->
<header id="main-header" class="site-header sticky top-0 z-50 glass-panel border-b border-white/5 transition-all duration-300">
    <div class="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
        <a href="<?php echo esc_url(home_url('/')); ?>" class="flex items-center space-x-2.5">
            <span class="w-10 h-10 rounded-lg bg-gradient-to-tr from-blue-600 to-cyan-600 flex items-center justify-center font-bold text-white text-lg shadow-lg border border-white/10 font-display">AH</span>
            <div class="flex flex-col">
                <span class="font-bold text-sm tracking-wider uppercase text-white leading-tight font-display">Alamin Hossain</span>
                <span class="text-[10px] tracking-widest text-gold font-medium uppercase">Growth Expert</span>
            </div>
        </a>

        <!-- Desktop Nav -->
        <nav class="hidden lg:flex items-center space-x-8 text-slate-300">
            <a href="<?php echo is_front_page() ? '#about' : esc_url(home_url('/#about')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">About</a>
            <a href="<?php echo is_front_page() ? '#services' : esc_url(home_url('/#services')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Services</a>
            <a href="<?php echo is_front_page() ? '#portfolio' : esc_url(home_url('/#portfolio')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Portfolio</a>
            <a href="<?php echo is_front_page() ? '#case-studies' : esc_url(home_url('/#case-studies')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Case Studies</a>
            <a href="<?php echo is_front_page() ? '#skills' : esc_url(home_url('/#skills')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Skills</a>
            <a href="<?php echo is_front_page() ? '#pricing' : esc_url(home_url('/#pricing')); ?>" class="nav-link text-xs font-semibold tracking-wider uppercase hover:text-cyan transition-colors">Pricing</a>
        </nav>

        <!-- Theme Toggle & CTA Desktop -->
        <div class="hidden lg:flex items-center space-x-4">
            <button @click="darkMode = !darkMode" class="text-slate-500 hover:text-cyan transition-colors focus:outline-none text-lg">
                <i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i>
            </button>
            <a href="#contact" class="btn-premium btn-premium-primary !px-6 !py-2.5 !text-[10px]">Hire Me</a>
        </div>

        <!-- Mobile Controls -->
        <div class="lg:hidden flex items-center space-x-4">
            <button @click="darkMode = !darkMode" class="text-slate-500 hover:text-cyan transition-colors focus:outline-none text-lg"><i class="fa-solid fa-moon" x-show="!darkMode"></i><i class="fa-solid fa-sun" x-show="darkMode" x-cloak></i></button>
            <button @click="mobileMenuOpen = !mobileMenuOpen" class="text-slate-400 hover:text-cyan focus:outline-none"><i class="fa-solid" :class="mobileMenuOpen ? 'fa-xmark text-xl' : 'fa-bars text-xl'"></i></button>
        </div>
    </div>

    <!-- Sub/Mobile Nav Menu -->
    <div x-show="mobileMenuOpen" x-cloak class="lg:hidden absolute top-20 left-0 w-full glass-panel border-b border-white/10 py-6 px-6 z-40">
        <nav class="flex flex-col space-y-4">
            <a href="<?php echo is_front_page() ? '#about' : esc_url(home_url('/#about')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">About</a>
            <a href="<?php echo is_front_page() ? '#services' : esc_url(home_url('/#services')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Services</a>
            <a href="<?php echo is_front_page() ? '#portfolio' : esc_url(home_url('/#portfolio')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Portfolio</a>
            <a href="<?php echo is_front_page() ? '#case-studies' : esc_url(home_url('/#case-studies')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Case Studies</a>
            <a href="<?php echo is_front_page() ? '#skills' : esc_url(home_url('/#skills')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Skills</a>
            <a href="<?php echo is_front_page() ? '#pricing' : esc_url(home_url('/#pricing')); ?>" @click="mobileMenuOpen = false" class="nav-link text-sm tracking-wider uppercase text-slate-300 py-2 border-b border-white/5">Pricing</a>
            <a href="#contact" @click="mobileMenuOpen = false" class="mt-4 w-full text-center btn-premium btn-premium-primary">Hire Me</a>
        </nav>
    </div>
</header>