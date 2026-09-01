<?php
/**
 * Custom 401 Unauthorized template
 */
get_header();

$status_code = 401;
$status_message = get_status_header_desc();
if ( empty( $status_message ) ) {
    $status_message = 'You are not authorized to view this page.';
}
?>

<main class="min-h-screen bg-navy-950 flex items-center justify-center px-6 py-24">
    <section class="max-w-2xl text-center glass-panel rounded-3xl border border-white/10 p-10 shadow-2xl">
        <span class="inline-block px-4 py-1 rounded-full bg-red-500/10 text-red-400 text-xs font-semibold uppercase tracking-[0.3em] mb-4">401</span>
        <h1 class="text-4xl md:text-5xl font-display font-bold text-white mb-4">Unauthorized Access</h1>
        <p class="text-slate-300 text-base md:text-lg leading-relaxed mb-6">
            <?php echo esc_html( $status_message ); ?>
        </p>
        <div class="flex justify-center gap-3 flex-wrap">
            <a href="<?php echo esc_url( home_url( '/' ) ); ?>" class="btn-premium btn-premium-primary">Go Home</a>
            <a href="#contact" class="btn-premium btn-premium-outline">Contact Support</a>
        </div>
    </section>
</main>

<?php get_footer(); ?>
