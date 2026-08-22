<?php
/**
 * Custom 500 Internal Server Error template
 */
get_header();

$status_code = 500;
$status_message = get_status_header_desc();
if ( empty( $status_message ) ) {
    $status_message = 'Something went wrong on our side.';
}
?>

<main class="min-h-screen bg-navy-950 flex items-center justify-center px-6 py-24">
    <section class="max-w-2xl text-center glass-panel rounded-3xl border border-white/10 p-10 shadow-2xl">
        <span class="inline-block px-4 py-1 rounded-full bg-amber-500/10 text-amber-300 text-xs font-semibold uppercase tracking-[0.3em] mb-4">500</span>
        <h1 class="text-4xl md:text-5xl font-display font-bold text-white mb-4">Server Error</h1>
        <p class="text-slate-300 text-base md:text-lg leading-relaxed mb-6">
            <?php echo esc_html( $status_message ); ?>
        </p>
        <p class="text-slate-400 mb-6">Please refresh the page or try again in a few moments.</p>
        <div class="flex justify-center gap-3 flex-wrap">
            <a href="<?php echo esc_url( home_url( '/' ) ); ?>" class="btn-premium btn-premium-primary">Return Home</a>
            <a href="#contact" class="btn-premium btn-premium-outline">Report Issue</a>
        </div>
    </section>
</main>

<?php get_footer(); ?>
