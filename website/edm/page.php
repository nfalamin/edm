<?php
/**
 * Universal Page Template & Dynamic Routing Engine
 * Automatically routes /edm, /nfdashbord, /about, /contact, /services, /portfolio
 * and renders full content for all custom WordPress pages with 0 blank screens.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

global $post;
$slug = $post->post_name ?? '';
$title = strtolower(trim($post->post_title ?? ''));

// 1. Auto-route to EDM Product Hub
if ($slug === 'edm' || $slug === 'edm-hub' || $title === 'edm' || $title === 'exclusive download manager') {
    require get_template_directory() . '/page-edm.php';
    return;
}

// 2. Auto-route to ControlPlane Dashboard
if (in_array($slug, ['nfdashbord', 'nfdashboard', 'nf', 'dashboard', 'dashbord', 'control-plane'], true)) {
    require get_template_directory() . '/page-nfdashbord.php';
    return;
}

// 3. Auto-route to EDM Downloads Hub
if (in_array($slug, ['edm-download', 'download', 'downloads'], true)) {
    require get_template_directory() . '/page-edm-download.php';
    return;
}

// 4. Auto-route to EDM Features
if (in_array($slug, ['edm-features', 'features'], true)) {
    require get_template_directory() . '/page-edm-features.php';
    return;
}

// 5. Auto-route to EDM Extensions
if (in_array($slug, ['edm-extensions', 'extensions'], true)) {
    require get_template_directory() . '/page-edm-extensions.php';
    return;
}

// 6. Auto-route to About
if ($slug === 'about' || $slug === 'about-me') {
    require get_template_directory() . '/page-about.php';
    return;
}

// 7. Auto-route to Services
if ($slug === 'services') {
    require get_template_directory() . '/page-services.php';
    return;
}

// 8. Auto-route to Portfolio
if ($slug === 'portfolio' || $slug === 'projects') {
    require get_template_directory() . '/page-portfolio.php';
    return;
}

// 9. Auto-route to Contact
if ($slug === 'contact' || $slug === 'contact-me') {
    require get_template_directory() . '/page-contact.php';
    return;
}

// 10. Auto-route to Privacy Policy
if ($slug === 'privacy' || $slug === 'privacy-policy') {
    require get_template_directory() . '/page-privacy.php';
    return;
}

// 11. Auto-route to Terms
if ($slug === 'terms' || $slug === 'terms-of-service' || $slug === 'eula') {
    require get_template_directory() . '/page-terms.php';
    return;
}

// 12. Standard Premium Glassmorphic Page Fallback
get_header();
?>

<main class="min-h-screen pt-24 pb-20 px-6 bg-mesh-net">
    <div class="w-full max-w-[96%] 2xl:max-w-[1440px] mx-auto flex flex-col space-y-10">
        
        <?php while (have_posts()) : the_post(); ?>
            <header class="section-header text-left">
                <div class="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-blue-600/10 border border-blue-500/20 w-fit mb-4">
                    <span class="w-2 h-2 rounded-full bg-cyan animate-pulse"></span>
                    <span class="text-xs font-mono uppercase tracking-widest text-cyan font-bold"><?php bloginfo('name'); ?></span>
                </div>
                <h1 class="text-3xl sm:text-5xl font-extrabold text-white font-display tracking-tight leading-tight">
                    <?php the_title(); ?>
                </h1>
            </header>

            <article id="post-<?php the_ID(); ?>" <?php post_class('glass-panel p-6 sm:p-10 rounded-3xl border border-white/10 text-slate-300 leading-relaxed'); ?>>
                <?php if (has_post_thumbnail()) : ?>
                    <div class="mb-8 rounded-2xl overflow-hidden">
                        <?php the_post_thumbnail('full', ['class' => 'w-full h-auto object-cover']); ?>
                    </div>
                <?php endif; ?>

                <div class="entry-content prose prose-invert max-w-none">
                    <?php the_content(); ?>
                </div>
            </article>
        <?php endwhile; ?>

    </div>
</main>

<?php
get_footer();
