<?php
/**
 * Template Name: Services & Strategy - Alamin Hossain
 * Description: Dedicated Services page template showcasing marketing and software services.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-24 pb-20 px-6">
    <div class="w-full max-w-[96%] 2xl:max-w-[1820px] mx-auto flex flex-col space-y-16">
        
        <!-- Header -->
        <div class="flex flex-col space-y-3">
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Specialized Capabilities</span>
            <h1 class="text-4xl md:text-5xl lg:text-6xl font-extrabold text-white font-display tracking-tight leading-tight">
                High-Impact Services Engineered <br>
                <span class="bg-gradient-to-r from-blue-400 via-cyan-300 to-indigo-400 bg-clip-text text-transparent">For Measurable Business ROI</span>
            </h1>
        </div>

        <!-- Services Partial -->
        <?php get_template_part('section-services'); ?>

        <!-- Direct Contact Banner -->
        <div class="glass-panel p-8 md:p-12 rounded-3xl border border-white/10 flex flex-col md:flex-row items-center justify-between gap-8 bg-gradient-to-r from-navy-950 to-navy-900">
            <div class="flex flex-col space-y-2 max-w-xl">
                <h3 class="text-2xl font-bold text-white font-display">Need a Customized Growth Strategy?</h3>
                <p class="text-sm text-slate-400">Directly contact Alamin for technical SEO audits, Google Ads ROAS scaling, or custom SaaS web architecture.</p>
                <span class="text-xs text-cyan font-mono pt-1">📞 01888567189 · ✉️ nfxalamin@gmail.com</span>
            </div>
            <a href="<?php echo esc_url(home_url('/contact')); ?>" class="btn-premium btn-premium-primary !px-8 !py-4 whitespace-nowrap">
                <span>Request Free Audit</span>
                <i class="fa-solid fa-arrow-right ml-2 text-xs"></i>
            </a>
        </div>

    </div>
</main>

<?php
get_footer();
