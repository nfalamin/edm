<?php
/**
 * The template for displaying 404 pages (Not Found)
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-28 pb-20 px-6 flex items-center justify-center relative overflow-hidden">
    <!-- Ambient Background Aura -->
    <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-blue-600/10 rounded-full blur-[140px] pointer-events-none -z-10"></div>
    
    <div class="max-w-xl mx-auto text-center flex flex-col items-center space-y-6">
        
        <div class="relative">
            <span class="text-8xl sm:text-9xl font-black font-display bg-gradient-to-r from-blue-500 via-cyan-400 to-indigo-500 bg-clip-text text-transparent opacity-80">404</span>
            <div class="absolute inset-0 flex items-center justify-center">
                <span class="text-sm uppercase tracking-widest text-gold font-bold bg-navy-950/80 px-4 py-1 rounded-full border border-white/10">Page Not Found</span>
            </div>
        </div>

        <h1 class="text-2xl sm:text-3xl font-extrabold text-white font-display">Oops! This Coordinate Does Not Exist</h1>
        
        <p class="text-slate-400 text-sm sm:text-base leading-relaxed max-w-md">
            The page or resource you are looking for might have been moved, renamed, or is temporarily offline. Explore our active hubs below:
        </p>

        <div class="flex flex-wrap items-center justify-center gap-4 pt-4">
            <a href="<?php echo esc_url(home_url('/')); ?>" class="btn-premium btn-premium-primary">
                <i class="fa-solid fa-house mr-1.5"></i> Return Home
            </a>
            <a href="<?php echo esc_url(home_url('/edm/')); ?>" class="btn-premium btn-premium-outline">
                <i class="fa-solid fa-bolt mr-1.5 text-cyan"></i> EDM Product Hub
            </a>
            <a href="<?php echo esc_url(home_url('/contact/')); ?>" class="btn-premium btn-premium-outline">
                <i class="fa-solid fa-envelope mr-1.5"></i> Contact Support
            </a>
        </div>

        <!-- Quick Search Form -->
        <div class="w-full pt-6">
            <form role="search" method="get" class="flex items-center max-w-md mx-auto relative" action="<?php echo esc_url(home_url('/')); ?>">
                <input type="search" class="w-full bg-navy-900/90 border border-white/10 rounded-xl px-4 py-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-cyan transition-colors font-sans" placeholder="Search portfolio or EDM docs..." value="<?php echo get_search_query(); ?>" name="s">
                <button type="submit" class="absolute right-2 px-3 py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-xs font-semibold transition-colors">Search</button>
            </form>
        </div>

    </div>
</main>

<?php
get_footer();
