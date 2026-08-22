<?php
/**
 * Template Name: Terms of Service & Software EULA
 * Description: Legal-grade Software End-User License Agreement and Terms of Service.
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
    <div class="absolute top-10 right-1/4 w-[600px] h-[600px] bg-cyan-600/10 rounded-full blur-[150px] pointer-events-none -z-10"></div>

    <div class="w-full max-w-[96%] 2xl:max-w-[1400px] mx-auto flex flex-col space-y-12">
        
        <!-- Header -->
        <div class="flex flex-col space-y-4 border-b border-white/10 pb-8">
            <div class="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-blue-600/10 border border-blue-500/20 w-fit">
                <span class="w-2 h-2 rounded-full bg-cyan"></span>
                <span class="text-xs font-mono uppercase tracking-widest text-cyan font-bold">Official Software Licensing & Service Agreement</span>
            </div>

            <h1 class="text-3xl sm:text-5xl font-extrabold text-white font-display tracking-tight leading-tight">
                Terms of Service & End-User License Agreement (EULA)
            </h1>

            <div class="flex flex-wrap items-center gap-6 text-xs text-slate-400 font-mono">
                <span>Effective Date: <strong>August 22, 2026</strong></span>
                <span>•</span>
                <span>Software Build: <strong>Exclusive Download Manager v2.1.0+</strong></span>
                <span>•</span>
                <span>Author & Publisher: <strong>Alamin Hossain</strong></span>
            </div>
        </div>

        <!-- Terms Content Sections -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-12 items-start">
            
            <!-- Main Legal Content -->
            <div class="lg:col-span-8 flex flex-col space-y-10 text-slate-300 text-sm sm:text-base leading-relaxed">
                
                <!-- Section 1 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-blue-600/20 text-blue-400 flex items-center justify-center text-sm font-mono font-bold">01</span>
                        Acceptance of Terms & Software Scope
                    </h2>
                    <p>
                        By installing, copying, downloading, accessing, or otherwise using <strong>Exclusive Download Manager (EDM)</strong> or engaging in professional marketing consultation services provided by <strong>Alamin Hossain</strong>, you agree to be bound by the terms of this Agreement. If you do not agree to these terms, do not install or use the software.
                    </p>
                </section>

                <!-- Section 2 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-cyan-600/20 text-cyan flex items-center justify-center text-sm font-mono font-bold">02</span>
                        Software License Grant & Usage Tiers
                    </h2>
                    <p>
                        Subject to the terms of this Agreement, we grant you a non-exclusive, revocable, non-transferable license to use EDM according to your purchased license tier:
                    </p>
                    <ul class="list-disc list-inside space-y-2 text-xs sm:text-sm text-slate-400 pl-2">
                        <li><strong>Free / 30-Day Evaluation Tier:</strong> Granted for personal testing, non-commercial evaluation of the 32-socket turbo engine, and basic browser extension integration.</li>
                        <li><strong>Pro License Tier:</strong> Valid for activation on up to 3 individual Windows PCs owned by the license holder, enabling full 32-socket concurrency, unlimited 4K video streams, and automated browser takeovers.</li>
                        <li><strong>Enterprise Lifetime Tier:</strong> Granted for commercial business workstations with uncapped multi-device deployment rights, priority VIP updates, and direct technical support.</li>
                    </ul>
                </section>

                <!-- Section 3 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-emerald-600/20 text-emerald-400 flex items-center justify-center text-sm font-mono font-bold">03</span>
                        Permitted Use & Ethical Restrictions
                    </h2>
                    <p>
                        You agree to use EDM in compliance with all applicable local, national, and international laws. Specifically, you agree NOT to:
                    </p>
                    <ul class="list-disc list-inside space-y-1.5 text-xs sm:text-sm text-slate-400 pl-2">
                        <li>Reverse-engineer, decompile, decrypt, or disassemble any binary components, DRM license validation routines, or API endpoints.</li>
                        <li>Distribute modified, cracked, or keygenerated versions of the software installer.</li>
                        <li>Use the multi-socket download accelerator to perform intentional Denial-of-Service (DoS) attacks on web servers.</li>
                        <li>Circumvent copyright protection mechanisms on digital media where explicitly prohibited by law.</li>
                    </ul>
                </section>

                <!-- Section 4 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-gold/20 text-gold flex items-center justify-center text-sm font-mono font-bold">04</span>
                        30-Day Money-Back Guarantee & Refund Policy
                    </h2>
                    <p>
                        We stand behind our software with total confidence. If EDM does not accelerate your downloads or fails to integrate with your browser as described, you may request a <strong>100% full refund within 30 days</strong> of purchase.
                    </p>
                    <p class="text-xs sm:text-sm text-slate-400">
                        Refunds are processed promptly with zero hassle via email request to <a href="mailto:nfxalamin@gmail.com" class="text-cyan font-bold">nfxalamin@gmail.com</a>.
                    </p>
                </section>

                <!-- Section 5 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-indigo-600/20 text-indigo-400 flex items-center justify-center text-sm font-mono font-bold">05</span>
                        Intellectual Property & Code Rights
                    </h2>
                    <p>
                        All title, copyright, intellectual property rights, and code architectures in and to Exclusive Download Manager (including but not limited to the 32-socket scheduling algorithm, native Win32 IPC bridge, and UI designs) are owned exclusively by <strong>Alamin Hossain</strong>.
                    </p>
                </section>

                <!-- Section 6 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-pink-600/20 text-pink-400 flex items-center justify-center text-sm font-mono font-bold">06</span>
                        Warranty Disclaimer & Limitation of Liability
                    </h2>
                    <p class="text-xs sm:text-sm text-slate-400">
                        EDM is provided on an "AS IS" and "AS AVAILABLE" basis. While we maintain rigorous quality assurance and Authenticode verification, Alamin Hossain shall not be liable for any indirect, incidental, or consequential damages resulting from network ISP outages, server-side rate limits, or third-party web changes.
                    </p>
                </section>

            </div>

            <!-- Right Summary Sticky Sidebar -->
            <div class="lg:col-span-4 flex flex-col space-y-6 sticky top-28">
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                        <i class="fa-solid fa-scale-balanced text-gold"></i>
                        Legal & Licensing Support
                    </h3>
                    <p class="text-xs text-slate-400 leading-relaxed">
                        For commercial licensing, enterprise volume agreements, or reseller inquiries, contact our office directly:
                    </p>
                    <div class="text-xs font-mono space-y-2 border-t border-white/5 pt-3">
                        <div><span class="text-slate-500">Legal Contact:</span> <strong class="text-white">Alamin Hossain</strong></div>
                        <div><span class="text-slate-500">Inquiry Email:</span> <a href="mailto:nfxalamin@gmail.com" class="text-cyan font-bold hover:underline">nfxalamin@gmail.com</a></div>
                        <div><span class="text-slate-500">Direct Phone:</span> <a href="tel:01888567189" class="text-white font-bold hover:underline">01888567189</a></div>
                    </div>
                </div>

                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-3 bg-gradient-to-b from-navy-900 to-navy-950">
                    <h4 class="text-xs uppercase tracking-widest text-cyan font-bold">Quick Navigation</h4>
                    <ul class="space-y-2 text-xs">
                        <li><a href="<?php echo esc_url(home_url('/privacy/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-shield-halved text-slate-500"></i> Privacy Policy & Data Charter</a></li>
                        <li><a href="<?php echo esc_url(home_url('/edm-download/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-download text-slate-500"></i> Official Downloads (19.8 MB)</a></li>
                        <li><a href="<?php echo esc_url(home_url('/about/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-user text-slate-500"></i> About Alamin Hossain</a></li>
                    </ul>
                </div>
            </div>

        </div>

    </div>
</main>

<?php
get_footer();
