<?php
/**
 * Custom 503 Service Unavailable template
 */
get_header();

$status_code = 503;
$status_message = get_status_header_desc();
if ( empty( $status_message ) ) {
    $status_message = 'The service is temporarily unavailable. Please try again soon.';
}
?>

<main class="min-h-screen bg-navy-950 flex items-center justify-center px-6 py-24">
    <section class="max-w-2xl text-center glass-panel rounded-3xl border border-white/10 p-10 shadow-2xl">
        <span class="inline-block px-4 py-1 rounded-full bg-cyan-500/10 text-cyan-300 text-xs font-semibold uppercase tracking-[0.3em] mb-4">503</span>
        <h1 class="text-4xl md:text-5xl font-display font-bold text-white mb-4">Service Unavailable</h1>
        <p class="text-slate-300 text-base md:text-lg leading-relaxed mb-6">
            <?php echo esc_html( $status_message ); ?>
        </p>
        <p class="text-slate-400 mb-6">We are currently performing maintenance. Please check back shortly.</p>
        <a href="<?php echo esc_url( home_url( '/' ) ); ?>" class="btn-premium btn-premium-primary">Try Again</a>
    </section>
</main>

<?php get_footer(); ?>
